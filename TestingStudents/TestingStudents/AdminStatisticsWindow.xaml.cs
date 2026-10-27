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
    /// Окно статистики и отчётов для администратора.
    /// Три вкладки: по дисциплинам, по группам, по студентам.
    /// Экспорт в PDF / печать текущей вкладки.
    /// </summary>
    public partial class AdminStatisticsWindow : Window
    {
        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        private SqlConnection connection;
        private string currentTab = "Predmet"; // Predmet | Group | Student

        // Данные для диаграмм
        private List<PredmetStatRow> predmetStats = new List<PredmetStatRow>();
        private List<GroupStatRow> groupStats = new List<GroupStatRow>();

        public AdminStatisticsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();

                LoadFilterCombos();

                // Загружаем все три вкладки, чтобы фильтры и данные были готовы
                LoadPredmetStatistics();
                LoadGroupStatistics();
                LoadStudentStatistics();

                ShowTab("Predmet");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ================== ЗАГРУЗКА ФИЛЬТРОВ ==================

        private void LoadFilterCombos()
        {
            // Группы
            using (SqlCommand cmd = new SqlCommand(
                "SELECT IDGroup, Nazvanie FROM [UP03_Groups] ORDER BY Kurs, Nazvanie", connection))
            {
                DataTable dt = new DataTable();
                using (SqlDataReader r = cmd.ExecuteReader()) dt.Load(r);

                DataRow allRow = dt.NewRow();
                allRow["IDGroup"] = 0;
                allRow["Nazvanie"] = "Все группы";
                dt.Rows.InsertAt(allRow, 0);

                FilterGroupComboBox.ItemsSource = dt.DefaultView;
                FilterGroupComboBox.SelectedIndex = 0;
            }

            // Дисциплины
            using (SqlCommand cmd = new SqlCommand(
                "SELECT IDPredmet, Nazvanie FROM [UP03_Predmet] ORDER BY Nazvanie", connection))
            {
                DataTable dt = new DataTable();
                using (SqlDataReader r = cmd.ExecuteReader()) dt.Load(r);

                DataRow allRow = dt.NewRow();
                allRow["IDPredmet"] = 0;
                allRow["Nazvanie"] = "Все дисциплины";
                dt.Rows.InsertAt(allRow, 0);

                FilterPredmetComboBox.ItemsSource = dt.DefaultView;
                FilterPredmetComboBox.SelectedIndex = 0;
            }
        }

        // ================== ВКЛАДКА: ПО ДИСЦИПЛИНАМ ==================

        private void LoadPredmetStatistics()
        {
            string sql = @"
                SELECT 
                    p.IDPredmet,
                    p.Nazvanie,
                    (SELECT COUNT(*) FROM [UP03_Tests] t WHERE t.IDPredmet = p.IDPredmet) AS TestCount,
                    ISNULL((
                        SELECT COUNT(*) 
                        FROM [UP03_Resultat] r
                        JOIN [UP03_Tests] t ON r.IDTest = t.IDTest
                        WHERE t.IDPredmet = p.IDPredmet
                    ), 0) AS AttemptsCount,
                    ISNULL((
                        SELECT AVG(CAST(r.Procent AS FLOAT))
                        FROM [UP03_Resultat] r
                        JOIN [UP03_Tests] t ON r.IDTest = t.IDTest
                        WHERE t.IDPredmet = p.IDPredmet
                    ), 0) AS AvgPercent,
                    ISNULL((
                        SELECT COUNT(*)
                        FROM [UP03_Resultat] r
                        JOIN [UP03_Tests] t ON r.IDTest = t.IDTest
                        WHERE t.IDPredmet = p.IDPredmet AND r.Ocenka >= 3
                    ), 0) AS PassedCount
                FROM [UP03_Predmet] p
                ORDER BY p.Nazvanie";

            DataTable dt = new DataTable();
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }

            dgPredmets.ItemsSource = dt.DefaultView;

            // Сводные показатели
            int totalPredmets = dt.Rows.Count;
            int totalTests = 0;
            double sumPercent = 0;
            int rowsWithPercent = 0;

            predmetStats.Clear();
            foreach (DataRow row in dt.Rows)
            {
                totalTests += Convert.ToInt32(row["TestCount"]);

                double avg = row["AvgPercent"] != DBNull.Value
                    ? Convert.ToDouble(row["AvgPercent"]) : 0;

                if (avg > 0)
                {
                    sumPercent += avg;
                    rowsWithPercent++;
                }

                predmetStats.Add(new PredmetStatRow
                {
                    Nazvanie = row["Nazvanie"].ToString(),
                    AvgPercent = avg
                });
            }

            TotalPredmetsTextBlock.Text = totalPredmets.ToString();
            TotalTestsTextBlock.Text = totalTests.ToString();
            OverallAvgTextBlock.Text = rowsWithPercent > 0
                ? $"{sumPercent / rowsWithPercent:F1}%"
                : "—";

            // Диаграмма: ширина пропорциональна проценту
            BuildChartForPredmets();
        }

        private void BuildChartForPredmets()
        {
            const double maxBarWidth = 220.0; // пикселей при 100%

            foreach (var item in predmetStats)
            {
                item.BarWidth = item.AvgPercent / 100.0 * maxBarWidth;
                if (item.BarWidth < 2) item.BarWidth = 2; // минимальная видимая ширина
            }

            PredmetChartItems.ItemsSource = null;
            PredmetChartItems.ItemsSource = predmetStats;
        }

        // ================== ВКЛАДКА: ПО ГРУППАМ ==================

        private void LoadGroupStatistics()
        {
            string sql = @"
                SELECT 
                    g.IDGroup,
                    g.Nazvanie,
                    g.Kurs,
                    (SELECT COUNT(*) FROM [UP03_StudentGroup] sg 
                     WHERE sg.IDGroup = g.IDGroup) AS StudentCount,
                    ISNULL((
                        SELECT COUNT(DISTINCT r.IDUser)
                        FROM [UP03_Resultat] r
                        JOIN [UP03_StudentGroup] sg ON sg.IDStudenta = r.IDUser
                        WHERE sg.IDGroup = g.IDGroup
                    ), 0) AS CompletedCount,
                    ISNULL((
                        SELECT AVG(CAST(r.Balli AS FLOAT))
                        FROM [UP03_Resultat] r
                        JOIN [UP03_StudentGroup] sg ON sg.IDStudenta = r.IDUser
                        WHERE sg.IDGroup = g.IDGroup
                    ), 0) AS AvgBalli,
                    ISNULL((
                        SELECT COUNT(*)
                        FROM [UP03_Resultat] r
                        JOIN [UP03_StudentGroup] sg ON sg.IDStudenta = r.IDUser
                        WHERE sg.IDGroup = g.IDGroup AND r.Ocenka >= 3
                    ), 0) AS PassedCount
                FROM [UP03_Groups] g
                ORDER BY g.Kurs, g.Nazvanie";

            DataTable dt = new DataTable();
            using (SqlCommand cmd = new SqlCommand(sql, connection))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }

            dgGroupsStats.ItemsSource = dt.DefaultView;

            int totalGroups = dt.Rows.Count;
            int totalStudents = 0;
            double sumBalli = 0;
            int rowsWithBalli = 0;

            groupStats.Clear();
            foreach (DataRow row in dt.Rows)
            {
                totalStudents += Convert.ToInt32(row["StudentCount"]);

                double avg = row["AvgBalli"] != DBNull.Value
                    ? Convert.ToDouble(row["AvgBalli"]) : 0;

                if (avg > 0)
                {
                    sumBalli += avg;
                    rowsWithBalli++;
                }

                groupStats.Add(new GroupStatRow
                {
                    Nazvanie = row["Nazvanie"].ToString(),
                    AvgBalli = avg
                });
            }

            TotalGroupsTextBlock.Text = totalGroups.ToString();
            TotalStudentsTextBlock.Text = totalStudents.ToString();
            OverallAvgBalliTextBlock.Text = rowsWithBalli > 0
                ? $"{sumBalli / rowsWithBalli:F1}"
                : "—";

            BuildChartForGroups();
        }

        private void BuildChartForGroups()
        {
            const double maxBalli = 5.0;
            const double maxBarWidth = 220.0;

            foreach (var item in groupStats)
            {
                item.BarWidth = item.AvgBalli / maxBalli * maxBarWidth;
                if (item.BarWidth < 2) item.BarWidth = 2;
            }

            GroupChartItems.ItemsSource = null;
            GroupChartItems.ItemsSource = groupStats;
        }

        // ================== ВКЛАДКА: ПО СТУДЕНТАМ ==================

        private void LoadStudentStatistics()
        {
            int groupFilter = 0;
            int predmetFilter = 0;

            if (FilterGroupComboBox.SelectedValue != null)
                int.TryParse(FilterGroupComboBox.SelectedValue.ToString(), out groupFilter);

            if (FilterPredmetComboBox.SelectedValue != null)
                int.TryParse(FilterPredmetComboBox.SelectedValue.ToString(), out predmetFilter);

            string sql = @"
                SELECT 
                    u.Familiya + ' ' + u.Imya + 
                        ISNULL(' ' + u.Otchestvo, '') AS FIO,
                    g.Nazvanie AS GroupNazvanie,
                    p.Nazvanie AS PredmetNazvanie,
                    t.Nazvanie AS TestNazvanie,
                    r.Balli,
                    r.Procent,
                    r.Ocenka,
                    r.DataProhozhdeniya
                FROM [UP03_Resultat] r
                JOIN [UP03_Users] u ON r.IDUser = u.IDUser
                JOIN [UP03_Tests] t ON r.IDTest = t.IDTest
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                LEFT JOIN [UP03_StudentGroup] sg ON sg.IDStudenta = u.IDUser
                LEFT JOIN [UP03_Groups] g ON g.IDGroup = sg.IDGroup
                WHERE (@GroupFilter = 0 OR g.IDGroup = @GroupFilter)
                  AND (@PredmetFilter = 0 OR p.IDPredmet = @PredmetFilter)
                ORDER BY u.Familiya, p.Nazvanie, t.Nazvanie";

            SqlDataAdapter adapter = new SqlDataAdapter(sql, connection);
            adapter.SelectCommand.Parameters.AddWithValue("@GroupFilter", groupFilter);
            adapter.SelectCommand.Parameters.AddWithValue("@PredmetFilter", predmetFilter);

            DataTable dt = new DataTable();
            adapter.Fill(dt);
            dgStudentsStats.ItemsSource = dt.DefaultView;
        }

        // ================== ФИЛЬТРЫ ==================

        private void FilterGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadStudentStatistics();
        }

        private void FilterPredmetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadStudentStatistics();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            FilterGroupComboBox.SelectedIndex = 0;
            FilterPredmetComboBox.SelectedIndex = 0;
            LoadStudentStatistics();
        }

        // ================== НАВИГАЦИЯ ==================

        private void ByPredmetTabButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPredmetStatistics();
            ShowTab("Predmet");
        }

        private void ByGroupTabButton_Click(object sender, RoutedEventArgs e)
        {
            LoadGroupStatistics();
            ShowTab("Group");
        }

        private void ByStudentTabButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStudentStatistics();
            ShowTab("Student");
        }

        private void ShowTab(string tab)
        {
            currentTab = tab;

            ByPredmetPanel.Visibility = Visibility.Collapsed;
            ByGroupPanel.Visibility = Visibility.Collapsed;
            ByStudentPanel.Visibility = Visibility.Collapsed;

            switch (tab)
            {
                case "Predmet":
                    ByPredmetPanel.Visibility = Visibility.Visible;
                    break;
                case "Group":
                    ByGroupPanel.Visibility = Visibility.Visible;
                    break;
                case "Student":
                    ByStudentPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        // ================== ЭКСПОРТ И ПЕЧАТЬ ==================

        /// <summary>
        /// Печать / экспорт в PDF через PrintDialog.
        /// Пользователь в диалоге выбирает «Microsoft Print to PDF» — 
        /// в результате создаётся PDF-файл.
        /// </summary>
        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            PrintCurrentTab();
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintCurrentTab();
        }

        private void PrintCurrentTab()
        {
            try
            {
                // Формируем документ на основе текущей вкладки
                FlowDocument doc = BuildCurrentFlowDocument();

                PrintDialog printDialog = new PrintDialog();

                // Настраиваем размер страницы
                doc.PageWidth = printDialog.PrintableAreaWidth;
                doc.PageHeight = printDialog.PrintableAreaHeight;
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                if (printDialog.ShowDialog() == true)
                {
                    IDocumentPaginatorSource idpSource = doc;
                    printDialog.PrintDocument(idpSource.DocumentPaginator, "Отчёт по статистике");

                    MessageBox.Show(
                        "Документ отправлен на печать.\n\n" +
                        "Чтобы сохранить в PDF — выберите принтер «Microsoft Print to PDF».",
                        "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlowDocument BuildCurrentFlowDocument()
        {
            FlowDocument doc = new FlowDocument();
            doc.FontFamily = new FontFamily("Segoe UI");
            doc.FontSize = 12;
            doc.PagePadding = new Thickness(40);

            // Заголовок
            Paragraph title = new Paragraph(new Run("Отчёт по статистике"))
            {
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#1A365D"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            doc.Blocks.Add(title);

            // Подзаголовок
            Paragraph subtitle = new Paragraph(new Run(
                $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}"))
            {
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 16)
            };
            doc.Blocks.Add(subtitle);

            // Название вкладки
            string tabTitle;
            switch (currentTab)
            {
                case "Predmet": tabTitle = "Раздел: по дисциплинам"; break;
                case "Group": tabTitle = "Раздел: по группам"; break;
                default: tabTitle = "Раздел: по студентам"; break;
            }

            Paragraph tabHeader = new Paragraph(new Run(tabTitle))
            {
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            doc.Blocks.Add(tabHeader);

            // Таблицу строим из текущего DataGrid
            DataGrid sourceGrid = null;
            switch (currentTab)
            {
                case "Predmet": sourceGrid = dgPredmets; break;
                case "Group": sourceGrid = dgGroupsStats; break;
                default: sourceGrid = dgStudentsStats; break;
            }

            if (sourceGrid != null && sourceGrid.ItemsSource is DataView dataView)
            {
                doc.Blocks.Add(BuildTableFromDataView(dataView));
            }
            else
            {
                doc.Blocks.Add(new Paragraph(new Run("Нет данных для отчёта.")));
            }

            return doc;
        }

        /// <summary>
        /// Строит WPF Table на основе DataView
        /// </summary>
        private Table BuildTableFromDataView(DataView dataView)
        {
            Table table = new Table();
            table.CellSpacing = 0;
            table.BorderBrush = Brushes.LightGray;
            table.BorderThickness = new Thickness(1);

            if (dataView == null || dataView.Table == null)
                return table;

            DataTable dt = dataView.Table;
            if (dt.Rows.Count == 0)
                return table;

            int colCount = dt.Columns.Count;
            for (int i = 0; i < colCount; i++)
            {
                TableColumn tc = new TableColumn();
                table.Columns.Add(tc);
            }

            TableRowGroup rowGroup = new TableRowGroup();
            table.RowGroups.Add(rowGroup);

            TableRow headerRow = new TableRow();
            headerRow.Background = (Brush)new BrushConverter().ConvertFrom("#1A365D");

            for (int i = 0; i < colCount; i++)
            {
                string headerText = dt.Columns[i].ColumnName;

                Paragraph p = new Paragraph(new Run(headerText));
                p.FontWeight = FontWeights.SemiBold;
                p.Foreground = Brushes.White;
                p.Margin = new Thickness(0);

                TableCell cell = new TableCell(p);
                cell.Padding = new Thickness(8, 6, 8, 6);
                cell.BorderBrush = Brushes.LightGray;
                cell.BorderThickness = new Thickness(0, 0, 1, 1);

                headerRow.Cells.Add(cell);
            }
            rowGroup.Rows.Add(headerRow);

            bool alt = false;
            foreach (DataRowView rowView in dataView)
            {
                TableRow tr = new TableRow();
                if (alt)
                {
                    tr.Background = (Brush)new BrushConverter().ConvertFrom("#F8F9FA");
                }
                alt = !alt;

                for (int i = 0; i < colCount; i++)
                {
                    object value = rowView[i];
                    string text = (value == null || value == DBNull.Value)
                        ? ""
                        : value.ToString();

                    Paragraph p = new Paragraph(new Run(text));
                    p.Margin = new Thickness(0);

                    TableCell cell = new TableCell(p);
                    cell.Padding = new Thickness(8, 4, 8, 4);
                    cell.BorderBrush = Brushes.LightGray;
                    cell.BorderThickness = new Thickness(0, 0, 1, 1);

                    tr.Cells.Add(cell);
                }
                rowGroup.Rows.Add(tr);
            }

            return table;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            AdminMainWindow adminWindow = new AdminMainWindow();
            adminWindow.Show();
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

    // ================== МОДЕЛИ ДЛЯ ДИАГРАММ ==================

    public class PredmetStatRow
    {
        public string Nazvanie { get; set; }
        public double AvgPercent { get; set; }
        public double BarWidth { get; set; }
    }

    public class GroupStatRow
    {
        public string Nazvanie { get; set; }
        public double AvgBalli { get; set; }
        public double BarWidth { get; set; }
    }
}