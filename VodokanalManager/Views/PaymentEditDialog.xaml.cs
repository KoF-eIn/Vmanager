using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;

namespace VodokanalManager.Views
{
    public partial class PaymentEditDialog : Window
    {
        private readonly Subscriber _defaultSubscriber;
        private readonly IDatabase _database;
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly IPaymentRepository _paymentRepository;

        public PaymentEditDialog(Subscriber defaultSubscriber = null)
        {
            InitializeComponent();
            _defaultSubscriber = defaultSubscriber;

            _database = new SqliteDatabase();
            _subscriberRepository = new SubscriberRepository(_database);
            _paymentRepository = new PaymentRepository(_database);

            PaymentDatePicker.SelectedDate = DateTime.Today;

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

        private void SubscriberComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SubscriberComboBox.SelectedItem is Subscriber sub)
            {
                AccountInfoText.Text = $"Лицевой счёт: {sub.AccountNo}";
                AddressInfoText.Text = $"Адрес: {sub.Address}";
                DebtInfoText.Text = $"{sub.DebtRub:N2} ₽";

                if (sub.DebtRub > 0)
                {
                    AmountTextBox.Text = sub.DebtRub.ToString("F2", CultureInfo.InvariantCulture);
                }
                else
                {
                    AmountTextBox.Text = "500.00";
                }
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

            string input = AmountTextBox.Text?.Replace(',', '.').Trim();
            if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amountRub) || amountRub <= 0)
            {
                ShowError("Введите корректную сумму платежа больше 0 ₽.");
                return;
            }

            int methodId = 1;
            if (PaymentMethodComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                methodId = Convert.ToInt32(item.Tag);
            }

            DateTime payDate = PaymentDatePicker.SelectedDate ?? DateTime.Today;
            long amountKop = (long)Math.Round(amountRub * 100m);

            var payment = new Payment
            {
                SubscriberId = sub.Id,
                PaymentDate = payDate,
                AmountKop = amountKop,
                PaymentMethodId = methodId
            };

            try
            {
                _paymentRepository.AddPayment(payment);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError("Ошибка проведения платежа: " + ex.Message);
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
