using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TestingStudents
{
    /// <summary>
    /// Кабинет преподавателя.
    /// Видит только свои дисциплины (через UP03_PrepodPredmet),
    /// свои тесты и результаты студентов по ним.
    /// </summary>
    public partial class TeacherMainWindow : Window
    {
        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        private SqlConnection connection;
        private int currentUserId;
        private string currentFio = "";

        // Список ID дисциплин, закреплённых за преподавателем
        private List<int> myPredmetIds = new List<int>();

        // Для диаграммы отчёта
        private List<PredmetReportRow> reportRows = new List<PredmetReportRow>();

        public TeacherMainWindow()
        {
            InitializeComponent();
        }

        //  ЗАГРУЗКА 

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();

                currentUserId = App.CurrentUserId;

                if (currentUserId <= 0)
                {
                    MessageBox.Show(
                        "Не удалось определить ID текущего пользователя. " +
                        "Проверьте авторизацию.",
                        "Ошибка сессии", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Загружаем ФИО преподавателя
                LoadTeacherInfo();

                // Загружаем список закреплённых дисциплин
                LoadMyPredmetIds();

                // Загружаем данные для всех вкладок
                SafeExecute("загрузка дисциплин", LoadMyPredmets);
                SafeExecute("загрузка тестов", LoadMyTests);
                SafeExecute("загрузка фильтров результатов", LoadFilterCombos);
                SafeExecute("загрузка результатов", LoadResults);
                SafeExecute("загрузка отчёта", LoadReport);

                UpdateHeader();
                ShowPanel("Predmets");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SafeExecute(string stepName, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка на этапе «{stepName}»:\n{ex.Message}",
                    "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Загрузка ФИО преподавателя.
        /// </summary>
        private void LoadTeacherInfo()
        {
            string sql = @"
                SELECT Familiya, Imya, Otchestvo
                FROM [UP03_Users]
                WHERE IDUser = @IDUser";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@IDUser", currentUserId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string f = reader["Familiya"]?.ToString() ?? "";
                        string i = reader["Imya"]?.ToString() ?? "";
                        string o = reader["Otchestvo"]?.ToString() ?? "";
                        currentFio = $"{f} {i} {o}".Trim();
                        if (string.IsNullOrEmpty(currentFio))
                            currentFio = App.CurrentUserFio;
                    }
                }
            }
        }

        /// <summary>
        /// Загрузка ID дисциплин, закреплённых за преподавателем
        /// через связь UP03_PrepodPredmet.
        /// </summary>
        private void LoadMyPredmetIds()
        {
            myPredmetIds.Clear();

            string sql = @"
                SELECT IDPredmet
                FROM [UP03_PrepodPredmet]
                WHERE IDPrepodavatelya = @IDTeacher";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@IDTeacher", currentUserId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        myPredmetIds.Add(Convert.ToInt32(reader["IDPredmet"]));
                    }
                }
            }
        }

        /// <summary>
        /// Список дисциплин преподавателя с количеством тестов.
        /// </summary>
        private void LoadMyPredmets()
        {
            if (myPredmetIds.Count == 0)
            {
                dgMyPredmets.ItemsSource = null;
                return;
            }

            // Формируем список параметров @p0, @p1, ... для IN
            string inClause = "@p" + string.Join(", @p", System.Linq.Enumerable.Range(0, myPredmetIds.Count));

            string sql = $@"
                SELECT 
                    p.IDPredmet,
                    p.Nazvanie,
                    p.Opisanie,
                    (SELECT COUNT(*) FROM [UP03_Tests] t WHERE t.IDPredmet = p.IDPredmet) AS TestCount
                FROM [UP03_Predmet] p
                WHERE p.IDPredmet IN ({inClause})
                ORDER BY p.Nazvanie";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                for (int i = 0; i < myPredmetIds.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, myPredmetIds[i]);

                DataTable dt = new DataTable();
                using (SqlDataReader reader = cmd.ExecuteReader())
                    dt.Load(reader);

                dgMyPredmets.ItemsSource = dt.DefaultView;
            }
        }

        /// <summary>
        /// Тесты по дисциплинам преподавателя.
        /// </summary>
        private void LoadMyTests()
        {
            if (myPredmetIds.Count == 0)
            {
                dgMyTests.ItemsSource = null;
                return;
            }

            string inClause = "@p" + string.Join(", @p", System.Linq.Enumerable.Range(0, myPredmetIds.Count));

            string sql = $@"
                SELECT 
                    t.IDTest,
                    t.Nazvanie,
                    t.Opisanie,
                    t.Vremya_vipolneniya,
                    t.IDPredmet,
                    p.Nazvanie AS NazvaniePredmeta
                FROM [UP03_Tests] t
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                WHERE t.IDPredmet IN ({inClause})
                ORDER BY p.Nazvanie, t.Nazvanie";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                for (int i = 0; i < myPredmetIds.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, myPredmetIds[i]);

                DataTable dt = new DataTable();
                using (SqlDataReader reader = cmd.ExecuteReader())
                    dt.Load(reader);

                dgMyTests.ItemsSource = dt.DefaultView;
            }
        }

        /// <summary>
        /// Загрузка ComboBox-фильтров: дисциплины, тесты, группы.
        /// Все списки ограничены дисциплинами преподавателя.
        /// </summary>
        private void LoadFilterCombos()
        {
            if (myPredmetIds.Count == 0) return;

            string inClause = "@p" + string.Join(", @p", System.Linq.Enumerable.Range(0, myPredmetIds.Count));

            // ==== Дисциплины ====
            string predmetSql = $@"
                SELECT IDPredmet, Nazvanie 
                FROM [UP03_Predmet]
                WHERE IDPredmet IN ({inClause})
                ORDER BY Nazvanie";

            DataTable dtPredmets = new DataTable();
            using (SqlCommand cmd = new SqlCommand(predmetSql, connection))
            {
                for (int i = 0; i < myPredmetIds.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, myPredmetIds[i]);

                using (SqlDataReader r = cmd.ExecuteReader())
                    dtPredmets.Load(r);
            }

            DataRow allPredmetsRow = dtPredmets.NewRow();
            allPredmetsRow["IDPredmet"] = 0;
            allPredmetsRow["Nazvanie"] = "Все дисциплины";
            dtPredmets.Rows.InsertAt(allPredmetsRow, 0);

            FilterPredmetComboBox.ItemsSource = dtPredmets.DefaultView;
            FilterPredmetComboBox.SelectedIndex = 0;

            // ==== Тесты (по дисциплинам преподавателя) ====
            string testSql = $@"
                SELECT t.IDTest, t.Nazvanie
                FROM [UP03_Tests] t
                WHERE t.IDPredmet IN ({inClause})
                ORDER BY t.Nazvanie";

            DataTable dtTests = new DataTable();
            using (SqlCommand cmd = new SqlCommand(testSql, connection))
            {
                for (int i = 0; i < myPredmetIds.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, myPredmetIds[i]);

                using (SqlDataReader r = cmd.ExecuteReader())
                    dtTests.Load(r);
            }

            DataRow allTestsRow = dtTests.NewRow();
            allTestsRow["IDTest"] = 0;
            allTestsRow["Nazvanie"] = "Все тесты";
            dtTests.Rows.InsertAt(allTestsRow, 0);

            FilterTestComboBox.ItemsSource = dtTests.DefaultView;
            FilterTestComboBox.SelectedIndex = 0;

            // ==== Группы (все — преподаватель видит группы, где есть его дисциплины) ====
            string groupSql = @"
                SELECT IDGroup, Nazvanie
                FROM [UP03_Groups]
                ORDER BY Kurs, Nazvanie";

            DataTable dtGroups = new DataTable();
            using (SqlCommand cmd = new SqlCommand(groupSql, connection))
            using (SqlDataReader r = cmd.ExecuteReader())
                dtGroups.Load(r);

            DataRow allGroupsRow = dtGroups.NewRow();
            allGroupsRow["IDGroup"] = 0;
            allGroupsRow["Nazvanie"] = "Все группы";
            dtGroups.Rows.InsertAt(allGroupsRow, 0);

            FilterGroupComboBox.ItemsSource = dtGroups.DefaultView;
            FilterGroupComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Загрузка результатов студентов по дисциплинам преподавателя.
        /// </summary>
        private void LoadResults()
        {
            if (myPredmetIds.Count == 0)
            {
                dgResults.ItemsSource = null;
                return;
            }

            int predmetFilter = GetSelectedInt(FilterPredmetComboBox);
            int testFilter = GetSelectedInt(FilterTestComboBox);
            int groupFilter = GetSelectedInt(FilterGroupComboBox);

            string inClause = "@p" + string.Join(", @p", System.Linq.Enumerable.Range(0, myPredmetIds.Count));

            string sql = $@"
                SELECT 
                    u.Familiya + ' ' + u.Imya + ISNULL(' ' + u.Otchestvo, '') AS FIO,
                    ISNULL(g.Nazvanie, '—') AS GroupNazvanie,
                    p.Nazvanie AS PredmetNazvanie,
                    t.Nazvanie AS TestNazvanie,
                    r.Balli,
                    r.Procent,
                    r.Ocenka
                FROM [UP03_Resultat] r
                JOIN [UP03_Users] u ON r.IDUser = u.IDUser
                JOIN [UP03_Tests] t ON r.IDTest = t.IDTest
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                LEFT JOIN [UP03_StudentGroup] sg ON sg.IDStudenta = u.IDUser
                LEFT JOIN [UP03_Groups] g ON g.IDGroup = sg.IDGroup
                WHERE p.IDPredmet IN ({inClause})
                  AND (@PredmetFilter = 0 OR p.IDPredmet = @PredmetFilter)
                  AND (@TestFilter = 0 OR t.IDTest = @TestFilter)
                  AND (@GroupFilter = 0 OR g.IDGroup = @GroupFilter)
                ORDER BY u.Familiya, p.Nazvanie, t.Nazvanie";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                for (int i = 0; i < myPredmetIds.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, myPredmetIds[i]);

                cmd.Parameters.AddWithValue("@PredmetFilter", predmetFilter);
                cmd.Parameters.AddWithValue("@TestFilter", testFilter);
                cmd.Parameters.AddWithValue("@GroupFilter", groupFilter);

                DataTable dt = new DataTable();
                using (SqlDataReader r = cmd.ExecuteReader())
                    dt.Load(r);

                dgResults.ItemsSource = dt.DefaultView;
            }
        }

        /// <summary>
        /// Отчёт: сводные цифры + диаграмма по дисциплинам.
        /// </summary>
        private void LoadReport()
        {
            if (myPredmetIds.Count == 0)
            {
                TotalPassesTextBlock.Text = "0";
                AveragePercentTextBlock.Text = "—";
                PassedCountTextBlock.Text = "0";
                PredmetChartItems.ItemsSource = null;
                return;
            }

            string inClause = "@p" + string.Join(", @p", System.Linq.Enumerable.Range(0, myPredmetIds.Count));

            string sql = $@"
                SELECT 
                    p.IDPredmet,
                    p.Nazvanie,
                    COUNT(r.IDResultat) AS PassesCount,
                    ISNULL(AVG(CAST(r.Procent AS FLOAT)), 0) AS AvgPercent,
                    ISNULL(SUM(CASE WHEN r.Ocenka >= 3 THEN 1 ELSE 0 END), 0) AS PassedCount
                FROM [UP03_Predmet] p
                LEFT JOIN [UP03_Tests] t ON t.IDPredmet = p.IDPredmet
                LEFT JOIN [UP03_Resultat] r ON r.IDTest = t.IDTest
                WHERE p.IDPredmet IN ({inClause})
                GROUP BY p.IDPredmet, p.Nazvanie
                ORDER BY p.Nazvanie";

            DataTable dt = new DataTable();
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                for (int i = 0; i < myPredmetIds.Count; i++)
                    cmd.Parameters.AddWithValue("@p" + i, myPredmetIds[i]);

                using (SqlDataReader r = cmd.ExecuteReader())
                    dt.Load(r);
            }

            int totalPasses = 0;
            int totalPassed = 0;
            double sumPercent = 0;
            int rowsWithPercent = 0;

            reportRows.Clear();

            foreach (DataRow row in dt.Rows)
            {
                int passes = Convert.ToInt32(row["PassesCount"]);
                double avg = Convert.ToDouble(row["AvgPercent"]);
                int passed = Convert.ToInt32(row["PassedCount"]);

                totalPasses += passes;
                totalPassed += passed;

                if (avg > 0)
                {
                    sumPercent += avg;
                    rowsWithPercent++;
                }

                reportRows.Add(new PredmetReportRow
                {
                    Nazvanie = row["Nazvanie"].ToString(),
                    AvgPercent = avg
                });
            }

            TotalPassesTextBlock.Text = totalPasses.ToString();
            PassedCountTextBlock.Text = totalPassed.ToString();
            AveragePercentTextBlock.Text = rowsWithPercent > 0
                ? $"{sumPercent / rowsWithPercent:F1}%"
                : "—";

            BuildPredmetChart();
        }

        private void BuildPredmetChart()
        {
            const double maxBarWidth = 320.0;

            foreach (var item in reportRows)
            {
                item.BarWidth = item.AvgPercent / 100.0 * maxBarWidth;
                if (item.BarWidth < 2) item.BarWidth = 2;
            }

            PredmetChartItems.ItemsSource = null;
            PredmetChartItems.ItemsSource = reportRows;
        }

        //  НАВИГАЦИЯ 

        private void PredmetsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMyPredmets();
            ShowPanel("Predmets");
        }

        private void TestsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMyTests();
            ShowPanel("Tests");
        }

        private void ResultsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadResults();
            ShowPanel("Results");
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadReport();
            ShowPanel("Reports");
        }

        private void ShowPanel(string panel)
        {
            PredmetsPanel.Visibility = Visibility.Collapsed;
            TestsPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Collapsed;
            ReportsPanel.Visibility = Visibility.Collapsed;

            switch (panel)
            {
                case "Predmets":
                    PredmetsPanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Мои дисциплины";
                    PageSubtitleTextBlock.Text = "Дисциплины, закреплённые за вами";
                    break;
                case "Tests":
                    TestsPanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Мои тесты";
                    PageSubtitleTextBlock.Text = "Тесты по вашим дисциплинам";
                    break;
                case "Results":
                    ResultsPanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Результаты студентов";
                    PageSubtitleTextBlock.Text = "Результаты по вашим дисциплинам и группам";
                    break;
                case "Reports":
                    ReportsPanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Отчёты";
                    PageSubtitleTextBlock.Text = "Сводная статистика и диаграммы";
                    break;
            }
        }

        private void UpdateHeader()
        {
            HeaderUserNameTextBlock.Text = currentFio;
            HeaderPredmetsCountTextBlock.Text = myPredmetIds.Count == 0
                ? "Дисциплины не назначены"
                : $"Дисциплин: {myPredmetIds.Count}";
            SidebarUserNameTextBlock.Text = currentFio;
        }

        //  ФИЛЬТРЫ 

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadResults();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            FilterPredmetComboBox.SelectedIndex = 0;
            FilterTestComboBox.SelectedIndex = 0;
            FilterGroupComboBox.SelectedIndex = 0;
            LoadResults();
        }

        private int GetSelectedInt(ComboBox cb)
        {
            if (cb?.SelectedValue == null) return 0;
            int.TryParse(cb.SelectedValue.ToString(), out int result);
            return result;
        }

        //  ПЕРЕХОД К РЕЗУЛЬТАТАМ ТЕСТА 

        private void ViewTestResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (dgMyTests.SelectedItem == null)
            {
                MessageBox.Show("Выберите тест в таблице!",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                DataRowView row = dgMyTests.SelectedItem as DataRowView;
                if (row == null) return;

                int idTest = Convert.ToInt32(row["IDTest"]);

                // Переключаемся на вкладку «Результаты», предварительно выставив фильтр по тесту
                ShowPanel("Results");

                // Найти этот тест в FilterTestComboBox
                foreach (DataRowView item in FilterTestComboBox.Items)
                {
                    if (Convert.ToInt32(item["IDTest"]) == idTest)
                    {
                        FilterTestComboBox.SelectedItem = item;
                        break;
                    }
                }

                LoadResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void dgMyTests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        //  ЭКСПОРТ И ПЕЧАТЬ 

        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            PrintReport();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintReport();
        }

        private void PrintReport()
        {
            try
            {
                FlowDocument doc = BuildReportDocument();
                var printDialog = new PrintDialog();

                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintDocument(
                        ((IDocumentPaginatorSource)doc).DocumentPaginator,
                        "Отчёт преподавателя");

                    MessageBox.Show(
                        "Документ отправлен на печать.\n\n" +
                        "Чтобы сохранить в PDF — выберите принтер «Microsoft Print to PDF».",
                        "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка печати: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument BuildReportDocument()
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 12;
            doc.PagePadding = new Thickness(40);

            // Заголовок
            doc.Blocks.Add(new Paragraph(new Run("Отчёт преподавателя"))
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#1A365D"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            // ФИО и дата
            doc.Blocks.Add(new Paragraph(new Run(
                $"{currentFio}\nДата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}"))
            {
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Сводные цифры
            doc.Blocks.Add(new Paragraph(new Run("Сводные показатели"))
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            doc.Blocks.Add(new Paragraph(new Run(
                $"• Всего прохождений тестов: {TotalPassesTextBlock.Text}\n" +
                $"• Средний процент: {AveragePercentTextBlock.Text}\n" +
                $"• Успешно сдано: {PassedCountTextBlock.Text}"))
            {
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Таблица по дисциплинам
            doc.Blocks.Add(new Paragraph(new Run("Статистика по дисциплинам"))
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            Table table = new Table();
            table.CellSpacing = 0;
            table.BorderBrush = Brushes.LightGray;
            table.BorderThickness = new Thickness(1);

            table.Columns.Add(new TableColumn());
            table.Columns.Add(new TableColumn());

            TableRowGroup rg = new TableRowGroup();
            table.RowGroups.Add(rg);

            // Заголовок таблицы
            TableRow headerRow = new TableRow();
            headerRow.Background = (Brush)new BrushConverter().ConvertFrom("#1A365D");

            var headerCell1 = new TableCell(new Paragraph(new Run("Дисциплина"))
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0)
            })
            {
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 1, 1)
            };

            var headerCell2 = new TableCell(new Paragraph(new Run("Средний процент"))
            {
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0)
            })
            {
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 1, 1)
            };

            headerRow.Cells.Add(headerCell1);
            headerRow.Cells.Add(headerCell2);
            rg.Rows.Add(headerRow);

            // Данные
            foreach (var item in reportRows)
            {
                TableRow tr = new TableRow();

                var c1 = new TableCell(new Paragraph(new Run(item.Nazvanie))
                {
                    Margin = new Thickness(0)
                })
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };

                var c2 = new TableCell(new Paragraph(new Run($"{item.AvgPercent:F1}%"))
                {
                    Margin = new Thickness(0)
                })
                {
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };

                tr.Cells.Add(c1);
                tr.Cells.Add(c2);
                rg.Rows.Add(tr);
            }

            doc.Blocks.Add(table);
            return doc;
        }

        //  ПРОЧЕЕ 

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow loginWindow = new MainWindow();
            loginWindow.Show();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (connection != null && connection.State == ConnectionState.Open)
            {
                connection.Close();
                connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Строка отчёта: дисциплина и её средний процент.
    /// </summary>
    public class PredmetReportRow
    {
        public string Nazvanie { get; set; }
        public double AvgPercent { get; set; }
        public double BarWidth { get; set; }
    }
}