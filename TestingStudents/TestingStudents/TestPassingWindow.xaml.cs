using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TestingStudents
{
    /// <summary>
    /// Окно прохождения теста студентом.
    /// Загружает вопросы из UP03_Voprosi и ответы из UP03_Otvet,
    /// предоставляет UI для выбора ответов, ведёт таймер,
    /// по завершении считает процент, выставляет оценку и сохраняет результат.
    /// </summary>
    public partial class TestPassingWindow : Window
    {
        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        private readonly int _testId;
        private readonly int _userId;

        private SqlConnection connection;

        private string _testNazvanie = "";
        private string _predmetNazvanie = "";
        private int _vremyaVipolneniyaMin = 0;
        private int _passingScore = 60;   

        private List<QuestionRuntime> _questions = new List<QuestionRuntime>();

        private DispatcherTimer _timer;
        private TimeSpan _remaining;

        public TestPassingWindow(int testId, int userId)
        {
            InitializeComponent();
            _testId = testId;
            _userId = userId;
        }

        //  ЗАГРУЗКА 

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();

                LoadTestInfo();
                LoadQuestions();

                RenderQuestions();

                if (_vremyaVipolneniyaMin > 0)
                {
                    _remaining = TimeSpan.FromMinutes(_vremyaVipolneniyaMin);
                    _timer = new DispatcherTimer();
                    _timer.Interval = TimeSpan.FromSeconds(1);
                    _timer.Tick += Timer_Tick;
                    _timer.Start();
                    UpdateTimerText();
                }
                else
                {
                    TimerTextBlock.Text = "без ограничений";
                }

                UpdateProgress();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки теста: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        /// <summary>
        /// Загрузка информации о тесте: название, дисциплина, время.
        /// </summary>
        private void LoadTestInfo()
        {
            string sql = @"
                SELECT t.Nazvanie, t.Vremya_vipolneniya, p.Nazvanie AS Predmet
                FROM [UP03_Tests] t
                JOIN [UP03_Predmet] p ON t.IDPredmet = p.IDPredmet
                WHERE t.IDTest = @IDTest";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@IDTest", _testId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        _testNazvanie = reader["Nazvanie"]?.ToString() ?? "Без названия";
                        _predmetNazvanie = reader["Predmet"]?.ToString() ?? "—";
                        _vremyaVipolneniyaMin = reader["Vremya_vipolneniya"] != DBNull.Value
                            ? Convert.ToInt32(reader["Vremya_vipolneniya"])
                            : 0;
                    }
                    else
                    {
                        throw new Exception("Тест не найден в базе данных.");
                    }
                }
            }

            TestTitleTextBlock.Text = _testNazvanie;
            TestInfoTextBlock.Text = $"Дисциплина: {_predmetNazvanie}" +
                (_vremyaVipolneniyaMin > 0
                    ? $" • Ограничение по времени: {_vremyaVipolneniyaMin} мин"
                    : " • Без ограничения по времени");
        }

        /// <summary>
        /// Загрузка вопросов и вариантов ответов.
        /// Вопросы из UP03_Voprosi, ответы — из UP03_Otvet.
        /// </summary>
        private void LoadQuestions()
        {
            _questions.Clear();

            string questionSql = @"
                SELECT IDVoprosa, TextVoprosa, TipVoprosa, Kol_vo_ballov
                FROM [UP03_Voprosi]
                WHERE IDTest = @IDTest
                ORDER BY IDVoprosa";

            using (SqlCommand cmd = new SqlCommand(questionSql, connection))
            {
                cmd.Parameters.AddWithValue("@IDTest", _testId);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _questions.Add(new QuestionRuntime
                        {
                            Id = Convert.ToInt32(reader["IDVoprosa"]),
                            Text = reader["TextVoprosa"]?.ToString() ?? "",
                            Tip = reader["TipVoprosa"]?.ToString() ?? "Single",
                            Ballov = reader["Kol_vo_ballov"] != DBNull.Value
                                ? Convert.ToInt32(reader["Kol_vo_ballov"])
                                : 1
                        });
                    }
                }
            }

            if (_questions.Count == 0)
                throw new Exception("В этом тесте нет вопросов.");

            string answerSql = @"
                SELECT IDOtveta, TextOtveta, isCorrect
                FROM [UP03_Otvet]
                WHERE IDVoprosa = @IDVoprosa
                ORDER BY IDOtveta";

            foreach (var q in _questions)
            {
                using (SqlCommand cmd = new SqlCommand(answerSql, connection))
                {
                    cmd.Parameters.AddWithValue("@IDVoprosa", q.Id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            q.Answers.Add(new AnswerRuntime
                            {
                                Id = Convert.ToInt32(reader["IDOtveta"]),
                                Text = reader["TextOtveta"]?.ToString() ?? "",
                                IsCorrect = reader["isCorrect"] != DBNull.Value
                                    && Convert.ToBoolean(reader["isCorrect"])
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Отрисовка всех вопросов в QuestionsPanel.
        /// </summary>
        private void RenderQuestions()
        {
            QuestionsPanel.Children.Clear();

            int number = 1;
            foreach (var q in _questions)
            {
                Border card = new Border();
                card.Style = (Style)FindResource("CardStyle");
                card.Margin = new Thickness(0, 0, 0, 12);

                StackPanel stack = new StackPanel();

                TextBlock title = new TextBlock();
                title.Text = $"Вопрос {number}. {q.Text}";
                title.FontSize = 15;
                title.FontWeight = FontWeights.SemiBold;
                title.Foreground = (Brush)new BrushConverter().ConvertFrom("#212529");
                title.TextWrapping = TextWrapping.Wrap;
                title.Margin = new Thickness(0, 0, 0, 4);
                stack.Children.Add(title);

                TextBlock meta = new TextBlock();
                meta.Text = $"Тип: {(q.Tip == "Multiple" ? "множественный выбор" : "одиночный выбор")} • " +
                            $"Баллов: {q.Ballov}";
                meta.FontSize = 12;
                meta.Foreground = (Brush)new BrushConverter().ConvertFrom("#6C757D");
                meta.Margin = new Thickness(0, 0, 0, 12);
                stack.Children.Add(meta);

                q.IsMultipleChoice = q.Tip == "Multiple";
                q.RuntimeControls = new List<Control>();

                foreach (var a in q.Answers)
                {
                    if (q.IsMultipleChoice)
                    {
                        CheckBox cb = new CheckBox();
                        cb.Content = a.Text;
                        cb.Tag = a.Id;
                        cb.Margin = new Thickness(0, 4, 0, 4);
                        cb.FontSize = 14;
                        cb.Checked += Answer_Changed;
                        cb.Unchecked += Answer_Changed;
                        stack.Children.Add(cb);
                        q.RuntimeControls.Add(cb);
                    }
                    else
                    {
                        RadioButton rb = new RadioButton();
                        rb.Content = a.Text;
                        rb.Tag = a.Id;
                        rb.GroupName = $"Question_{q.Id}";
                        rb.Margin = new Thickness(0, 4, 0, 4);
                        rb.FontSize = 14;
                        rb.Checked += Answer_Changed;
                        stack.Children.Add(rb);
                        q.RuntimeControls.Add(rb);
                    }
                }

                card.Child = stack;
                QuestionsPanel.Children.Add(card);

                number++;
            }
        }

        //  ОТВЕТЫ 

        /// <summary>
        /// Событие при выборе/снятии ответа.
        /// Обновляет прогресс и счётчик отвеченных.
        /// </summary>
        private void Answer_Changed(object sender, RoutedEventArgs e)
        {
            UpdateProgress();
        }

        /// <summary>
        /// Проверяет, дан ли ответ на вопрос.
        /// </summary>
        private bool IsAnswered(QuestionRuntime q)
        {
            if (q.RuntimeControls == null) return false;

            foreach (var control in q.RuntimeControls)
            {
                if (control is CheckBox cb && cb.IsChecked == true) return true;
                if (control is RadioButton rb && rb.IsChecked == true) return true;
            }
            return false;
        }

        /// <summary>
        /// Возвращает список ID выбранных ответов.
        /// </summary>
        private List<int> GetSelectedAnswerIds(QuestionRuntime q)
        {
            var result = new List<int>();

            if (q.RuntimeControls == null) return result;

            foreach (var control in q.RuntimeControls)
            {
                if (control is CheckBox cb && cb.IsChecked == true)
                    result.Add(Convert.ToInt32(cb.Tag));
                else if (control is RadioButton rb && rb.IsChecked == true)
                    result.Add(Convert.ToInt32(rb.Tag));
            }

            return result;
        }

        /// <summary>
        /// Обновляет ProgressBar и счётчик отвеченных вопросов.
        /// </summary>
        private void UpdateProgress()
        {
            int answered = 0;
            foreach (var q in _questions)
            {
                if (IsAnswered(q)) answered++;
            }

            int total = _questions.Count;
            int percent = total > 0 ? (int)((double)answered / total * 100) : 0;

            ProgressBarControl.Value = percent;
            AnsweredCountTextBlock.Text = $"Отвечено: {answered} из {total}";

            FinishTestButton.IsEnabled = answered == total;
        }

        //  ТАЙМЕР 

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_remaining.TotalSeconds > 0)
            {
                _remaining = _remaining.Subtract(TimeSpan.FromSeconds(1));
                UpdateTimerText();

                if (_remaining.TotalSeconds <= 60)
                {
                    TimerTextBlock.Foreground =
                        (Brush)new BrushConverter().ConvertFrom("#DC3545");
                }
            }
            else
            {
                _timer.Stop();
                MessageBox.Show(
                    "Время выполнения теста истекло. Тест будет завершён автоматически.",
                    "Время истекло",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                FinishTestInternal(autoSubmitted: true);
            }
        }

        private void UpdateTimerText()
        {
            TimerTextBlock.Text = _remaining.ToString(@"mm\:ss");
        }

        //  ЗАВЕРШЕНИЕ ТЕСТА 

        private void FinishTestButton_Click(object sender, RoutedEventArgs e)
        {
            int answered = 0;
            foreach (var q in _questions) if (IsAnswered(q)) answered++;

            if (answered < _questions.Count)
            {
                var result = MessageBox.Show(
                    $"Вы ответили только на {answered} из {_questions.Count} вопросов.\n" +
                    "Завершить тест? Неотвеченные вопросы будут засчитаны как неправильные.",
                    "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }
            else
            {
                var result = MessageBox.Show(
                    "Вы уверены, что хотите завершить тест?",
                    "Подтверждение завершения",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;
            }

            FinishTestInternal(autoSubmitted: false);
        }

        /// <summary>
        /// Основная логика: сверка ответов, подсчёт процента, оценка, сохранение.
        /// </summary>
        private void FinishTestInternal(bool autoSubmitted)
        {
            if (_timer != null) _timer.Stop();

            try
            {
                int totalBallov = 0;
                int earnedBallov = 0;
                int correctCount = 0;

                foreach (var q in _questions)
                {
                    totalBallov += q.Ballov;

                    var selected = GetSelectedAnswerIds(q);
                    var correct = new List<int>();
                    foreach (var a in q.Answers) if (a.IsCorrect) correct.Add(a.Id);

                    selected.Sort();
                    correct.Sort();

                    bool isCorrect = selected.Count == correct.Count;
                    if (isCorrect)
                    {
                        for (int i = 0; i < selected.Count; i++)
                        {
                            if (selected[i] != correct[i]) { isCorrect = false; break; }
                        }
                    }

                    if (isCorrect)
                    {
                        earnedBallov += q.Ballov;
                        correctCount++;
                    }
                }

                double percent = totalBallov > 0
                    ? (double)earnedBallov / totalBallov * 100.0
                    : 0;

                int ocenka = CalculateGrade(percent, _passingScore);

                int resultId = SaveResult(earnedBallov, percent, ocenka);

                string message =
                    $"Тест завершён!\n\n" +
                    $"Правильных ответов: {correctCount} из {_questions.Count}\n" +
                    $"Набрано баллов: {earnedBallov} из {totalBallov}\n" +
                    $"Процент выполнения: {percent:F1}%\n" +
                    $"Оценка: {ocenka}\n\n";

                if (autoSubmitted)
                    message += "Тест был завершён автоматически (время истекло).";

                MessageBox.Show(message, "Результат теста",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при завершении теста: {ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Сохранение результата в таблицу UP03_Resultat.
        /// </summary>
        private int SaveResult(int balli, double percent, int ocenka)
        {
            string sql = @"
                INSERT INTO [UP03_Resultat] (IDUser, IDTest, Balli, Procent, Ocenka)
                VALUES (@IDUser, @IDTest, @Balli, @Procent, @Ocenka);
                SELECT SCOPE_IDENTITY();";

            using (SqlCommand cmd = new SqlCommand(sql, connection))
            {
                cmd.Parameters.AddWithValue("@IDUser", _userId);
                cmd.Parameters.AddWithValue("@IDTest", _testId);
                cmd.Parameters.AddWithValue("@Balli", balli);
                cmd.Parameters.AddWithValue("@Procent", Math.Round((decimal)percent, 2));
                cmd.Parameters.AddWithValue("@Ocenka", ocenka);

                object id = cmd.ExecuteScalar();
                return Convert.ToInt32(id);
            }
        }

        /// <summary>
        /// Расчёт оценки по проценту и порогу.
        /// </summary>
        private int CalculateGrade(double percent, int passingScore)
        {
            if (percent < passingScore) return 2;
            if (percent < 70) return 3;
            if (percent < 85) return 4;
            return 5;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите прервать прохождение теста?\n" +
                "Ответы не будут сохранены.",
                "Прерывание теста",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (_timer != null) _timer.Stop();
                this.DialogResult = false;
                this.Close();
            }
        }

        /// <summary>
        /// Перехват закрытия окна (крестик).
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_timer != null) _timer.Stop();
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

    //  ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ 

    /// <summary>
    /// Вопрос во время прохождения теста.
    /// </summary>
    public class QuestionRuntime
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string Tip { get; set; }
        public int Ballov { get; set; }

        public bool IsMultipleChoice { get; set; }
        public List<AnswerRuntime> Answers { get; set; } = new List<AnswerRuntime>();

        /// <summary>
        /// Ссылки на UI-контролы (RadioButton/CheckBox) для чтения выбора.
        /// </summary>
        public List<Control> RuntimeControls { get; set; }
    }

    /// <summary>
    /// Вариант ответа.
    /// </summary>
    public class AnswerRuntime
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
    }
}