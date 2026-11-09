using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;

namespace VodokanalManager.Views
{
    public partial class ReadingEditDialog : Window
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private readonly Meter _meter;
        private readonly IDatabase _database;
        private readonly IReadingRepository _readingRepository;
        private readonly ITariffRepository _tariffRepository;

        private long _prevValueL = 0;
        private bool _hasPrevReading = false;

        public ReadingEditDialog(Meter meter)
        {
            InitializeComponent();
            _meter = meter ?? throw new ArgumentNullException(nameof(meter));

            _database = new SqliteDatabase();
            _readingRepository = new ReadingRepository(_database);
            _tariffRepository = new TariffRepository(_database);

            SubscriberInfoText.Text = $"Абонент: {_meter.SubscriberName} (л/с {_meter.SubscriberAccountNo})";
            MeterInfoText.Text = $"Прибор: {_meter.SerialNo} • {_meter.ServiceTypeName}";

var lastReading = _readingRepository.GetLastReading(_meter.Id);
            if (lastReading != null)
            {
                _hasPrevReading = true;
                _prevValueL = lastReading.ValueL;
                PrevReadingText.Text = $"Предыдущее показание: {lastReading.ValueM3:N3} м³ (за {lastReading.Period:MM.yyyy})";
                PeriodDatePicker.SelectedDate = lastReading.Period.AddMonths(1);
            }
            else
            {
                _hasPrevReading = false;
                _prevValueL = 0;
                PrevReadingText.Text = "Первичное показание (расход по нему не начисляется)";
                PeriodDatePicker.SelectedDate = DateTime.Today;
            }

            Recalculate();
        }

        private void PeriodDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            Recalculate();
        }

        private void CurrentValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Recalculate();
        }

        private void Recalculate()
        {
            if (ConsumptionCalcText == null) return;

            string input = CurrentValueTextBox.Text?.Replace(',', '.').Trim();
            if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal currentM3) || currentM3 < 0)
            {
                ConsumptionCalcText.Text = "Расход: 0,000 м³";
                AmountCalcText.Text = "0,00 ₽";
                return;
            }

            long currentL = (long)Math.Round(currentM3 * 1000m);
            long consumptionL = 0;

            if (_hasPrevReading)
            {
                consumptionL = Math.Max(0, currentL - _prevValueL);
                ConsumptionCalcText.Text = $"Расход за месяц: {consumptionL / 1000m:N3} м³ ({consumptionL} л)";
            }
            else
            {
                ConsumptionCalcText.Text = "Первичное показание (расход = 0,000 м³)";
            }

DateTime period = PeriodDatePicker.SelectedDate ?? DateTime.Today;
            var tariff = _tariffRepository.GetActiveTariff(_meter.ServiceTypeId, period);

            if (tariff != null)
            {
                TariffCalcText.Text = $"Действующий тариф: {tariff.PriceRubPerM3:N2} ₽/м³";
                long amountKop = (consumptionL * tariff.PriceKopPerM3 + 500) / 1000;
                AmountCalcText.Text = $"{amountKop / 100m:N2} ₽";
            }
            else
            {
                TariffCalcText.Text = "Тариф на выбранный период не найден!";
                AmountCalcText.Text = "0,00 ₽";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorBorder.Visibility = Visibility.Collapsed;

            if (!PeriodDatePicker.SelectedDate.HasValue)
            {
                ShowError("Выберите расчётный период.");
                return;
            }

            string input = CurrentValueTextBox.Text?.Replace(',', '.').Trim();
            if (!decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal currentM3) || currentM3 < 0)
            {
                ShowError("Введите корректное числовое значение показания в м³.");
                return;
            }

            long currentL = (long)Math.Round(currentM3 * 1000m);

            try
            {
                _readingRepository.AddReading(_meter.Id, PeriodDatePicker.SelectedDate.Value, currentL, null);
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
