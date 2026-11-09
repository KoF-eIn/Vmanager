using System;
using System.Windows;
using System.Windows.Controls;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;

namespace VodokanalManager.Views
{
    public partial class MeterEditDialog : Window
    {
        private readonly Subscriber _defaultSubscriber;
        private readonly IDatabase _database;
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly IMeterRepository _meterRepository;

        public MeterEditDialog(Subscriber defaultSubscriber = null)
        {
            InitializeComponent();
            _defaultSubscriber = defaultSubscriber;

            _database = new SqliteDatabase();
            _subscriberRepository = new SubscriberRepository(_database);
            _meterRepository = new MeterRepository(_database);

            InstallDatePicker.SelectedDate = DateTime.Today;

            LoadSubscribers();
        }

        private void LoadSubscribers()
        {
            var list = _subscriberRepository.GetAll();
            SubscriberComboBox.ItemsSource = list;

            if (_defaultSubscriber != null)
            {
                var match = list.Find(s => s.Id == _defaultSubscriber.Id);
                SubscriberComboBox.SelectedItem = match;
            }
            else if (list.Count > 0)
            {
                SubscriberComboBox.SelectedIndex = 0;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            if (!(SubscriberComboBox.SelectedItem is Subscriber sub))
            {
                ShowError("Выберите абонента.");
                return;
            }

            string serial = SerialNoTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(serial))
            {
                ShowError("Укажите серийный номер прибора учёта.");
                return;
            }

            int serviceId = 1;
            if (ServiceTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                serviceId = Convert.ToInt32(item.Tag);
            }

            DateTime installDate = InstallDatePicker.SelectedDate ?? DateTime.Today;

            var meter = new Meter
            {
                SubscriberId = sub.Id,
                ServiceTypeId = serviceId,
                SerialNo = serial,
                InstallDate = installDate,
                IsActive = true
            };

            try
            {
                _meterRepository.Add(meter);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка регистрации прибора: " + ex.Message);
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
