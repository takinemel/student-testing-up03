using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TestingStudents
{
    /// <summary>
    /// Личный кабинет студента.
    /// Отображает ФИО, группу, доступные тесты и результаты.
    /// </summary>
    public partial class StudentMainWindow : Window
    {
        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        private SqlConnection connection;
        private int currentUserId;
        private string currentFamiliya = "";
        private string currentImya = "";
        private string currentOtchestvo = "";
        private string currentLogin = "";
        private DateTime? currentDataSozdaniya;
        private int? currentGroupId;
        private string currentGroupNazvanie = "—";
        private int? currentKurs;
        private string currentOtdelenie = "—";

        public StudentMainWindow()
        {
            InitializeComponent();
        }

        // ЗАГРУЗКА 

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();

                currentUserId = App.CurrentUserId;
                LoadStudentProfile();
                LoadMyStatistics();
                LoadAvailableTests();
                LoadMyResults();
                UpdateHeaderAndSidebar();
                ShowPanel("Profile");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Загрузка личных данных студента и информации о его группе.
        /// Связь: Users → StudentGroup → Groups (M:N)
        /// </summary>
        private void LoadStudentProfile()
        {
            string userSql = @"
        SELECT Familiya, Imya, Otchestvo, Login, DataSozdaniya
        FROM [UP03_Users]
        WHERE IDUser = @IDUser";

            using (SqlCommand cmd = new SqlCommand(userSql, connection))
            {
                cmd.Parameters.AddWithValue("@IDUser", currentUserId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentFamiliya = reader["Familiya"]?.ToString() ?? "";
                        currentImya = reader["Imya"]?.ToString() ?? "";
                        currentOtchestvo = reader["Otchestvo"]?.ToString() ?? "";
                        currentLogin = reader["Login"]?.ToString() ?? "";

                        if (reader["DataSozdaniya"] != DBNull.Value)
                            currentDataSozdaniya = Convert.ToDateTime(reader["DataSozdaniya"]);
                    }
                }
            }
            string groupSql = @"
        SELECT TOP 1 
            g.IDGroup, 
            g.Nazvanie, 
            g.Kurs, 
            o.Otdelenie
        FROM [UP03_StudentGroup] sg
        JOIN [UP03_Groups] g ON sg.IDGroup = g.IDGroup
        LEFT JOIN [UP03_Otdelenie] o ON g.IDOtdelenie = o.IDOtdelenie
        WHERE sg.IDStudenta = @IDUser
        ORDER BY g.Kurs DESC, g.Nazvanie";

            currentGroupId = null;
            currentGroupNazvanie = "—";
            currentKurs = null;
            currentOtdelenie = "—";

            using (SqlCommand cmd = new SqlCommand(groupSql, connection))
            {
                cmd.Parameters.AddWithValue("@IDUser", currentUserId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentGroupId = Convert.ToInt32(reader["IDGroup"]);
                        currentGroupNazvanie = reader["Nazvanie"]?.ToString() ?? "—";

                        currentKurs = reader["Kurs"] != DBNull.Value
                            ? Convert.ToInt32(reader["Kurs"])
                            : (int?)null;

                        currentOtdelenie = reader["Otdelenie"] != DBNull.Value
                            ? reader["Otdelenie"].ToString()
                            : "—";
                    }
                }
            }

            FamiliyaValueTextBlock.Text = string.IsNullOrEmpty(currentFamiliya) ? "—" : currentFamiliya;
            ImyaValueTextBlock.Text = string.IsNullOrEmpty(currentImya) ? "—" : currentImya;
            OtchestvoValueTextBlock.Text = string.IsNullOrEmpty(currentOtchestvo) ? "—" : currentOtchestvo;
            LoginValueTextBlock.Text = string.IsNullOrEmpty(currentLogin) ? "—" : currentLogin;

            GroupValueTextBlock.Text = currentGroupNazvanie;
            KursValueTextBlock.Text = currentKurs.HasValue ? $"{currentKurs} курс" : "—";
            OtdelenieValueTextBlock.Text = currentOtdelenie;
            DataSozdaniyaValueTextBlock.Text = currentDataSozdaniya.HasValue
                ? currentDataSozdaniya.Value.ToString("dd.MM.yyyy")
                : "—";

            // Инициалы для аватара
            string initials = "";
            if (!string.IsNullOrEmpty(currentFamiliya)) initials += currentFamiliya[0];
            if (!string.IsNullOrEmpty(currentImya)) initials += currentImya[0];
            AvatarInitialsTextBlock.Text = string.IsNullOrEmpty(initials) ? "?" : initials.ToUpper();
        }

        /// <summary>
        /// Сводная статистика студента по всем тестам
        /// </summary>
        private void LoadMyStatistics()
        {
            string sql = @"
                SELECT 
                    COUNT(*) AS CompletedCount,
                    AVG(CAST(r.Procent AS FLOAT)) AS AvgPercent,
                    SUM(CASE WHEN r.Ocenka >= 3 THEN 1 ELSE 0 END) AS PositiveCount
                FROM [UP03_Resultat] r
                WHERE r.IDUser = @IDUser";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@IDUser", currentUserId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int completed = reader["CompletedCount"] != DBNull.Value
                            ? Convert.ToInt32(reader["CompletedCount"]) : 0;
                        double avg = reader["AvgPercent"] != DBNull.Value
                            ? Convert.ToDouble(reader["AvgPercent"]) : 0;
                        int positive = reader["PositiveCount"] != DBNull.Value
                            ? Convert.ToInt32(reader["PositiveCount"]) : 0;

                        CompletedTestsCountTextBlock.Text = completed.ToString();
                        AveragePercentTextBlock.Text = completed > 0
                            ? $"{avg:F1}%"
                            : "—";
                        PositiveGradesCountTextBlock.Text = positive.ToString();
                    }
                }
            }
        }
        private void LoadAvailableTests()
        {
            string sql = @"
                SELECT 
                    t.IDTest,
                    t.Nazvanie,
                    t.Vremya_vipolneniya,
                    p.Nazvanie AS NazvaniePredmeta,
                    CASE 
                        WHEN EXISTS (
                            SELECT 1 FROM [UP03_Resultat] r
                            WHERE r.IDUser = @IDUser AND r.IDTest = t.IDTest
                        ) THEN 'Пройден'
                        ELSE 'Доступен'
                    END AS StatusText
                FROM [UP03_Tests] t
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                ORDER BY t.IDTest";

            SqlDataAdapter adapter = new SqlDataAdapter(sql, connection);
            adapter.SelectCommand.Parameters.AddWithValue("@IDUser", currentUserId);

            DataTable dt = new DataTable();
            adapter.Fill(dt);
            dgAvailableTests.ItemsSource = dt.DefaultView;
        }

        /// <summary>
        /// Результаты студента по всем пройденным тестам
        /// </summary>
        private void LoadMyResults()
        {
            string sql = @"
                SELECT 
                    r.IDResultat,
                    t.Nazvanie AS NazvanieTesta,
                    p.Nazvanie AS NazvaniePredmeta,
                    r.Balli,
                    r.Procent,
                    r.Ocenka
                FROM [UP03_Resultat] r
                JOIN [UP03_Tests] t ON r.IDTest = t.IDTest
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                WHERE r.IDUser = @IDUser
                ORDER BY r.IDResultat DESC";

            SqlDataAdapter adapter = new SqlDataAdapter(sql, connection);
            adapter.SelectCommand.Parameters.AddWithValue("@IDUser", currentUserId);

            DataTable dt = new DataTable();
            adapter.Fill(dt);
            dgMyResults.ItemsSource = dt.DefaultView;
        }

        /// <summary>
        /// Статистика по группе (без имён — только агрегаты)
        /// </summary>
        private void LoadGroupStatistics()
        {
            if (!currentGroupId.HasValue)
            {
                GroupCompletedTextBlock.Text = "—";
                GroupAverageTextBlock.Text = "—";
                GroupPassedTextBlock.Text = "—";
                return;
            }

            string sql = @"
                SELECT 
                    COUNT(DISTINCT r.IDUser) AS CompletedCount,
                    AVG(CAST(r.Balli AS FLOAT)) AS AvgBalli,
                    SUM(CASE WHEN r.Ocenka >= 3 THEN 1 ELSE 0 END) AS PassedCount
                FROM [UP03_Resultat] r
                JOIN [UP03_StudentGroup] sg ON sg.IDStudenta = r.IDUser
                WHERE sg.IDGroup = @IDGroup";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@IDGroup", currentGroupId.Value);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int completed = reader["CompletedCount"] != DBNull.Value
                            ? Convert.ToInt32(reader["CompletedCount"]) : 0;
                        double avg = reader["AvgBalli"] != DBNull.Value
                            ? Convert.ToDouble(reader["AvgBalli"]) : 0;
                        int passed = reader["PassedCount"] != DBNull.Value
                            ? Convert.ToInt32(reader["PassedCount"]) : 0;

                        GroupCompletedTextBlock.Text = completed.ToString();
                        GroupAverageTextBlock.Text = completed > 0 ? $"{avg:F1}" : "—";
                        GroupPassedTextBlock.Text = passed.ToString();
                    }
                }
            }
        }

        /// <summary>
        /// Обновление шапки и сайдбара
        /// </summary>
        private void UpdateHeaderAndSidebar()
        {
            string fio = $"{currentFamiliya} {currentImya}".Trim();
            if (string.IsNullOrEmpty(fio)) fio = currentLogin;

            HeaderUserNameTextBlock.Text = fio;
            HeaderGroupTextBlock.Text = currentGroupNazvanie == "—"
                ? "Группа не назначена"
                : $"Группа: {currentGroupNazvanie}";

            SidebarUserNameTextBlock.Text = fio;
        }

        //  НАВИГАЦИЯ 

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("Profile");
        }

        private void TestsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableTests(); 
            ShowPanel("Tests");
        }

        private void ResultsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMyResults();
            LoadGroupStatistics();
            ShowPanel("Results");
        }

        /// <summary>
        /// Переключение видимых панелей
        /// </summary>
        private void ShowPanel(string panelName)
        {
            ProfilePanel.Visibility = Visibility.Collapsed;
            TestsPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Collapsed;

            switch (panelName)
            {
                case "Profile":
                    ProfilePanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Профиль";
                    PageSubtitleTextBlock.Text = "Личные данные и информация об обучении";
                    break;
                case "Tests":
                    TestsPanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Доступные тесты";
                    PageSubtitleTextBlock.Text = "Тесты, назначенные вашей группе";
                    break;
                case "Results":
                    ResultsPanel.Visibility = Visibility.Visible;
                    PageTitleTextBlock.Text = "Мои результаты";
                    PageSubtitleTextBlock.Text = "Пройденные тесты и статистика по группе";
                    break;
            }
        }

        // ЗАПУСК ТЕСТА 

        private void dgAvailableTests_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            StartSelectedTest();
        }

        private void StartTestButton_Click(object sender, RoutedEventArgs e)
        {
            StartSelectedTest();
        }

        /// <summary>
        /// Открывает окно прохождения теста для выбранной строки
        /// </summary>
        private void StartSelectedTest()
        {
            if (dgAvailableTests.SelectedItem == null)
            {
                MessageBox.Show("Выберите тест из списка!", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                DataRowView row = dgAvailableTests.SelectedItem as DataRowView;
                if (row == null) return;

                int idTest = Convert.ToInt32(row["IDTest"]);
                string status = row["StatusText"]?.ToString() ?? "";

                if (status == "Пройден")
                {
                    MessageBoxResult result = MessageBox.Show(
                        "Вы уже проходили этот тест. Пройти повторно?",
                        "Повторное прохождение",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                TestPassingWindow testWindow = new TestPassingWindow(idTest, currentUserId);
                testWindow.Owner = this;
                testWindow.ShowDialog();

                LoadAvailableTests();
                LoadMyResults();
                LoadMyStatistics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска теста: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
}