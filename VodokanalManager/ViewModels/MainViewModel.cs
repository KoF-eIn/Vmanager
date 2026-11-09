using System;
using System.Windows.Input;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IDatabase _database;
        private readonly DatabaseInitializer _databaseInitializer;
        private readonly ISettingsService _settingsService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILoggerService _logger;
        private readonly IReceiptPrintService _receiptPrintService;
        private readonly ICsvExportService _csvExportService;

        private User _currentUser;
        private object _currentView;
        private string _activeTabName = string.Empty;

        public IDatabase Database => _database;
        public IPasswordHasher PasswordHasher => _passwordHasher;
        public DatabaseInitializer DatabaseInitializer => _databaseInitializer;

        public User CurrentUser
        {
            get => _currentUser;
            private set => SetProperty(ref _currentUser, value);
        }

        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string ActiveTabName
        {
            get => _activeTabName;
            set => SetProperty(ref _activeTabName, value);
        }

        public bool IsSubscribersActive => ActiveTabName == "Абоненты";
        public bool IsMetersReadingsActive => ActiveTabName == "Приборы и показания";
        public bool IsPaymentsActive => ActiveTabName == "Касса и платежи";
        public bool IsTariffsActive => ActiveTabName == "Тарифы";
        public bool IsReportsActive => ActiveTabName == "Аналитика и отчёты";
        public bool IsAdministrationActive => ActiveTabName == "Система и база данных";
        public bool IsSubscriberPortalActive => ActiveTabName == "Личный кабинет";

        public SubscribersViewModel SubscribersVM { get; private set; }
        public MetersReadingsViewModel MetersReadingsVM { get; private set; }
        public PaymentsViewModel PaymentsVM { get; private set; }
        public TariffsViewModel TariffsVM { get; private set; }
        public ReportsViewModel ReportsVM { get; private set; }
        public AdministrationViewModel AdministrationVM { get; private set; }
        public SubscriberPortalViewModel SubscriberPortalVM { get; private set; }

        public bool CanViewSubscribers => CurrentUser != null && (CurrentUser.IsAdmin || CurrentUser.IsOperator);
        public bool CanViewMetersReadings => CurrentUser != null && (CurrentUser.IsAdmin || CurrentUser.IsOperator);
        public bool CanViewPayments => CurrentUser != null && (CurrentUser.IsAdmin || CurrentUser.IsCashier);
        public bool CanViewTariffs => CurrentUser != null && CurrentUser.IsAdmin;
        public bool CanViewReports => CurrentUser != null && (CurrentUser.IsAdmin || CurrentUser.IsOperator || CurrentUser.IsCashier);
        public bool CanViewAdministration => CurrentUser != null && CurrentUser.IsAdmin;
        public bool CanViewSubscriberPortal => CurrentUser != null && CurrentUser.IsSubscriber;

        public ICommand NavigateSubscribersCommand { get; }
        public ICommand NavigateMetersReadingsCommand { get; }
        public ICommand NavigatePaymentsCommand { get; }
        public ICommand NavigateTariffsCommand { get; }
        public ICommand NavigateReportsCommand { get; }
        public ICommand NavigateAdministrationCommand { get; }
        public ICommand NavigateSubscriberPortalCommand { get; }
        public ICommand LogoutCommand { get; }

        public Action OnLogoutRequested { get; set; }

        public MainViewModel(
            User currentUser,
            IDatabase database,
            DatabaseInitializer databaseInitializer,
            ISettingsService settingsService,
            IPasswordHasher passwordHasher,
            ILoggerService logger,
            IReceiptPrintService receiptPrintService,
            ICsvExportService csvExportService)
        {
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _databaseInitializer = databaseInitializer ?? throw new ArgumentNullException(nameof(databaseInitializer));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _receiptPrintService = receiptPrintService ?? throw new ArgumentNullException(nameof(receiptPrintService));
            _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));

            InitializeChildViewModels();

            NavigateSubscribersCommand = new RelayCommand(() => SwitchView(SubscribersVM, "Абоненты"), () => CanViewSubscribers);
            NavigateMetersReadingsCommand = new RelayCommand(() => SwitchView(MetersReadingsVM, "Приборы и показания"), () => CanViewMetersReadings);
            NavigatePaymentsCommand = new RelayCommand(() => SwitchView(PaymentsVM, "Касса и платежи"), () => CanViewPayments);
            NavigateTariffsCommand = new RelayCommand(() => SwitchView(TariffsVM, "Тарифы"), () => CanViewTariffs);
            NavigateReportsCommand = new RelayCommand(() => SwitchView(ReportsVM, "Аналитика и отчёты"), () => CanViewReports);
            NavigateAdministrationCommand = new RelayCommand(() => SwitchView(AdministrationVM, "Система и база данных"), () => CanViewAdministration);
            NavigateSubscriberPortalCommand = new RelayCommand(() => SwitchView(SubscriberPortalVM, "Личный кабинет"), () => CanViewSubscriberPortal);
            LogoutCommand = new RelayCommand(() => OnLogoutRequested?.Invoke());

if (CurrentUser.IsSubscriber)
            {
                SwitchView(SubscriberPortalVM, "Личный кабинет");
            }
            else if (CurrentUser.IsCashier)
            {
                SwitchView(PaymentsVM, "Касса и платежи");
            }
            else
            {
                SwitchView(SubscribersVM, "Абоненты");
            }
        }

        private void InitializeChildViewModels()
        {
            var userRepo = new UserRepository(_database);
            var subRepo = new SubscriberRepository(_database);
            var meterRepo = new MeterRepository(_database);
            var readingRepo = new ReadingRepository(_database);
            var tariffRepo = new TariffRepository(_database);
            var paymentRepo = new PaymentRepository(_database);
            var reportRepo = new ReportRepository(_database);

            if (CanViewSubscribers)
            {
                SubscribersVM = new SubscribersViewModel(subRepo, meterRepo, _passwordHasher, _logger, CurrentUser);
                SubscribersVM.OnSubscriberChanged = () =>
                {
                    if (AdministrationVM != null)
                    {
                        _ = AdministrationVM.LoadUsersAsync();
                    }
                };
            }

            if (CanViewMetersReadings)
                MetersReadingsVM = new MetersReadingsViewModel(meterRepo, readingRepo, tariffRepo, subRepo, _logger, CurrentUser);

            if (CanViewPayments)
                PaymentsVM = new PaymentsViewModel(paymentRepo, subRepo, readingRepo, _receiptPrintService, _csvExportService, _logger, CurrentUser);

            if (CanViewTariffs)
                TariffsVM = new TariffsViewModel(tariffRepo, _logger, CurrentUser);

            if (CanViewReports)
                ReportsVM = new ReportsViewModel(reportRepo, _csvExportService, _logger);

            if (CanViewAdministration)
            {
                AdministrationVM = new AdministrationViewModel(userRepo, _database, _databaseInitializer, _settingsService, _passwordHasher, _logger, CurrentUser);
                AdministrationVM.OnDatabaseReloadRequested = () =>
                {
                    InitializeChildViewModels();
                    if (CurrentUser.IsAdmin) SwitchView(AdministrationVM, "Система и база данных");
                };
            }

            if (CanViewSubscriberPortal)
                SubscriberPortalVM = new SubscriberPortalViewModel(subRepo, meterRepo, readingRepo, paymentRepo, _receiptPrintService, _logger, CurrentUser);
        }

        private void SwitchView(object view, string tabName)
        {
            CurrentView = view;
            ActiveTabName = tabName;
            OnPropertyChanged(nameof(IsSubscribersActive));
            OnPropertyChanged(nameof(IsMetersReadingsActive));
            OnPropertyChanged(nameof(IsPaymentsActive));
            OnPropertyChanged(nameof(IsTariffsActive));
            OnPropertyChanged(nameof(IsReportsActive));
            OnPropertyChanged(nameof(IsAdministrationActive));
            OnPropertyChanged(nameof(IsSubscriberPortalActive));

if (view == AdministrationVM && AdministrationVM != null)
            {
                _ = AdministrationVM.LoadUsersAsync();
            }
            else if (view == SubscribersVM && SubscribersVM != null)
            {
                _ = SubscribersVM.LoadSubscribersAsync();
            }
            else if (view == MetersReadingsVM && MetersReadingsVM != null)
            {
                _ = MetersReadingsVM.LoadAllDataAsync();
            }
            else if (view == PaymentsVM && PaymentsVM != null)
            {
                _ = PaymentsVM.LoadPaymentsAsync();
            }
        }
    }
}
