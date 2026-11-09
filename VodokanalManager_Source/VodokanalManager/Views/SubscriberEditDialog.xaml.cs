using System;
using System.Windows;
using System.Windows.Controls;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;

namespace VodokanalManager.Views
{
    public partial class SubscriberEditDialog : Window
    {
        private readonly Subscriber _subscriber;
        private readonly IDatabase _database;
        private readonly IPasswordHasher _hasher;

        public SubscriberEditDialog(Subscriber subscriber, IDatabase database = null, IPasswordHasher hasher = null)
        {
            InitializeComponent();
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));

            _database = database ?? ((Application.Current as App)?.Database ?? new SqliteDatabase());
            _hasher = hasher ?? new PasswordHasher();

            DialogTitleText.Text = _subscriber.Id > 0 ? $"Редактирование абонента (л/с {_subscriber.AccountNo})" : "Добавление нового абонента";

            AccountNoTextBox.Text = _subscriber.AccountNo;
            AccountNoTextBox.IsEnabled = _subscriber.Id == 0; 
            NameTextBox.Text = _subscriber.Name;
            AddressTextBox.Text = _subscriber.Address;
            PhoneTextBox.Text = _subscriber.Phone;
            IsActiveCheckBox.IsChecked = _subscriber.IsActive;

            SubscriberTypeComboBox.SelectedIndex = _subscriber.SubscriberTypeId == 2 ? 1 : 0;

            if (_subscriber.Id == 0)
            {
                PasswordLabelText.Text = "Первичный пароль для входа в ЛК:";
                PasswordTextBox.Text = PasswordGenerator.Generate(8);
            }
            else
            {
                PasswordLabelText.Text = "Пароль для входа в ЛК (можно изменить):";
                try
                {
                    var userRepo = new UserRepository(_database);
                    var user = userRepo.GetBySubscriberId(_subscriber.Id);
                    if (user != null && !string.IsNullOrWhiteSpace(user.InitialPassword))
                    {
                        PasswordTextBox.Text = user.InitialPassword;
                    }
                }
                catch
                {
                    
                }
            }
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            PasswordTextBox.Text = PasswordGenerator.Generate(8);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            string acc = AccountNoTextBox.Text?.Trim();
            string name = NameTextBox.Text?.Trim();
            string addr = AddressTextBox.Text?.Trim();
            string phone = PhoneTextBox.Text?.Trim();
            string password = PasswordTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(acc))
            {
                ShowError("Укажите номер лицевого счёта.");
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Укажите ФИО абонента или наименование организации.");
                return;
            }

            if (string.IsNullOrWhiteSpace(addr))
            {
                ShowError("Укажите адрес объекта водоснабжения.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Укажите пароль для доступа абонента в личный кабинет.");
                return;
            }

            int typeId = 1;
            if (SubscriberTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                typeId = Convert.ToInt32(item.Tag);
            }

            _subscriber.AccountNo = acc;
            _subscriber.Name = name;
            _subscriber.SubscriberTypeId = typeId;
            _subscriber.Address = addr;
            _subscriber.Phone = phone;
            _subscriber.IsActive = IsActiveCheckBox.IsChecked == true;

            try
            {
                var subRepo = new SubscriberRepository(_database);

                if (_subscriber.Id == 0)
                {
                    subRepo.Add(_subscriber, _hasher, password);
                }
                else
                {
                    subRepo.Update(_subscriber, password, _hasher);
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка сохранения: " + ex.Message);
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
