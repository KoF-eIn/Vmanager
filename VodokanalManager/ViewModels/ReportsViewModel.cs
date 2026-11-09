using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class ReportsViewModel : ObservableObject
    {
        private readonly IReportRepository _reportRepository;
        private readonly ICsvExportService _csvExportService;
        private readonly ILoggerService _logger;

        private int _selectedReportIndex = 0; 
        private DateTime _fromDate = DateTime.Today.AddMonths(-6);
        private DateTime _toDate = DateTime.Today;

        private ObservableCollection<DebtorReportItem> _debtorItems = new ObservableCollection<DebtorReportItem>();
        private ObservableCollection<SuspiciousConsumptionItem> _suspiciousItems = new ObservableCollection<SuspiciousConsumptionItem>();
        private ObservableCollection<MonthlyConsumptionItem> _monthlyItems = new ObservableCollection<MonthlyConsumptionItem>();
        private ObservableCollection<PaymentSummaryItem> _paymentSummaryItems = new ObservableCollection<PaymentSummaryItem>();

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public int SelectedReportIndex
        {
            get => _selectedReportIndex;
            set
            {
                if (SetProperty(ref _selectedReportIndex, value))
                {
                    OnPropertyChanged(nameof(IsDebtorsReportVisible));
                    OnPropertyChanged(nameof(IsSuspiciousReportVisible));
                    OnPropertyChanged(nameof(IsConsumptionReportVisible));
                    OnPropertyChanged(nameof(IsPaymentSummaryVisible));
                    _ = LoadReportAsync();
                }
            }
        }

        public bool IsDebtorsReportVisible => SelectedReportIndex == 0;
        public bool IsSuspiciousReportVisible => SelectedReportIndex == 1;
        public bool IsConsumptionReportVisible => SelectedReportIndex == 2;
        public bool IsPaymentSummaryVisible => SelectedReportIndex == 3;

        public DateTime FromDate
        {
            get => _fromDate;
            set => SetProperty(ref _fromDate, value);
        }

        public DateTime ToDate
        {
            get => _toDate;
            set => SetProperty(ref _toDate, value);
        }

        public ObservableCollection<DebtorReportItem> DebtorItems
        {
            get => _debtorItems;
            set => SetProperty(ref _debtorItems, value);
        }

        public ObservableCollection<SuspiciousConsumptionItem> SuspiciousItems
        {
            get => _suspiciousItems;
            set => SetProperty(ref _suspiciousItems, value);
        }

        public ObservableCollection<MonthlyConsumptionItem> MonthlyItems
        {
            get => _monthlyItems;
            set => SetProperty(ref _monthlyItems, value);
        }

        public ObservableCollection<PaymentSummaryItem> PaymentSummaryItems
        {
            get => _paymentSummaryItems;
            set => SetProperty(ref _paymentSummaryItems, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand LoadReportCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public ReportsViewModel(
            IReportRepository reportRepository,
            ICsvExportService csvExportService,
            ILoggerService logger)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
            _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LoadReportCommand = new AsyncRelayCommand(LoadReportAsync);
            ExportCsvCommand = new RelayCommand(ExportCsv);

            _ = LoadReportAsync();
        }

        public async Task LoadReportAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                switch (SelectedReportIndex)
                {
                    case 0:
                        var debtors = await Task.Run(() => _reportRepository.GetDebtorsReport());
                        DebtorItems = new ObservableCollection<DebtorReportItem>(debtors);
                        StatusMessage = $"Сформирован реестр должников: {debtors.Count} абонентов.";
                        break;

                    case 1:
                        var suspicious = await Task.Run(() => _reportRepository.GetSuspiciousConsumptionReport());
                        SuspiciousItems = new ObservableCollection<SuspiciousConsumptionItem>(suspicious);
                        StatusMessage = $"Выявлено аномальных всплесков потребления: {suspicious.Count}.";
                        break;

                    case 2:
                        var monthly = await Task.Run(() => _reportRepository.GetMonthlyConsumptionReport(FromDate, ToDate));
                        MonthlyItems = new ObservableCollection<MonthlyConsumptionItem>(monthly);
                        StatusMessage = $"Сформирована помесячная динамика потребления за {monthly.Count} месяцев.";
                        break;

                    case 3:
                        var payments = await Task.Run(() => _reportRepository.GetPaymentSummaryReport(FromDate, ToDate));
                        PaymentSummaryItems = new ObservableCollection<PaymentSummaryItem>(payments);
                        StatusMessage = "Сформирован сводный отчет по способам оплаты.";
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка построения отчёта: " + ex.Message;
                _logger.LogError("Ошибка формирования отчёта", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExportCsv()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Файлы CSV (*.csv)|*.csv",
                FileName = $"Otchet_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                switch (SelectedReportIndex)
                {
                    case 0:
                        _csvExportService.ExportToCsv(
                            sfd.FileName,
                            DebtorItems,
                            d => new[] { d.Rank.ToString(), d.AccountNo, d.Name, d.SubscriberTypeName, d.Address, d.Phone, d.DebtRub.ToString("F2"), d.TotalChargedRub.ToString("F2"), d.TotalPaidRub.ToString("F2"), d.LastPaymentDateText },
                            new[] { "Ранг", "Лицевой счет", "Абонент", "Тип", "Адрес", "Телефон", "Долг, руб.", "Всего начислено, руб.", "Всего оплачено, руб.", "Дата последней оплаты" });
                        break;

                    case 1:
                        _csvExportService.ExportToCsv(
                            sfd.FileName,
                            SuspiciousItems,
                            s => new[] { s.AccountNo, s.SubscriberName, s.ServiceTypeName, s.MeterSerialNo, s.PeriodText, s.ConsumptionM3.ToString("F3"), s.AvgPrev3MonthsM3.ToString("F3"), s.Ratio.ToString("F2"), s.Note },
                            new[] { "Лицевой счет", "Абонент", "Услуга", "Счетчик", "Период", "Расход, м3", "Средний за 3 мес., м3", "Коэффициент", "Примечание" });
                        break;

                    case 2:
                        _csvExportService.ExportToCsv(
                            sfd.FileName,
                            MonthlyItems,
                            m => new[] { m.PeriodName, m.ColdWaterM3.ToString("F3"), m.HotWaterM3.ToString("F3"), m.TotalM3.ToString("F3") },
                            new[] { "Расчетный период", "ХВС, м3", "ГВС, м3", "Всего, м3" });
                        break;

                    case 3:
                        _csvExportService.ExportToCsv(
                            sfd.FileName,
                            PaymentSummaryItems,
                            p => new[] { p.PaymentMethodName, p.PaymentsCount.ToString(), p.TotalAmountRub.ToString("F2") },
                            new[] { "Способ оплаты", "Количество платежей", "Сумма, руб." });
                        break;
                }

                StatusMessage = $"Данные успешно экспортированы в файл: {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка экспорта в CSV: " + ex.Message;
                _logger.LogError("Ошибка экспорта CSV", ex);
            }
        }
    }
}
