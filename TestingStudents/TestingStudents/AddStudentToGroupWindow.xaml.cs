using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TestingStudents
{
    /// <summary>
    /// Логика взаимодействия для AddStudentToGroupWindow.xaml
    /// </summary>
    public partial class AddStudentToGroupWindow : Window
    {
        string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        public AddStudentToGroupWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectString))
                {
                    conn.Open();

                    // загрузка групп
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT IDGroup, Nazvanie + ' (' + CAST(Kurs AS VARCHAR) + ' курс)' AS DisplayName " +
                        "FROM [UP03_Groups] ORDER BY Kurs, Nazvanie", conn))
                    {
                        DataTable dt = new DataTable();
                        using (SqlDataReader r = cmd.ExecuteReader()) dt.Load(r);
                        GroupComboBox.ItemsSource = dt.DefaultView;
                    }

                    // загрузка студентов (только роль 3 = Студент)
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT IDUser, Familiya + ' ' + Imya AS DisplayName " +
                        "FROM [UP03_Users] WHERE IDRole = 3 ORDER BY Familiya", conn))
                    {
                        DataTable dt = new DataTable();
                        using (SqlDataReader r = cmd.ExecuteReader()) dt.Load(r);
                        StudentComboBox.ItemsSource = dt.DefaultView;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnrollButton_Click(object sender, RoutedEventArgs e)
        {
            if (GroupComboBox.SelectedValue == null || StudentComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите группу и студента!",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectString))
                {
                    conn.Open();

                    int idGroup = Convert.ToInt32(GroupComboBox.SelectedValue);
                    int idStudent = Convert.ToInt32(StudentComboBox.SelectedValue);

                    // проверка дубликата
                    using (SqlCommand checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM [UP03_StudentGroup] " +
                        "WHERE IDStudenta = @IDStudenta AND IDGroup = @IDGroup", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@IDStudenta", idStudent);
                        checkCmd.Parameters.AddWithValue("@IDGroup", idGroup);

                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists > 0)
                        {
                            MessageBox.Show("Этот студент уже зачислен в данную группу!",
                                "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                    }

                    // вставка
                    using (SqlCommand cmd = new SqlCommand(
                        "INSERT INTO [UP03_StudentGroup] (IDStudenta, IDGroup) " +
                        "VALUES (@IDStudenta, @IDGroup)", conn))
                    {
                        cmd.Parameters.AddWithValue("@IDStudenta", idStudent);
                        cmd.Parameters.AddWithValue("@IDGroup", idGroup);
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("Студент успешно зачислен в группу!",
                        "Успех!", MessageBoxButton.OK, MessageBoxImage.Information);

                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при зачислении: {ex.Message}", "Ошибка!",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
