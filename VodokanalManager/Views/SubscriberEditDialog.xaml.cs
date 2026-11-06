using System;
using System.Windows;
using System.Windows.Controls;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;

namespace VodokanalManager.Views
{
    public partial class SubscriberEditDialog : Window
    {
        private readonly Subscriber _subscriber;

        public SubscriberEditDialog(Subscriber subscriber)
        {
            InitializeComponent();
            _subscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));

            DialogTitleText.Text = _subscriber.Id > 0 ? "Редактирование данных абонента" : "Добавление нового абонента";

            AccountNoTextBox.Text = _subscriber.AccountNo;
            AccountNoTextBox.IsEnabled = _subscriber.Id == 0; // Номер л/с неизменен после создания
            NameTextBox.Text = _subscriber.Name;
            AddressTextBox.Text = _subscriber.Address;
            PhoneTextBox.Text = _subscriber.Phone;
            IsActiveCheckBox.IsChecked = _subscriber.IsActive;

            SubscriberTypeComboBox.SelectedIndex = _subscriber.SubscriberTypeId == 2 ? 1 : 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            string acc = AccountNoTextBox.Text?.Trim();
            string name = NameTextBox.Text?.Trim();
            string addr = AddressTextBox.Text?.Trim();
            string phone = PhoneTextBox.Text?.Trim();

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
                // Сохранение в БД
                var app = Application.Current as App;
                var db = new Data.SqliteDatabase();
                var subRepo = new SubscriberRepository(db);
                var hasher = new PasswordHasher();

                if (_subscriber.Id == 0)
                {
                    subRepo.Add(_subscriber, hasher, "user");
                }
                else
                {
                    subRepo.Update(_subscriber);
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
