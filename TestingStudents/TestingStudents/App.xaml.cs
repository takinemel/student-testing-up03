using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TestingStudents
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static int CurrentUserId { get; set; }
        public static int CurrentRoleId { get; set; }
        public static string CurrentRoleName { get; set; }
        public static string CurrentUserFio { get; set; }
    }
}
