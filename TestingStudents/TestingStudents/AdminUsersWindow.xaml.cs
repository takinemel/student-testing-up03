using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace TestingStudents
{
    public partial class AdminUsersWindow : Window
    {
        // ============================================================
        // ПОДКЛЮЧЕНИЕ К БАЗЕ ДАННЫХ
        // ============================================================

        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db; User Id=user88_db; Password=user88;TrustServerCertificate=True;";

        private SqlConnection connection;

        // Основная таблица (то, что показывается в DataGrid — может быть отфильтрована)
        private DataTable usersTable;

        // Полная копия без фильтра (для быстрого поиска через DefaultView.RowFilter)
        private DataTable usersOriginalTable;

        private DataTable rolesTable;

        private bool isPasswordVisible = false;


        // ============================================================
        // КОНСТРУКТОР
        // ============================================================

        public AdminUsersWindow()
        {
            InitializeComponent();
        }


        // ============================================================
        // ЗАГРУЗКА ОКНА
        // ============================================================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();

                LoadRoles();
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка подключения к базе данных:\n\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // ЗАГРУЗКА РОЛЕЙ
        // ============================================================

        private void LoadRoles()
        {
            try
            {
                string sql = @"
                    SELECT 
                        IDRole,
                        NazvanieRoli
                    FROM [UP03_Role]
                    ORDER BY IDRole";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(sql, connection))
                {
                    rolesTable = new DataTable();
                    adapter.Fill(rolesTable);

                    RoleComboBox.ItemsSource = rolesTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки ролей:\n\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // ЗАГРУЗКА ПОЛЬЗОВАТЕЛЕЙ
        // ============================================================

        private void LoadUsers()
        {
            try
            {
                string sql = @"
                    SELECT
                        u.IDUser,
                        u.Familiya,
                        u.Imya,
                        u.Otchestvo,
                        u.Login,
                        u.Parol,
                        u.DataSozdaniya,
                        u.IDRole,
                        r.NazvanieRoli
                    FROM [UP03_Users] u
                    INNER JOIN [UP03_Role] r
                        ON u.IDRole = r.IDRole
                    ORDER BY u.IDUser";

                using (SqlDataAdapter adapter =
                    new SqlDataAdapter(sql, connection))
                {
                    usersOriginalTable = new DataTable();
                    adapter.Fill(usersOriginalTable);

                    // Копия для DataGrid (копия — чтобы фильтровать независимо)
                    usersTable = usersOriginalTable.Copy();

                    dgUsers.ItemsSource = usersTable.DefaultView;

                    // Сбрасываем поиск при перезагрузке
                    if (TbSearch != null)
                        TbSearch.Text = string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки пользователей:\n\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // ПОИСК ПО ФАМИЛИИ ИЛИ ЛОГИНУ
        // ============================================================

        /// <summary>
        /// Поиск пользователей по фамилии ИЛИ логину.
        /// Использует DataView.RowFilter — быстрая фильтрация DataTable
        /// в памяти без обращения к БД.
        /// </summary>
        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (usersTable == null || dgUsers == null)
                    return;

                // Получаем текст поиска, убираем пробелы по краям
                string searchText = TbSearch.Text?.Trim() ?? string.Empty;

                DataView view = usersTable.DefaultView;

                if (string.IsNullOrEmpty(searchText))
                {
                    // Пустой поиск → показываем всех
                    view.RowFilter = string.Empty;
                }
                else
                {
                    // Экранирование одинарной кавычки (защита от инъекции в RowFilter)
                    string safe = searchText.Replace("'", "''");

                    // Условие: фамилия ИЛИ логин содержит введённый текст
                    // LIKE '%текст%' — регистронезависимый по умолчанию
                    view.RowFilter =
                        $"Familiya LIKE '%{safe}%' OR Login LIKE '%{safe}%'";
                }

                dgUsers.ItemsSource = view;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка поиска:\n\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // СБРОС ПОИСКА
        // ============================================================

        private void ResetSearchButton_Click(object sender, RoutedEventArgs e)
        {
            TbSearch.Text = string.Empty;
            // Метод TbSearch_TextChanged автоматически покажет всех
        }


        // ============================================================
        // ВЫБОР ПОЛЬЗОВАТЕЛЯ В DATAGRID
        // ============================================================

        private void dgUsers_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (dgUsers.SelectedItem == null)
                return;

            DataRowView row =
                dgUsers.SelectedItem as DataRowView;

            if (row == null)
                return;

            try
            {
                IDUserTextBox.Text =
                    row["IDUser"]?.ToString() ?? "";

                FamiliyaTextBox.Text =
                    row["Familiya"]?.ToString() ?? "";

                ImyaTextBox.Text =
                    row["Imya"]?.ToString() ?? "";

                OtchestvoTextBox.Text =
                    row["Otchestvo"] == DBNull.Value
                        ? ""
                        : row["Otchestvo"]?.ToString() ?? "";

                LoginTextBox.Text =
                    row["Login"]?.ToString() ?? "";

                string password =
                    row["Parol"] == DBNull.Value
                        ? ""
                        : row["Parol"]?.ToString() ?? "";

                PasswordTextBox.Text = password;
                PasswordBoxHidden.Password = password;
                isPasswordVisible = false;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBoxHidden.Visibility = Visibility.Visible;

                TogglePasswordButton.Content = "👁";

                if (row["IDRole"] != DBNull.Value)
                {
                    RoleComboBox.SelectedValue =
                        Convert.ToInt32(row["IDRole"]);
                }
                else
                {
                    RoleComboBox.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки данных пользователя:\n\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // ============================================================
        // ПОКАЗ / СКРЫТИЕ ПАРОЛЯ
        // ============================================================

        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isPasswordVisible)
            {
                // Переходим к отображению открытого пароля
                PasswordTextBox.Text = PasswordBoxHidden.Password;
                PasswordBoxHidden.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "🙈";
                isPasswordVisible = true;
            }
            else
            {
                // Возвращаем скрытый пароль
                PasswordBoxHidden.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBoxHidden.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "👁";
                isPasswordVisible = false;
            }
        }


        // ============================================================
        // ПОЛУЧЕНИЕ ПАРОЛЯ
        // ============================================================

        private string GetPassword()
        {
            if (PasswordTextBox.Visibility == Visibility.Visible)
                return PasswordTextBox.Text;

            return PasswordBoxHidden.Password;
        }


        // ============================================================
        // ДОБАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯ
        // ============================================================

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверка фамилии
                if (string.IsNullOrWhiteSpace(FamiliyaTextBox.Text))
                {
                    MessageBox.Show("Введите фамилию пользователя!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    FamiliyaTextBox.Focus();
                    return;
                }

                // Проверка имени
                if (string.IsNullOrWhiteSpace(ImyaTextBox.Text))
                {
                    MessageBox.Show("Введите имя пользователя!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    ImyaTextBox.Focus();
                    return;
                }

                // Проверка логина
                if (string.IsNullOrWhiteSpace(LoginTextBox.Text))
                {
                    MessageBox.Show("Введите логин пользователя!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    LoginTextBox.Focus();
                    return;
                }

                // Получаем пароль
                string password = GetPassword();

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Введите пароль пользователя!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать минимум 6 символов!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                // Проверка роли
                if (RoleComboBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите роль пользователя!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    RoleComboBox.Focus();
                    return;
                }

                // Проверка длины полей
                if (FamiliyaTextBox.Text.Trim().Length > 100 ||
                    ImyaTextBox.Text.Trim().Length > 100 ||
                    OtchestvoTextBox.Text.Trim().Length > 100 ||
                    LoginTextBox.Text.Trim().Length > 50)
                {
                    MessageBox.Show("Превышена допустимая длина одного из полей!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                // Проверка логина
                if (!Regex.IsMatch(LoginTextBox.Text.Trim(), @"^[a-zA-Z0-9._-]+$"))
                {
                    MessageBox.Show(
                        "Логин может содержать только латинские буквы, цифры, точку, дефис и подчёркивание!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                // Проверка уникальности логина
                string checkSql = @"
                    SELECT COUNT(*)
                    FROM [UP03_Users]
                    WHERE Login = @Login";

                using (SqlCommand checkCommand = new SqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.Add("@Login", SqlDbType.VarChar, 50).Value = LoginTextBox.Text.Trim();

                    int count = Convert.ToInt32(checkCommand.ExecuteScalar());
                    if (count > 0)
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует!",
                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                }

                // INSERT
                string insertSql = @"
                    INSERT INTO [UP03_Users]
                    (
                        IDRole,
                        Familiya,
                        Imya,
                        Otchestvo,
                        Login,
                        Parol,
                        DataSozdaniya
                    )
                    VALUES
                    (
                        @IDRole,
                        @Familiya,
                        @Imya,
                        @Otchestvo,
                        @Login,
                        @Parol,
                        @DataSozdaniya
                    )";

                using (SqlCommand insertCommand = new SqlCommand(insertSql, connection))
                {
                    insertCommand.Parameters.Add("@IDRole", SqlDbType.Int).Value = Convert.ToInt32(RoleComboBox.SelectedValue);
                    insertCommand.Parameters.Add("@Familiya", SqlDbType.VarChar, 100).Value = FamiliyaTextBox.Text.Trim();
                    insertCommand.Parameters.Add("@Imya", SqlDbType.VarChar, 100).Value = ImyaTextBox.Text.Trim();
                    insertCommand.Parameters.Add("@Otchestvo", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(OtchestvoTextBox.Text)
                            ? (object)DBNull.Value
                            : OtchestvoTextBox.Text.Trim();
                    insertCommand.Parameters.Add("@Login", SqlDbType.VarChar, 50).Value = LoginTextBox.Text.Trim();
                    insertCommand.Parameters.Add("@Parol", SqlDbType.VarChar, 100).Value = password.Trim();
                    insertCommand.Parameters.Add("@DataSozdaniya", SqlDbType.Date).Value = DateTime.Now.Date;

                    insertCommand.ExecuteNonQuery();
                }

                MessageBox.Show("Пользователь успешно добавлен!", "Успех!",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadUsers();
                ClearFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении:\n\n{ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ============================================================
        // ИЗМЕНЕНИЕ ПОЛЬЗОВАТЕЛЯ
        // ============================================================

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem == null)
            {
                MessageBox.Show("Выберите пользователя для редактирования!",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                DataRowView selectedRow = dgUsers.SelectedItem as DataRowView;
                if (selectedRow == null)
                {
                    MessageBox.Show("Не удалось определить выбранного пользователя.",
                        "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int currentId = Convert.ToInt32(selectedRow["IDUser"]);

                // Проверка обязательных полей
                if (string.IsNullOrWhiteSpace(FamiliyaTextBox.Text) ||
                    string.IsNullOrWhiteSpace(ImyaTextBox.Text) ||
                    string.IsNullOrWhiteSpace(LoginTextBox.Text))
                {
                    MessageBox.Show("Заполните обязательные поля (Фамилия, Имя, Логин)!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                string password = GetPassword();

                if (!string.IsNullOrWhiteSpace(password) && password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать минимум 6 символов!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (RoleComboBox.SelectedValue == null)
                {
                    MessageBox.Show("Выберите роль пользователя!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (FamiliyaTextBox.Text.Trim().Length > 100 ||
                    ImyaTextBox.Text.Trim().Length > 100 ||
                    OtchestvoTextBox.Text.Trim().Length > 100 ||
                    LoginTextBox.Text.Trim().Length > 50)
                {
                    MessageBox.Show("Превышена допустимая длина одного из полей!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                if (!Regex.IsMatch(LoginTextBox.Text.Trim(), @"^[a-zA-Z0-9._-]+$"))
                {
                    MessageBox.Show(
                        "Логин может содержать только латинские буквы, цифры, точку, дефис и подчёркивание!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }

                // Проверка уникальности логина
                string checkSql = @"
                    SELECT COUNT(*)
                    FROM [UP03_Users]
                    WHERE Login = @Login
                      AND IDUser <> @IDUser";

                using (SqlCommand checkCommand = new SqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.Add("@Login", SqlDbType.VarChar, 50).Value = LoginTextBox.Text.Trim();
                    checkCommand.Parameters.Add("@IDUser", SqlDbType.Int).Value = currentId;

                    int count = Convert.ToInt32(checkCommand.ExecuteScalar());
                    if (count > 0)
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует!",
                            "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                }

                // UPDATE
                string updateSql;

                if (string.IsNullOrWhiteSpace(password))
                {
                    updateSql = @"
                        UPDATE [UP03_Users]
                        SET
                            Familiya = @Familiya,
                            Imya = @Imya,
                            Otchestvo = @Otchestvo,
                            Login = @Login,
                            IDRole = @IDRole
                        WHERE IDUser = @IDUser";
                }
                else
                {
                    updateSql = @"
                        UPDATE [UP03_Users]
                        SET
                            Familiya = @Familiya,
                            Imya = @Imya,
                            Otchestvo = @Otchestvo,
                            Login = @Login,
                            Parol = @Parol,
                            IDRole = @IDRole
                        WHERE IDUser = @IDUser";
                }

                using (SqlCommand updateCommand = new SqlCommand(updateSql, connection))
                {
                    updateCommand.Parameters.Add("@Familiya", SqlDbType.VarChar, 100).Value = FamiliyaTextBox.Text.Trim();
                    updateCommand.Parameters.Add("@Imya", SqlDbType.VarChar, 100).Value = ImyaTextBox.Text.Trim();
                    updateCommand.Parameters.Add("@Otchestvo", SqlDbType.VarChar, 100).Value =
                        string.IsNullOrWhiteSpace(OtchestvoTextBox.Text)
                            ? (object)DBNull.Value
                            : OtchestvoTextBox.Text.Trim();
                    updateCommand.Parameters.Add("@Login", SqlDbType.VarChar, 50).Value = LoginTextBox.Text.Trim();
                    updateCommand.Parameters.Add("@IDRole", SqlDbType.Int).Value = Convert.ToInt32(RoleComboBox.SelectedValue);
                    updateCommand.Parameters.Add("@IDUser", SqlDbType.Int).Value = currentId;

                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        updateCommand.Parameters.Add("@Parol", SqlDbType.VarChar, 100).Value = password.Trim();
                    }

                    int affectedRows = updateCommand.ExecuteNonQuery();

                    if (affectedRows == 0)
                    {
                        MessageBox.Show("Пользователь не найден в базе данных.",
                            "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                MessageBox.Show("Данные пользователя обновлены!", "Успех!",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadUsers();
                ClearFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании:\n\n{ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ============================================================
        // УДАЛЕНИЕ ПОЛЬЗОВАТЕЛЯ
        // ============================================================

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsers.SelectedItem == null)
            {
                MessageBox.Show("Выберите пользователя для удаления!",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                DataRowView selectedRow = dgUsers.SelectedItem as DataRowView;
                if (selectedRow == null) return;

                int idUser = Convert.ToInt32(selectedRow["IDUser"]);
                string fio = $"{selectedRow["Familiya"]} {selectedRow["Imya"]}";
                int idRole = Convert.ToInt32(selectedRow["IDRole"]);

                // Нельзя удалить самого себя
                if (idUser == App.CurrentUserId)
                {
                    MessageBox.Show("Нельзя удалить собственную учётную запись!",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Нельзя удалить последнего администратора
                if (idRole == 1)
                {
                    string countAdminsSql = @"
                        SELECT COUNT(*)
                        FROM [UP03_Users]
                        WHERE IDRole = 1";

                    using (SqlCommand command = new SqlCommand(countAdminsSql, connection))
                    {
                        int adminCount = Convert.ToInt32(command.ExecuteScalar());

                        if (adminCount <= 1)
                        {
                            MessageBox.Show("Нельзя удалить последнего администратора системы!",
                                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                // Проверяем связанные записи
                string checkLinksSql = @"
                    SELECT
                        (SELECT COUNT(*) FROM [UP03_StudentGroup] WHERE IDStudenta = @IDUser) AS StudentLinks,
                        (SELECT COUNT(*) FROM [UP03_PrepodPredmet] WHERE IDPrepodavatelya = @IDUser) AS TeacherLinks,
                        (SELECT COUNT(*) FROM [UP03_Resultat] WHERE IDUser = @IDUser) AS ResultLinks";

                int studentLinks = 0;
                int teacherLinks = 0;
                int resultLinks = 0;

                using (SqlCommand command = new SqlCommand(checkLinksSql, connection))
                {
                    command.Parameters.Add("@IDUser", SqlDbType.Int).Value = idUser;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            studentLinks = Convert.ToInt32(reader["StudentLinks"]);
                            teacherLinks = Convert.ToInt32(reader["TeacherLinks"]);
                            resultLinks = Convert.ToInt32(reader["ResultLinks"]);
                        }
                    }
                }

                string warning = $"Вы уверены, что хотите удалить пользователя «{fio}»?";

                if (studentLinks + teacherLinks + resultLinks > 0)
                {
                    warning += "\n\nВнимание! С этим пользователем связаны записи:";
                    if (studentLinks > 0) warning += $"\n• Зачислений в группы: {studentLinks}";
                    if (teacherLinks > 0) warning += $"\n• Назначений на дисциплины: {teacherLinks}";
                    if (resultLinks > 0) warning += $"\n• Результатов тестирования: {resultLinks}";
                    warning += "\n\nСвязанные записи также будут удалены.";
                }

                MessageBoxResult result = MessageBox.Show(warning,
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Удаляем связанные записи
                string[] deleteSqls =
                {
                    "DELETE FROM [UP03_StudentGroup] WHERE IDStudenta = @IDUser",
                    "DELETE FROM [UP03_PrepodPredmet] WHERE IDPrepodavatelya = @IDUser",
                    "DELETE FROM [UP03_Resultat] WHERE IDUser = @IDUser"
                };

                foreach (string sql in deleteSqls)
                {
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add("@IDUser", SqlDbType.Int).Value = idUser;
                        command.ExecuteNonQuery();
                    }
                }

                // Удаляем самого пользователя
                string deleteUserSql = "DELETE FROM [UP03_Users] WHERE IDUser = @IDUser";

                using (SqlCommand deleteCommand = new SqlCommand(deleteUserSql, connection))
                {
                    deleteCommand.Parameters.Add("@IDUser", SqlDbType.Int).Value = idUser;

                    int affectedRows = deleteCommand.ExecuteNonQuery();

                    if (affectedRows == 0)
                    {
                        MessageBox.Show("Пользователь не найден в базе данных.",
                            "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                MessageBox.Show("Пользователь удалён!", "Успех!",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadUsers();
                ClearFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении:\n\n{ex.Message}",
                    "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // ============================================================
        // КНОПКА "ВЫЙТИ"
        // ============================================================

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            AdminMainWindow adminWindow = new AdminMainWindow();
            adminWindow.Show();
            this.Close();
        }


        // ============================================================
        // ОЧИСТКА ПОЛЕЙ
        // ============================================================

        private void ClearFields()
        {
            IDUserTextBox.Clear();
            FamiliyaTextBox.Clear();
            ImyaTextBox.Clear();
            OtchestvoTextBox.Clear();
            LoginTextBox.Clear();
            PasswordTextBox.Clear();
            PasswordBoxHidden.Clear();
            RoleComboBox.SelectedIndex = -1;

            // Возвращаем пароль в скрытый режим
            isPasswordVisible = false;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBoxHidden.Visibility = Visibility.Visible;
            TogglePasswordButton.Content = "👁";
        }


        // ============================================================
        // ЗАКРЫТИЕ СОЕДИНЕНИЯ
        // ============================================================

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (connection != null)
            {
                if (connection.State == ConnectionState.Open)
                    connection.Close();

                connection.Dispose();
                connection = null;
            }
        }
    }
}