using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class SubscriberPortalViewModel : ObservableObject
    {
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly IMeterRepository _meterRepository;
        private readonly IReadingRepository _readingRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IReceiptPrintService _receiptPrintService;
        private readonly ILoggerService _logger;
        private readonly User _currentUser;

        private Subscriber _subscriber;
        private ObservableCollection<Meter> _myMeters = new ObservableCollection<Meter>();
        private ObservableCollection<MeterReading> _myReadings = new ObservableCollection<MeterReading>();
        private ObservableCollection<Payment> _myPayments = new ObservableCollection<Payment>();

        private Meter _selectedMeterForReading;
        private decimal _inputReadingM3;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public Subscriber Subscriber
        {
            get => _subscriber;
            set => SetProperty(ref _subscriber, value);
        }

        public ObservableCollection<Meter> MyMeters
        {
            get => _myMeters;
            set => SetProperty(ref _myMeters, value);
        }

        public ObservableCollection<MeterReading> MyReadings
        {
            get => _myReadings;
            set => SetProperty(ref _myReadings, value);
        }

        public ObservableCollection<Payment> MyPayments
        {
            get => _myPayments;
            set => SetProperty(ref _myPayments, value);
        }

        public Meter SelectedMeterForReading
        {
            get => _selectedMeterForReading;
            set
            {
                if (SetProperty(ref _selectedMeterForReading, value))
                {
                    if (value != null)
                    {
                        InputReadingM3 = value.LastReadingM3;
                    }
                }
            }
        }

        public decimal InputReadingM3
        {
            get => _inputReadingM3;
            set => SetProperty(ref _inputReadingM3, value);
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

        public ICommand RefreshCommand { get; }
        public ICommand SubmitReadingCommand { get; }
        public ICommand PrintCurrentReceiptCommand { get; }

        public Action<System.Windows.Documents.FlowDocument> OpenReceiptPreview { get; set; }

        public SubscriberPortalViewModel(
            ISubscriberRepository subscriberRepository,
            IMeterRepository meterRepository,
            IReadingRepository readingRepository,
            IPaymentRepository paymentRepository,
            IReceiptPrintService receiptPrintService,
            ILoggerService logger,
            User currentUser)
        {
            _subscriberRepository = subscriberRepository ?? throw new ArgumentNullException(nameof(subscriberRepository));
            _meterRepository = meterRepository ?? throw new ArgumentNullException(nameof(meterRepository));
            _readingRepository = readingRepository ?? throw new ArgumentNullException(nameof(readingRepository));
            _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
            _receiptPrintService = receiptPrintService ?? throw new ArgumentNullException(nameof(receiptPrintService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

            RefreshCommand = new AsyncRelayCommand(LoadPortalDataAsync);
            SubmitReadingCommand = new AsyncRelayCommand(SubmitReadingAsync, () => SelectedMeterForReading != null);
            PrintCurrentReceiptCommand = new AsyncRelayCommand(PrintReceiptAsync);

            _ = LoadPortalDataAsync();
        }

        public async Task LoadPortalDataAsync()
        {
            if (!_currentUser.SubscriberId.HasValue)
            {
                ErrorMessage = "К данной учетной записи не привязан лицевой счёт абонента.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            int subId = _currentUser.SubscriberId.Value;

            try
            {
                Subscriber = await Task.Run(() => _subscriberRepository.GetById(subId));
                var meters = await Task.Run(() => _meterRepository.GetBySubscriberId(subId));
                MyMeters = new ObservableCollection<Meter>(meters);

                if (meters.Count > 0 && SelectedMeterForReading == null)
                {
                    SelectedMeterForReading = meters[0];
                }

                var readings = await Task.Run(() => _readingRepository.GetAll(subId));
                MyReadings = new ObservableCollection<MeterReading>(readings);

                var payments = await Task.Run(() => _paymentRepository.GetAll(subId));
                MyPayments = new ObservableCollection<Payment>(payments);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки данных личного кабинета: " + ex.Message;
                _logger.LogError("Ошибка ЛК абонента", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SubmitReadingAsync()
        {
            if (SelectedMeterForReading == null) return;

            long valueL = (long)Math.Round(InputReadingM3 * 1000m);
            DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await Task.Run(() => _readingRepository.AddReading(
                    SelectedMeterForReading.Id,
                    currentMonth,
                    valueL,
                    _currentUser.Id));

                StatusMessage = $"Показание {InputReadingM3:N3} м³ успешно передано в абонентский отдел.";
                await LoadPortalDataAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task PrintReceiptAsync()
        {
            if (Subscriber == null) return;

            try
            {
                DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var readings = await Task.Run(() => _readingRepository.GetAll(Subscriber.Id, null, currentMonth));
                decimal paymentsSum = await Task.Run(() => _paymentRepository.GetPaymentsSumForPeriod(
                    Subscriber.Id, currentMonth, currentMonth.AddMonths(1).AddDays(-1)));

                var doc = _receiptPrintService.CreateReceiptDocument(Subscriber, currentMonth, readings, paymentsSum);

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
                ErrorMessage = "Ошибка печати квитанции: " + ex.Message;
            }
        }
    }
}
