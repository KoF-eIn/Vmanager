using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class PaymentsViewModel : ObservableObject
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly IReadingRepository _readingRepository;
        private readonly IReceiptPrintService _receiptPrintService;
        private readonly ICsvExportService _csvExportService;
        private readonly ILoggerService _logger;
        private readonly User _currentUser;

        private ObservableCollection<Payment> _payments = new ObservableCollection<Payment>();
        private ObservableCollection<Subscriber> _subscribers = new ObservableCollection<Subscriber>();
        private Payment _selectedPayment;
        private Subscriber _selectedFilterSubscriber;

        private DateTime? _filterFromDate;
        private DateTime? _filterToDate;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<Payment> Payments
        {
            get => _payments;
            set => SetProperty(ref _payments, value);
        }

        public ObservableCollection<Subscriber> Subscribers
        {
            get => _subscribers;
            set => SetProperty(ref _subscribers, value);
        }

        public Payment SelectedPayment
        {
            get => _selectedPayment;
            set => SetProperty(ref _selectedPayment, value);
        }

        public Subscriber SelectedFilterSubscriber
        {
            get => _selectedFilterSubscriber;
            set
            {
                if (SetProperty(ref _selectedFilterSubscriber, value))
                {
                    _ = LoadPaymentsAsync();
                }
            }
        }

        public DateTime? FilterFromDate
        {
            get => _filterFromDate;
            set
            {
                if (SetProperty(ref _filterFromDate, value))
                {
                    _ = LoadPaymentsAsync();
                }
            }
        }

        public DateTime? FilterToDate
        {
            get => _filterToDate;
            set
            {
                if (SetProperty(ref _filterToDate, value))
                {
                    _ = LoadPaymentsAsync();
                }
            }
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

        public bool CanManage => _currentUser != null && (_currentUser.IsAdmin || _currentUser.IsCashier);

        public ICommand RefreshCommand { get; }
        public ICommand RegisterPaymentCommand { get; }
        public ICommand PrintReceiptCommand { get; }
        public ICommand DeletePaymentCommand { get; }

        public Func<Subscriber, Task<bool>> OpenNewPaymentDialogAsync { get; set; }
        public Action<System.Windows.Documents.FlowDocument> OpenReceiptPreview { get; set; }

        public PaymentsViewModel(
            IPaymentRepository paymentRepository,
            ISubscriberRepository subscriberRepository,
            IReadingRepository readingRepository,
            IReceiptPrintService receiptPrintService,
            ICsvExportService csvExportService,
            ILoggerService logger,
            User currentUser)
        {
            _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
            _subscriberRepository = subscriberRepository ?? throw new ArgumentNullException(nameof(subscriberRepository));
            _readingRepository = readingRepository ?? throw new ArgumentNullException(nameof(readingRepository));
            _receiptPrintService = receiptPrintService ?? throw new ArgumentNullException(nameof(receiptPrintService));
            _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser;

            RefreshCommand = new AsyncRelayCommand(LoadAllDataAsync);
            RegisterPaymentCommand = new AsyncRelayCommand(RegisterPaymentAsync, () => CanManage);
            PrintReceiptCommand = new AsyncRelayCommand(PrintReceiptAsync, () => SelectedPayment != null || SelectedFilterSubscriber != null);
            DeletePaymentCommand = new AsyncRelayCommand(DeletePaymentAsync, () => CanManage && SelectedPayment != null);

            _ = LoadAllDataAsync();
        }

        public async Task LoadAllDataAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var subs = await Task.Run(() => _subscriberRepository.GetAll());
                Subscribers = new ObservableCollection<Subscriber>(subs);

                await LoadPaymentsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки данных платежей: " + ex.Message;
                _logger.LogError("Ошибка загрузки платежей", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadPaymentsAsync()
        {
            try
            {
                int? subId = SelectedFilterSubscriber?.Id;
                DateTime? from = FilterFromDate;
                DateTime? to = FilterToDate;

                var list = await Task.Run(() => _paymentRepository.GetAll(subId, from, to));
                Payments = new ObservableCollection<Payment>(list);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка выборки платежей: " + ex.Message;
            }
        }

        private async Task RegisterPaymentAsync()
        {
            if (OpenNewPaymentDialogAsync == null) return;

            bool saved = await OpenNewPaymentDialogAsync(SelectedFilterSubscriber);
            if (saved)
            {
                StatusMessage = "Платеж успешно принят и проведен в кассе.";
                await LoadAllDataAsync();
            }
        }

        private async Task PrintReceiptAsync()
        {
            Subscriber sub = null;
            DateTime period = DateTime.Today;

            if (SelectedPayment != null)
            {
                sub = await Task.Run(() => _subscriberRepository.GetById(SelectedPayment.SubscriberId));
                period = SelectedPayment.PaymentDate;
            }
            else if (SelectedFilterSubscriber != null)
            {
                sub = await Task.Run(() => _subscriberRepository.GetById(SelectedFilterSubscriber.Id));
            }

            if (sub == null)
            {
                ErrorMessage = "Для формирования квитанции выберите платеж или абонента.";
                return;
            }

            try
            {
                DateTime periodMonth = new DateTime(period.Year, period.Month, 1);
                var readings = await Task.Run(() => _readingRepository.GetAll(sub.Id, null, periodMonth));
                decimal paymentsSum = await Task.Run(() => _paymentRepository.GetPaymentsSumForPeriod(
                    sub.Id, periodMonth, periodMonth.AddMonths(1).AddDays(-1)));

                var doc = _receiptPrintService.CreateReceiptDocument(sub, periodMonth, readings, paymentsSum);

                if (OpenReceiptPreview != null)
                {
                    OpenReceiptPreview(doc);
                }
                else
                {
                    _receiptPrintService.PrintReceipt(doc);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка формирования квитанции: " + ex.Message;
                _logger.LogError("Ошибка формирования квитанции", ex);
            }
        }
        private async Task DeletePaymentAsync()
        {
            if (SelectedPayment == null)
            {
                ErrorMessage = "Выберите платёж для удаления.";
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                $"Вы действительно хотите отменить и удалить платёж №{SelectedPayment.Id} на сумму {SelectedPayment.AmountRub:N2} ₽ от абонента '{SelectedPayment.SubscriberName}'?",
                "Подтверждение удаления платежа",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                int payId = SelectedPayment.Id;
                await System.Threading.Tasks.Task.Run(() => _paymentRepository.DeletePayment(payId));
                SelectedPayment = null;
                StatusMessage = "Платёж успешно отменён и удалён.";
                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка удаления платежа: " + ex.Message;
                _logger.LogError("Ошибка удаления платежа", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}