using System;
using System.Collections.Generic;
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
    /// Логика взаимодействия для AdminMainWindow.xaml
    /// </summary>
    public partial class AdminMainWindow : Window
    {
        public AdminMainWindow()
        {
            InitializeComponent();
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            AdminUsersWindow usersWindow = new AdminUsersWindow();
            usersWindow.Show();
            this.Close();
        }

        private void GroupsButton_Click(object sender, RoutedEventArgs e)
        {
            AdminGroupsWindow groupsWindow = new AdminGroupsWindow();
            groupsWindow.Show();
            this.Close();
        }

        private void TestsButton_Click(object sender, RoutedEventArgs e)
        {
            AdminTestsWindow testsWindow = new AdminTestsWindow();
            testsWindow.Show();
            this.Close();
        }

        private void StatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            AdminStatisticsWindow statsWindow = new AdminStatisticsWindow();
            statsWindow.Show();
            this.Close();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow loginWindow = new MainWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
