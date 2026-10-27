using System;
using System.Data.SqlClient;
using System.Windows;

namespace TestingStudents
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string con = @"data source=stud-mssql.sttec.yar.ru,38325;initial catalog=user88_db;persist security info=True;user id=user88_db;password=user88;multipleactiveresultsets=True;encrypt=True;trustservercertificate=True";

            if (string.IsNullOrWhiteSpace(LoginTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("Заполните все поля ввода!", "Авторизация",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                using (SqlConnection conConnection = new SqlConnection(con))
                {
                    string command = @"
                        SELECT u.IDUser, u.IDRole, u.Login, u.Parol, 
                               u.Familiya, u.Imya, u.Otchestvo, r.NazvanieRoli
                        FROM [UP03_Users] u
                        JOIN [UP03_Role] r ON u.IDRole = r.IDRole
                        WHERE u.Login = @Login AND u.Parol = @Parol";

                    using (SqlCommand sqlCommand = new SqlCommand(command, conConnection))
                    {
                        sqlCommand.Parameters.AddWithValue("@Login", LoginTextBox.Text.Trim());
                        sqlCommand.Parameters.AddWithValue("@Parol", PasswordBox.Password);

                        conConnection.Open();

                        using (SqlDataReader reader = sqlCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // получение данных пользователя
                                int userId = Convert.ToInt32(reader["IDUser"]);
                                int roleId = Convert.ToInt32(reader["IDRole"]);
                                string roleName = reader["NazvanieRoli"].ToString();

                                string familiya = reader["Familiya"]?.ToString() ?? "";
                                string imya = reader["Imya"]?.ToString() ?? "";
                                string otchestvo = reader["Otchestvo"]?.ToString() ?? "";
                                string fio = $"{familiya} {imya} {otchestvo}".Trim();

                                // сохранение в сессию
                                App.CurrentUserId = userId;
                                App.CurrentRoleId = roleId;
                                App.CurrentRoleName = roleName;
                                App.CurrentUserFio = fio;

                                // окно по роли
                                Window nextWindow;

                                switch (roleId)
                                {
                                    case 1:
                                        nextWindow = new AdminMainWindow();
                                        break;
                                    case 2:
                                        nextWindow = new TeacherMainWindow();
                                        break;
                                    case 3:
                                        nextWindow = new StudentMainWindow();
                                        break;
                                    default:
                                        MessageBox.Show(
                                            "Роль пользователя не определена. Обратитесь к администратору.",
                                            "Авторизация",
                                            MessageBoxButton.OK, MessageBoxImage.Warning);
                                        return;
                                }

                                nextWindow.Show();
                                this.Close();
                            }
                            else
                            {
                                MessageBox.Show("Неверный логин или пароль", "Авторизация",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                PasswordBox.Clear();
                                PasswordBox.Focus();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Авторизация",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}