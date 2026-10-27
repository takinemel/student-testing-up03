using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace TestingStudents
{
    /// <summary>
    /// Логика взаимодействия для AdminTestsWindow.xaml
    /// Управление тестами (таблицы UP03_Tests, UP03_Predmet, UP03_Voprosi, UP03_Otvet)
    /// </summary>
    public partial class AdminTestsWindow : Window
    {
        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        private SqlConnection connection;
        private DataTable predmetsTable;

        public AdminTestsWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Загрузка окна: подключение к БД, загрузка дисциплин и тестов
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();

                LoadPredmets();
                LoadTests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к базе данных: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Загрузка дисциплин в ComboBox
        /// </summary>
        private void LoadPredmets()
        {
            string sql = "SELECT IDPredmet, Nazvanie FROM [UP03_Predmet] ORDER BY Nazvanie";
            SqlDataAdapter adapter = new SqlDataAdapter(sql, connection);
            predmetsTable = new DataTable();
            adapter.Fill(predmetsTable);
            PredmetComboBox.ItemsSource = predmetsTable.DefaultView;
        }

        /// <summary>
        /// Загрузка тестов через SqlDataReader.
        /// Не используется SqlDataAdapter.Update() — вся запись идёт
        /// через явные SqlCommand в методах Add/Change/Delete.
        /// </summary>
        private void LoadTests()
        {
            string sql = @"
                SELECT t.IDTest, t.Nazvanie, t.Opisanie, 
                       t.Vremya_vipolneniya, t.IDPredmet, 
                       p.Nazvanie AS NazvaniePredmeta
                FROM [UP03_Tests] t
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                ORDER BY t.IDTest";

            DataTable dt = new DataTable();

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }

            dgTests.ItemsSource = dt.DefaultView;
        }

        /// <summary>
        /// Выбор строки: заполнение полей формы
        /// </summary>
        private void dgTests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTests.SelectedItem != null)
            {
                DataRowView row = dgTests.SelectedItem as DataRowView;
                if (row == null) return;

                IDTestTextBox.Text = row["IDTest"]?.ToString() ?? "";
                TestNazvanieTextBox.Text = row["Nazvanie"]?.ToString() ?? "";
                TestOpisanieTextBox.Text = row["Opisanie"]?.ToString() ?? "";
                TestTimeTextBox.Text = row["Vremya_vipolneniya"]?.ToString() ?? "";

                if (row["IDPredmet"] != DBNull.Value)
                    PredmetComboBox.SelectedValue = Convert.ToInt32(row["IDPredmet"]);
            }
        }

        /// <summary>
        /// Добавление нового теста через явный INSERT
        /// </summary>
        private void AddTestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(TestNazvanieTextBox.Text))
                {
                    MessageBox.Show("Введите название теста!", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    TestNazvanieTextBox.Focus();
                    return;
                }

                if (PredmetComboBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите дисциплину!", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    PredmetComboBox.Focus();
                    return;
                }

                if (TestNazvanieTextBox.Text.Trim().Length > 50)
                {
                    MessageBox.Show("Название теста не должно превышать 50 символов!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (TestOpisanieTextBox.Text.Trim().Length > 50)
                {
                    MessageBox.Show("Описание теста не должно превышать 50 символов!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                int vremya = 0;
                if (!string.IsNullOrWhiteSpace(TestTimeTextBox.Text))
                {
                    if (!int.TryParse(TestTimeTextBox.Text.Trim(), out vremya) || vremya < 0)
                    {
                        MessageBox.Show("Время выполнения должно быть неотрицательным целым числом!",
                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        TestTimeTextBox.Focus();
                        return;
                    }
                }

                // Явный INSERT с параметрами
                string insertSql = @"
                    INSERT INTO [UP03_Tests] 
                        (Nazvanie, Opisanie, Vremya_vipolneniya, IDPredmet)
                    VALUES 
                        (@Nazvanie, @Opisanie, @Vremya, @IDPredmet);
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(insertSql, connection))
                {
                    cmd.Parameters.AddWithValue("@Nazvanie", TestNazvanieTextBox.Text.Trim());
                    cmd.Parameters.AddWithValue("@Opisanie",
                        string.IsNullOrWhiteSpace(TestOpisanieTextBox.Text)
                            ? (object)DBNull.Value
                            : TestOpisanieTextBox.Text.Trim());
                    cmd.Parameters.AddWithValue("@Vremya", vremya);
                    cmd.Parameters.AddWithValue("@IDPredmet", PredmetComboBox.SelectedValue);

                    object result = cmd.ExecuteScalar();
                    int newId = Convert.ToInt32(result);
                }

                MessageBox.Show("Тест успешно добавлен", "Успех!",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadTests();
                ClearFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Изменение выбранного теста через явный UPDATE
        /// </summary>
        private void ChangeTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (dgTests.SelectedItem == null)
            {
                MessageBox.Show("Выберите тест для редактирования", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(IDTestTextBox.Text) ||
                    !int.TryParse(IDTestTextBox.Text.Trim(), out int idTest))
                {
                    MessageBox.Show("Не удалось определить ID теста!", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TestNazvanieTextBox.Text))
                {
                    MessageBox.Show("Введите название теста!", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (PredmetComboBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите дисциплину!", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (TestNazvanieTextBox.Text.Trim().Length > 50)
                {
                    MessageBox.Show("Название теста не должно превышать 50 символов!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (TestOpisanieTextBox.Text.Trim().Length > 50)
                {
                    MessageBox.Show("Описание теста не должно превышать 50 символов!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                int vremya = 0;
                if (!string.IsNullOrWhiteSpace(TestTimeTextBox.Text))
                {
                    if (!int.TryParse(TestTimeTextBox.Text.Trim(), out vremya) || vremya < 0)
                    {
                        MessageBox.Show("Время выполнения должно быть неотрицательным целым числом!",
                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                }

                // Явный UPDATE по IDTest
                string updateSql = @"
                    UPDATE [UP03_Tests]
                    SET Nazvanie = @Nazvanie,
                        Opisanie = @Opisanie,
                        Vremya_vipolneniya = @Vremya,
                        IDPredmet = @IDPredmet
                    WHERE IDTest = @IDTest";

                using (SqlCommand cmd = new SqlCommand(updateSql, connection))
                {
                    cmd.Parameters.AddWithValue("@Nazvanie", TestNazvanieTextBox.Text.Trim());
                    cmd.Parameters.AddWithValue("@Opisanie",
                        string.IsNullOrWhiteSpace(TestOpisanieTextBox.Text)
                            ? (object)DBNull.Value
                            : TestOpisanieTextBox.Text.Trim());
                    cmd.Parameters.AddWithValue("@Vremya", vremya);
                    cmd.Parameters.AddWithValue("@IDPredmet", PredmetComboBox.SelectedValue);
                    cmd.Parameters.AddWithValue("@IDTest", idTest);

                    int affected = cmd.ExecuteNonQuery();
                    if (affected == 0)
                    {
                        MessageBox.Show("Тест не найден в базе данных. Возможно, он был удалён.",
                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        LoadTests();
                        return;
                    }
                }

                MessageBox.Show("Тест обновлён", "Успех!",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadTests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании: {ex.Message}", "Ошибка!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Удаление теста через явные DELETE с транзакцией
        /// </summary>
        private void DeleteTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (dgTests.SelectedItem == null)
            {
                MessageBox.Show("Выберите тест для удаления", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                DataRowView selectedRow = (DataRowView)dgTests.SelectedItem;
                int idTest = Convert.ToInt32(selectedRow["IDTest"]);
                string nazvanie = selectedRow["Nazvanie"]?.ToString() ?? "";

                // Подсчёт связанных вопросов и ответов
                int questionCount = 0;
                int answerCount = 0;

                string countSql = @"
                    SELECT 
                        (SELECT COUNT(*) FROM [UP03_Voprosi] WHERE IDTest = @IDTest) AS QCount,
                        (SELECT COUNT(*) FROM [UP03_Otvet] 
                         WHERE IDVoprosa IN (SELECT IDVoprosa FROM [UP03_Voprosi] WHERE IDTest = @IDTest)) AS ACount";

                using (SqlCommand cmd = new SqlCommand(countSql, connection))
                {
                    cmd.Parameters.AddWithValue("@IDTest", idTest);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            questionCount = Convert.ToInt32(reader["QCount"]);
                            answerCount = Convert.ToInt32(reader["ACount"]);
                        }
                    }
                }

                string warningMessage = $"Вы уверены, что хотите удалить тест «{nazvanie}»?";
                if (questionCount > 0 || answerCount > 0)
                {
                    warningMessage += "\n\nВместе с тестом будет удалено:";
                    if (questionCount > 0) warningMessage += $"\n• Вопросов: {questionCount}";
                    if (answerCount > 0) warningMessage += $"\n• Вариантов ответов: {answerCount}";
                }

                MessageBoxResult result = MessageBox.Show(warningMessage,
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Транзакция удаления
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Ответы
                        string deleteAnswersSql = @"
                            DELETE FROM [UP03_Otvet]
                            WHERE IDVoprosa IN (
                                SELECT IDVoprosa FROM [UP03_Voprosi] WHERE IDTest = @IDTest
                            )";
                        using (SqlCommand cmd = new SqlCommand(deleteAnswersSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@IDTest", idTest);
                            cmd.ExecuteNonQuery();
                        }

                        // 2. Вопросы
                        string deleteQuestionsSql =
                            "DELETE FROM [UP03_Voprosi] WHERE IDTest = @IDTest";
                        using (SqlCommand cmd = new SqlCommand(deleteQuestionsSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@IDTest", idTest);
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Тест
                        string deleteTestSql =
                            "DELETE FROM [UP03_Tests] WHERE IDTest = @IDTest";
                        using (SqlCommand cmd = new SqlCommand(deleteTestSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@IDTest", idTest);
                            int affected = cmd.ExecuteNonQuery();
                            if (affected == 0)
                            {
                                throw new Exception(
                                    "Не удалось удалить тест: запись не найдена в базе данных.");
                            }
                        }

                        transaction.Commit();

                        MessageBox.Show("Тест успешно удалён", "Успех!",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadTests();
                        ClearFields();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Импорт теста из JSON-файла (без изменений)
        /// </summary>
        private void ImportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PredmetComboBox.SelectedValue == null)
                {
                    MessageBox.Show(
                        "Перед импортом выберите дисциплину в выпадающем списке!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    return;
                }

                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Title = "Выберите JSON-файл с тестом"
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                string jsonContent = File.ReadAllText(openFileDialog.FileName);

                TestImportModel testData;
                try
                {
                    testData = JsonConvert.DeserializeObject<TestImportModel>(jsonContent);
                }
                catch (JsonException jsonEx)
                {
                    MessageBox.Show(
                        $"Некорректный JSON-файл:\n{jsonEx.Message}",
                        "Ошибка импорта",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                if (testData == null)
                {
                    MessageBox.Show("Не удалось прочитать JSON-файл.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (testData.Voprosi == null || testData.Voprosi.Count == 0)
                {
                    MessageBox.Show("В JSON-файле отсутствуют вопросы.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int idPredmet = Convert.ToInt32(PredmetComboBox.SelectedValue);

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string insertTestSql = @"
                            INSERT INTO [UP03_Tests]
                                (Nazvanie, Opisanie, Vremya_vipolneniya, IDPredmet)
                            VALUES
                                (@Nazvanie, @Opisanie, @Vremya, @IDPredmet);
                            SELECT SCOPE_IDENTITY();";

                        int newTestId;
                        using (SqlCommand cmd = new SqlCommand(insertTestSql, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Nazvanie",
                                string.IsNullOrWhiteSpace(testData.Nazvanie)
                                    ? "Импортированный тест"
                                    : testData.Nazvanie);

                            cmd.Parameters.AddWithValue("@Opisanie",
                                string.IsNullOrWhiteSpace(testData.Opisanie)
                                    ? (object)DBNull.Value
                                    : testData.Opisanie);

                            cmd.Parameters.AddWithValue("@Vremya", testData.VremyaVipolneniya);
                            cmd.Parameters.AddWithValue("@IDPredmet", idPredmet);

                            newTestId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        int totalAnswers = 0;
                        foreach (var question in testData.Voprosi)
                        {
                            string insertQuestionSql = @"
                                INSERT INTO [UP03_Voprosi]
                                    (IDTest, TextVoprosa, TipVoprosa, Kol_vo_ballov)
                                VALUES
                                    (@IDTest, @Text, @Tip, @Balli);
                                SELECT SCOPE_IDENTITY();";

                            int newQuestionId;
                            using (SqlCommand cmd = new SqlCommand(insertQuestionSql, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@IDTest", newTestId);
                                cmd.Parameters.AddWithValue("@Text",
                                    string.IsNullOrWhiteSpace(question.TextVoprosa)
                                        ? "Вопрос без текста"
                                        : question.TextVoprosa);
                                cmd.Parameters.AddWithValue("@Tip",
                                    string.IsNullOrWhiteSpace(question.TipVoprosa)
                                        ? "Single"
                                        : question.TipVoprosa);
                                cmd.Parameters.AddWithValue("@Balli", question.KolVoBallov);

                                newQuestionId = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            if (question.Otvety != null)
                            {
                                foreach (var answer in question.Otvety)
                                {
                                    string insertAnswerSql = @"
                                        INSERT INTO [UP03_Otvet]
                                            (IDVoprosa, TextOtveta, isCorrect)
                                        VALUES
                                            (@IDVoprosa, @TextOtveta, @isCorrect);";

                                    using (SqlCommand cmd = new SqlCommand(insertAnswerSql, connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@IDVoprosa", newQuestionId);
                                        cmd.Parameters.AddWithValue("@TextOtveta",
                                            string.IsNullOrWhiteSpace(answer.TextOtveta)
                                                ? "Вариант ответа"
                                                : answer.TextOtveta);
                                        cmd.Parameters.AddWithValue("@isCorrect", answer.IsCorrect ? 1 : 0);
                                        cmd.ExecuteNonQuery();
                                        totalAnswers++;
                                    }
                                }
                            }
                        }

                        transaction.Commit();

                        MessageBox.Show(
                            $"Тест успешно импортирован!\n\n" +
                            $"Название: {testData.Nazvanie}\n" +
                            $"Вопросов: {testData.Voprosi.Count}\n" +
                            $"Вариантов ответа: {totalAnswers}",
                            "Импорт завершён",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        LoadTests();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при импорте:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Переход к главному окну администратора
        /// </summary>
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            AdminMainWindow adminWindow = new AdminMainWindow();
            adminWindow.Show();
            this.Close();
        }

        /// <summary>
        /// Очистка полей формы
        /// </summary>
        private void ClearFields()
        {
            IDTestTextBox.Clear();
            TestNazvanieTextBox.Clear();
            TestOpisanieTextBox.Clear();
            TestTimeTextBox.Clear();
            PredmetComboBox.SelectedIndex = -1;
        }

        /// <summary>
        /// Закрытие соединения с БД
        /// </summary>
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

    // ================== МОДЕЛИ ДЛЯ JSON ==================

    public class TestImportModel
    {
        public string Nazvanie { get; set; }
        public string Opisanie { get; set; }
        public int VremyaVipolneniya { get; set; }
        public List<QuestionImportModel> Voprosi { get; set; } = new List<QuestionImportModel>();
    }

    public class QuestionImportModel
    {
        public string TextVoprosa { get; set; }
        public string TipVoprosa { get; set; }
        public int KolVoBallov { get; set; }
        public List<AnswerImportModel> Otvety { get; set; } = new List<AnswerImportModel>();
    }

    public class AnswerImportModel
    {
        [JsonProperty("TextOtvet")]
        public string TextOtveta { get; set; }
        public bool IsCorrect { get; set; }
    }
}