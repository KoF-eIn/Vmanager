using System;
using System.Windows;
using System.Windows.Controls;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;

namespace VodokanalManager.Views
{
    public partial class UserEditDialog : Window
    {
        private readonly User _user;
        private readonly IDatabase _database;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UserEditDialog(User user, IDatabase database = null, IPasswordHasher hasher = null)
        {
            InitializeComponent();
            _user = user ?? throw new ArgumentNullException(nameof(user));

            _database = database ?? ((Application.Current as App)?.Database ?? new SqliteDatabase());
            _userRepository = new UserRepository(_database);
            _passwordHasher = hasher ?? new PasswordHasher();

            DialogTitleText.Text = _user.Id > 0 ? $"Редактирование учётной записи '{_user.Login}'" : "Добавление нового пользователя";

            LoginTextBox.Text = _user.Login;
            FullNameTextBox.Text = _user.FullName;
            IsActiveCheckBox.IsChecked = _user.IsActive;

PasswordTextBox.Text = !string.IsNullOrWhiteSpace(_user.InitialPassword) ? _user.InitialPassword : string.Empty;

            if (_user.Id > 0)
            {
                PasswordLabelText.Text = "Пароль для входа (можно изменить):";
                SetSelectedRole(_user.RoleId);
                if (_user.RoleId == 4)
                {
                    RoleComboBox.IsEnabled = false; 
                }
            }
            else
            {
                PasswordLabelText.Text = "Первичный пароль для входа:";
                if (string.IsNullOrWhiteSpace(PasswordTextBox.Text))
                {
                    PasswordTextBox.Text = PasswordGenerator.Generate(8);
                }
                SetSelectedRole(_user.RoleId);
            }
        }

        private void SetSelectedRole(int roleId)
        {
            switch (roleId)
            {
                case 1: RoleComboBox.SelectedIndex = 0; break;
                case 2: RoleComboBox.SelectedIndex = 1; break;
                case 3: RoleComboBox.SelectedIndex = 2; break;
                case 4: RoleComboBox.SelectedIndex = 3; break;
                default: RoleComboBox.SelectedIndex = 1; break;
            }
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            PasswordTextBox.Text = PasswordGenerator.Generate(8);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            string login = LoginTextBox.Text?.Trim();
            string fullName = FullNameTextBox.Text?.Trim();
            string password = PasswordTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Укажите логин пользователя.");
                return;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ShowError("Укажите ФИО пользователя или наименование абонента.");
                return;
            }

            if (_user.Id == 0 && string.IsNullOrWhiteSpace(password))
            {
                ShowError("Укажите первичный пароль для пользователя.");
                return;
            }

            int roleId = _user.RoleId;
            if (RoleComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                roleId = Convert.ToInt32(item.Tag);
            }

            _user.Login = login;
            _user.FullName = fullName;
            _user.RoleId = roleId;
            _user.IsActive = IsActiveCheckBox.IsChecked == true;

            try
            {
                if (_user.Id == 0)
                {
                    _userRepository.AddUser(_user, password, _passwordHasher);
                }
                else
                {
                    _userRepository.UpdateUser(_user, password, _passwordHasher);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }
    }
}
