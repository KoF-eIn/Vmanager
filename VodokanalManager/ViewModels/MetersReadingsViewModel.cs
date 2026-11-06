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
    public class MetersReadingsViewModel : ObservableObject
    {
        private readonly IMeterRepository _meterRepository;
        private readonly IReadingRepository _readingRepository;
        private readonly ITariffRepository _tariffRepository;
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly ILoggerService _logger;
        private readonly User _currentUser;

        private ObservableCollection<Meter> _meters = new ObservableCollection<Meter>();
        private ObservableCollection<MeterReading> _readings = new ObservableCollection<MeterReading>();
        private ObservableCollection<Subscriber> _subscribers = new ObservableCollection<Subscriber>();

        private Meter _selectedMeter;
        private MeterReading _selectedReading;
        private Subscriber _selectedFilterSubscriber;
        private DateTime? _selectedFilterPeriod;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<Meter> Meters
        {
            get => _meters;
            set => SetProperty(ref _meters, value);
        }

        public ObservableCollection<MeterReading> Readings
        {
            get => _readings;
            set => SetProperty(ref _readings, value);
        }

        public ObservableCollection<Subscriber> Subscribers
        {
            get => _subscribers;
            set => SetProperty(ref _subscribers, value);
        }

        public Meter SelectedMeter
        {
            get => _selectedMeter;
            set
            {
                if (SetProperty(ref _selectedMeter, value))
                {
                    _ = LoadReadingsAsync();
                }
            }
        }

        public MeterReading SelectedReading
        {
            get => _selectedReading;
            set => SetProperty(ref _selectedReading, value);
        }

        public Subscriber SelectedFilterSubscriber
        {
            get => _selectedFilterSubscriber;
            set
            {
                if (SetProperty(ref _selectedFilterSubscriber, value))
                {
                    _ = LoadMetersAsync();
                }
            }
        }

        public DateTime? SelectedFilterPeriod
        {
            get => _selectedFilterPeriod;
            set
            {
                if (SetProperty(ref _selectedFilterPeriod, value))
                {
                    _ = LoadReadingsAsync();
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

        public bool CanManage => _currentUser != null && (_currentUser.IsAdmin || _currentUser.IsOperator);

        public ICommand RefreshCommand { get; }
        public ICommand AddReadingCommand { get; }
        public ICommand AddMeterCommand { get; }
        public ICommand DeactivateMeterCommand { get; }

        public Func<Meter, Task<bool>> OpenAddReadingDialogAsync { get; set; }
        public Func<Subscriber, Task<bool>> OpenAddMeterDialogAsync { get; set; }

        public MetersReadingsViewModel(
            IMeterRepository meterRepository,
            IReadingRepository readingRepository,
            ITariffRepository tariffRepository,
            ISubscriberRepository subscriberRepository,
            ILoggerService logger,
            User currentUser)
        {
            _meterRepository = meterRepository ?? throw new ArgumentNullException(nameof(meterRepository));
            _readingRepository = readingRepository ?? throw new ArgumentNullException(nameof(readingRepository));
            _tariffRepository = tariffRepository ?? throw new ArgumentNullException(nameof(tariffRepository));
            _subscriberRepository = subscriberRepository ?? throw new ArgumentNullException(nameof(subscriberRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser;

            RefreshCommand = new AsyncRelayCommand(LoadAllDataAsync);
            AddReadingCommand = new AsyncRelayCommand(AddReadingAsync, () => CanManage && SelectedMeter != null && SelectedMeter.IsActive);
            AddMeterCommand = new AsyncRelayCommand(AddMeterAsync, () => CanManage);
            DeactivateMeterCommand = new AsyncRelayCommand(DeactivateMeterAsync, () => CanManage && SelectedMeter != null && SelectedMeter.IsActive);

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

                await LoadMetersAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки данных: " + ex.Message;
                _logger.LogError("Ошибка загрузки приборов и показаний", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadMetersAsync()
        {
            try
            {
                int? subId = SelectedFilterSubscriber?.Id;
                var list = await Task.Run(() => subId.HasValue && subId.Value > 0
                    ? _meterRepository.GetBySubscriberId(subId.Value)
                    : _meterRepository.GetAll());

                Meters = new ObservableCollection<Meter>(list);
                await LoadReadingsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки списка приборов: " + ex.Message;
            }
        }

        public async Task LoadReadingsAsync()
        {
            try
            {
                int? subId = SelectedFilterSubscriber?.Id;
                int? meterId = SelectedMeter?.Id;
                DateTime? period = SelectedFilterPeriod;

                var list = await Task.Run(() => _readingRepository.GetAll(subId, meterId, period));
                Readings = new ObservableCollection<MeterReading>(list);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки показаний: " + ex.Message;
            }
        }

        private async Task AddReadingAsync()
        {
            if (OpenAddReadingDialogAsync == null || SelectedMeter == null) return;

            bool saved = await OpenAddReadingDialogAsync(SelectedMeter);
            if (saved)
            {
                StatusMessage = $"Показание для прибора №{SelectedMeter.SerialNo} успешно зафиксировано.";
                await LoadAllDataAsync();
            }
        }

        private async Task AddMeterAsync()
        {
            if (OpenAddMeterDialogAsync == null) return;

            bool saved = await OpenAddMeterDialogAsync(SelectedFilterSubscriber);
            if (saved)
            {
                StatusMessage = "Новый прибор учёта успешно зарегистрирован.";
                await LoadAllDataAsync();
            }
        }

        private async Task DeactivateMeterAsync()
        {
            if (SelectedMeter == null) return;

            IsBusy = true;
            try
            {
                await Task.Run(() => _meterRepository.SetActive(SelectedMeter.Id, false));
                StatusMessage = $"Прибор учёта №{SelectedMeter.SerialNo} выведен из эксплуатации.";
                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка вывода прибора из эксплуатации: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
