using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace TestingStudents
{
    /// <summary>
    /// Логика взаимодействия для AdminGroupsWindow.xaml
    /// Управление учебными группами (таблица UP03_Groups).
    /// Поле «Курс» — ComboBox со значениями 1–6.
    /// </summary>
    public partial class AdminGroupsWindow : Window
    {
        // =========================================================
        // ПОДКЛЮЧЕНИЕ К БАЗЕ ДАННЫХ
        // =========================================================

        private string ConnectString = "Server=stud-mssql.sttec.yar.ru,38325;Database=user88_db;User Id=user88_db;Password=user88;TrustServerCertificate=True;";

        private DataTable dataTable;
        private SqlDataAdapter adapter;
        private SqlConnection connection;

        // Таблица отделений
        private DataTable otdeleniyaTable;

        // Таблица курсов (для ComboBox)
        private DataTable kursyTable;


        // =========================================================
        // КОНСТРУКТОР
        // =========================================================

        public AdminGroupsWindow()
        {
            InitializeComponent();
        }


        // =========================================================
        // ЗАГРУЗКА ОКНА
        // =========================================================

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                connection = new SqlConnection(ConnectString);
                connection.Open();
                LoadOtdeleniya();
                LoadKursy();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка подключения к базе данных:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ЗАГРУЗКА ОТДЕЛЕНИЙ В COMBOBOX
        // =========================================================

        private void LoadOtdeleniya()
        {
            try
            {
                string sql = @"
                    SELECT
                        IDOtdelenie,
                        Otdelenie
                    FROM UP03_Otdelenie
                    ORDER BY Otdelenie";

                using (SqlDataAdapter otdelenieAdapter =
                       new SqlDataAdapter(sql, connection))
                {
                    otdeleniyaTable = new DataTable();
                    otdelenieAdapter.Fill(otdeleniyaTable);

                    OtdelenieComboBox.ItemsSource = otdeleniyaTable.DefaultView;
                    OtdelenieComboBox.DisplayMemberPath = "Otdelenie";
                    OtdelenieComboBox.SelectedValuePath = "IDOtdelenie";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки отделений:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ЗАГРУЗКА КУРСОВ В COMBOBOX
        // =========================================================

        /// <summary>
        /// Заполняет ComboBox «Курс обучения» значениями 1–6.
        /// Курсы хранятся в UP03_Groups.Kurs (int), диапазон 1–6.
        /// </summary>
        private void LoadKursy()
        {
            try
            {
                kursyTable = new DataTable();
                kursyTable.Columns.Add("Value", typeof(int));
                kursyTable.Columns.Add("Display", typeof(string));

                for (int i = 1; i <= 4; i++)
                {
                    DataRow row = kursyTable.NewRow();
                    row["Value"] = i;
                    row["Display"] = $"{i} курс";
                    kursyTable.Rows.Add(row);
                }

                KursComboBox.ItemsSource = kursyTable.DefaultView;
                KursComboBox.DisplayMemberPath = "Display";
                KursComboBox.SelectedValuePath = "Value";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки курсов:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ЗАГРУЗКА ГРУПП
        // =========================================================

        private void LoadGroups()
        {
            try
            {
                string selectSql = @"
                    SELECT
                        g.IDGroup,
                        g.Nazvanie,
                        g.Kurs,
                        g.IDOtdelenie,
                        o.Otdelenie AS NazvanieOtdeleniya
                    FROM UP03_Groups g
                    LEFT JOIN UP03_Otdelenie o
                        ON g.IDOtdelenie = o.IDOtdelenie
                    ORDER BY
                        g.IDGroup";

                adapter = new SqlDataAdapter(selectSql, connection);

                dataTable = new DataTable();
                adapter.Fill(dataTable);

                dgGroups.ItemsSource = dataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка загрузки групп:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ВЫБОР ГРУППЫ В DATAGRID
        // =========================================================

        private void dgGroups_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (dgGroups.SelectedItem == null)
                return;

            DataRowView row = dgGroups.SelectedItem as DataRowView;
            if (row == null)
                return;

            // ID
            IDGroupTextBox.Text =
                row["IDGroup"] == DBNull.Value
                    ? ""
                    : row["IDGroup"].ToString();

            // Название
            NazvanieTextBox.Text =
                row["Nazvanie"] == DBNull.Value
                    ? ""
                    : row["Nazvanie"].ToString();

            // Курс
            if (row["Kurs"] != DBNull.Value)
            {
                int kursValue = Convert.ToInt32(row["Kurs"]);
                KursComboBox.SelectedValue = kursValue;
            }
            else
            {
                KursComboBox.SelectedIndex = -1;
            }

            // Отделение
            if (row["IDOtdelenie"] != DBNull.Value)
            {
                OtdelenieComboBox.SelectedValue =
                    Convert.ToInt32(row["IDOtdelenie"]);
            }
            else
            {
                OtdelenieComboBox.SelectedIndex = -1;
            }
        }


        // =========================================================
        // ДОБАВЛЕНИЕ ГРУППЫ
        // =========================================================

        private void AddButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                // -------------------------------------------------
                // Проверка названия
                // -------------------------------------------------

                if (string.IsNullOrWhiteSpace(NazvanieTextBox.Text))
                {
                    MessageBox.Show(
                        "Введите название группы!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    NazvanieTextBox.Focus();
                    return;
                }

                string nazvanie = NazvanieTextBox.Text.Trim();

                // -------------------------------------------------
                // Проверка длины названия
                // -------------------------------------------------

                if (nazvanie.Length > 50)
                {
                    MessageBox.Show(
                        "Название группы не должно превышать 50 символов!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    NazvanieTextBox.Focus();
                    return;
                }

                // -------------------------------------------------
                // Проверка курса (ComboBox)
                // -------------------------------------------------

                if (KursComboBox.SelectedValue == null)
                {
                    MessageBox.Show(
                        "Выберите курс обучения!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    KursComboBox.Focus();
                    return;
                }

                int kurs = Convert.ToInt32(KursComboBox.SelectedValue);

                // -------------------------------------------------
                // Получаем ID отделения
                // -------------------------------------------------

                int? idOtdelenie = null;

                if (OtdelenieComboBox.SelectedValue != null &&
                    OtdelenieComboBox.SelectedValue != DBNull.Value)
                {
                    idOtdelenie = Convert.ToInt32(OtdelenieComboBox.SelectedValue);
                }

                // -------------------------------------------------
                // Проверка уникальности названия
                // -------------------------------------------------

                string checkSql = @"
                    SELECT COUNT(*)
                    FROM UP03_Groups
                    WHERE Nazvanie = @Nazvanie";

                using (SqlCommand checkCmd = new SqlCommand(checkSql, connection))
                {
                    checkCmd.Parameters.Add("@Nazvanie", SqlDbType.VarChar, 50).Value = nazvanie;

                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count > 0)
                    {
                        MessageBox.Show(
                            "Группа с таким названием уже существует!",
                            "Предупреждение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);

                        return;
                    }
                }

                // -------------------------------------------------
                // INSERT
                // -------------------------------------------------

                string insertSql = @"
                    INSERT INTO UP03_Groups
                    (
                        Nazvanie,
                        Kurs,
                        IDOtdelenie
                    )
                    VALUES
                    (
                        @Nazvanie,
                        @Kurs,
                        @IDOtdelenie
                    )";

                using (SqlCommand insertCmd = new SqlCommand(insertSql, connection))
                {
                    insertCmd.Parameters.Add("@Nazvanie", SqlDbType.VarChar, 50).Value = nazvanie;
                    insertCmd.Parameters.Add("@Kurs", SqlDbType.Int).Value = kurs;

                    if (idOtdelenie.HasValue)
                    {
                        insertCmd.Parameters.Add("@IDOtdelenie", SqlDbType.Int).Value = idOtdelenie.Value;
                    }
                    else
                    {
                        insertCmd.Parameters.Add("@IDOtdelenie", SqlDbType.Int).Value = DBNull.Value;
                    }

                    insertCmd.ExecuteNonQuery();
                }

                MessageBox.Show(
                    "Группа успешно добавлена!",
                    "Успех!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RefreshDataGrid();
                ClearFields();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при добавлении:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ИЗМЕНЕНИЕ ГРУППЫ
        // =========================================================

        private void ChangeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (dgGroups.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите группу для редактирования.",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);

                return;
            }

            try
            {
                // -------------------------------------------------
                // Получаем выбранную строку
                // -------------------------------------------------

                DataRowView row = dgGroups.SelectedItem as DataRowView;
                if (row == null)
                    return;

                int currentId = Convert.ToInt32(row["IDGroup"]);

                // -------------------------------------------------
                // Проверка названия
                // -------------------------------------------------

                if (string.IsNullOrWhiteSpace(NazvanieTextBox.Text))
                {
                    MessageBox.Show(
                        "Введите название группы!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    NazvanieTextBox.Focus();
                    return;
                }

                string nazvanie = NazvanieTextBox.Text.Trim();

                // -------------------------------------------------
                // Проверка длины
                // -------------------------------------------------

                if (nazvanie.Length > 50)
                {
                    MessageBox.Show(
                        "Название группы не должно превышать 50 символов!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    NazvanieTextBox.Focus();
                    return;
                }

                // -------------------------------------------------
                // Проверка курса (ComboBox)
                // -------------------------------------------------

                if (KursComboBox.SelectedValue == null)
                {
                    MessageBox.Show(
                        "Выберите курс обучения!",
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);

                    KursComboBox.Focus();
                    return;
                }

                int kurs = Convert.ToInt32(KursComboBox.SelectedValue);

                // -------------------------------------------------
                // Получаем выбранное отделение
                // -------------------------------------------------

                int? idOtdelenie = null;

                if (OtdelenieComboBox.SelectedValue != null &&
                    OtdelenieComboBox.SelectedValue != DBNull.Value)
                {
                    idOtdelenie = Convert.ToInt32(OtdelenieComboBox.SelectedValue);
                }

                // -------------------------------------------------
                // Проверка уникальности названия
                // -------------------------------------------------

                string checkSql = @"
                    SELECT COUNT(*)
                    FROM UP03_Groups
                    WHERE Nazvanie = @Nazvanie
                    AND IDGroup <> @IDGroup";

                using (SqlCommand checkCmd = new SqlCommand(checkSql, connection))
                {
                    checkCmd.Parameters.Add("@Nazvanie", SqlDbType.VarChar, 50).Value = nazvanie;
                    checkCmd.Parameters.Add("@IDGroup", SqlDbType.Int).Value = currentId;

                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count > 0)
                    {
                        MessageBox.Show(
                            "Группа с таким названием уже существует!",
                            "Предупреждение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation);

                        return;
                    }
                }

                // -------------------------------------------------
                // UPDATE
                // -------------------------------------------------

                string updateSql = @"
                    UPDATE UP03_Groups
                    SET
                        Nazvanie = @Nazvanie,
                        Kurs = @Kurs,
                        IDOtdelenie = @IDOtdelenie
                    WHERE
                        IDGroup = @IDGroup";

                using (SqlCommand updateCmd = new SqlCommand(updateSql, connection))
                {
                    updateCmd.Parameters.Add("@Nazvanie", SqlDbType.VarChar, 50).Value = nazvanie;
                    updateCmd.Parameters.Add("@Kurs", SqlDbType.Int).Value = kurs;

                    if (idOtdelenie.HasValue)
                    {
                        updateCmd.Parameters.Add("@IDOtdelenie", SqlDbType.Int).Value = idOtdelenie.Value;
                    }
                    else
                    {
                        updateCmd.Parameters.Add("@IDOtdelenie", SqlDbType.Int).Value = DBNull.Value;
                    }

                    updateCmd.Parameters.Add("@IDGroup", SqlDbType.Int).Value = currentId;

                    int affectedRows = updateCmd.ExecuteNonQuery();

                    if (affectedRows == 0)
                    {
                        MessageBox.Show(
                            "Запись не была изменена.",
                            "Предупреждение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }
                }

                MessageBox.Show(
                    "Данные группы обновлены!",
                    "Успех!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RefreshDataGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при редактировании:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // УДАЛЕНИЕ ГРУППЫ
        // =========================================================

        private void DeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (dgGroups.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите группу для удаления.",
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);

                return;
            }

            try
            {
                DataRowView selectedRow = dgGroups.SelectedItem as DataRowView;
                if (selectedRow == null)
                    return;

                int idGroup = Convert.ToInt32(selectedRow["IDGroup"]);

                string nazvanie =
                    selectedRow["Nazvanie"] == DBNull.Value
                        ? ""
                        : selectedRow["Nazvanie"].ToString();

                // -------------------------------------------------
                // Проверяем наличие студентов
                // -------------------------------------------------

                string checkStudentsSql = @"
                    SELECT COUNT(*)
                    FROM UP03_StudentGroup
                    WHERE IDGroup = @IDGroup";

                using (SqlCommand checkCmd = new SqlCommand(checkStudentsSql, connection))
                {
                    checkCmd.Parameters.Add("@IDGroup", SqlDbType.Int).Value = idGroup;

                    int studentCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (studentCount > 0)
                    {
                        MessageBox.Show(
                            $"Невозможно удалить группу «{nazvanie}».\n\n" +
                            $"В ней числится студентов: {studentCount}.\n" +
                            "Сначала отчислите всех студентов из группы.",
                            "Предупреждение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }
                }

                // -------------------------------------------------
                // Подтверждение
                // -------------------------------------------------

                MessageBoxResult result =
                    MessageBox.Show(
                        $"Вы уверены, что хотите удалить группу «{nazvanie}»?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // -------------------------------------------------
                // DELETE
                // -------------------------------------------------

                string deleteSql = @"
                    DELETE FROM UP03_Groups
                    WHERE IDGroup = @IDGroup";

                using (SqlCommand deleteCmd = new SqlCommand(deleteSql, connection))
                {
                    deleteCmd.Parameters.Add("@IDGroup", SqlDbType.Int).Value = idGroup;

                    int affectedRows = deleteCmd.ExecuteNonQuery();

                    if (affectedRows == 0)
                    {
                        MessageBox.Show(
                            "Группа не была удалена.",
                            "Предупреждение",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }
                }

                MessageBox.Show(
                    "Группа удалена!",
                    "Успех!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                RefreshDataGrid();
                ClearFields();
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    $"Ошибка базы данных при удалении:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при удалении:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ЗАЧИСЛЕНИЕ СТУДЕНТА В ГРУППУ
        // =========================================================

        private void EnrollStudentButton_Click(object sender, RoutedEventArgs e)
        {
            AddStudentToGroupWindow dialog = new AddStudentToGroupWindow();
            dialog.Owner = this;
            dialog.ShowDialog();
        }


        // =========================================================
        // ВЫХОД
        // =========================================================

        private void ExitButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            AdminMainWindow adminWindow = new AdminMainWindow();
            adminWindow.Show();
            Close();
        }


        // =========================================================
        // ОБНОВЛЕНИЕ DATAGRID
        // =========================================================

        private void RefreshDataGrid()
        {
            try
            {
                dataTable.Clear();
                adapter.Fill(dataTable);
                dgGroups.ItemsSource = dataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка обновления данных:\n{ex.Message}",
                    "Ошибка!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        // =========================================================
        // ОЧИСТКА ПОЛЕЙ
        // =========================================================

        private void ClearFields()
        {
            IDGroupTextBox.Clear();
            NazvanieTextBox.Clear();
            KursComboBox.SelectedIndex = -1;
            OtdelenieComboBox.SelectedIndex = -1;
        }


        // =========================================================
        // ЗАКРЫТИЕ СОЕДИНЕНИЯ
        // =========================================================

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (connection != null)
            {
                if (connection.State != ConnectionState.Closed)
                {
                    connection.Close();
                }

                connection.Dispose();
            }
        }
    }
}