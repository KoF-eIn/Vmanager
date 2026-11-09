using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class SubscribersViewModel : ObservableObject
    {
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly IMeterRepository _meterRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILoggerService _logger;
        private readonly User _currentUser;

        private readonly DispatcherTimer _debounceTimer;

        private ObservableCollection<Subscriber> _subscribers = new ObservableCollection<Subscriber>();
        private ObservableCollection<Meter> _selectedSubscriberMeters = new ObservableCollection<Meter>();
        private Subscriber _selectedSubscriber;

        private string _searchQuery = string.Empty;
        private int _selectedSubscriberTypeId; 
        private bool _onlyWithDebt;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<Subscriber> Subscribers
        {
            get => _subscribers;
            set => SetProperty(ref _subscribers, value);
        }

        public ObservableCollection<Meter> SelectedSubscriberMeters
        {
            get => _selectedSubscriberMeters;
            set => SetProperty(ref _selectedSubscriberMeters, value);
        }

        public Subscriber SelectedSubscriber
        {
            get => _selectedSubscriber;
            set
            {
                if (SetProperty(ref _selectedSubscriber, value))
                {
                    _ = LoadSubscriberMetersAsync();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
            }
        }

        public int SelectedSubscriberTypeId
        {
            get => _selectedSubscriberTypeId;
            set
            {
                if (SetProperty(ref _selectedSubscriberTypeId, value))
                {
                    _ = LoadSubscribersAsync();
                }
            }
        }

        public bool OnlyWithDebt
        {
            get => _onlyWithDebt;
            set
            {
                if (SetProperty(ref _onlyWithDebt, value))
                {
                    _ = LoadSubscribersAsync();
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
        public ICommand AddSubscriberCommand { get; }
        public ICommand EditSubscriberCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand DeleteSubscriberCommand { get; }

        public Func<Subscriber, Task<bool>> OpenEditDialogAsync { get; set; }
        public Action OnSubscriberChanged { get; set; }

        public SubscribersViewModel(
            ISubscriberRepository subscriberRepository,
            IMeterRepository meterRepository,
            IPasswordHasher passwordHasher,
            ILoggerService logger,
            User currentUser)
        {
            _subscriberRepository = subscriberRepository ?? throw new ArgumentNullException(nameof(subscriberRepository));
            _meterRepository = meterRepository ?? throw new ArgumentNullException(nameof(meterRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser;

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                _ = LoadSubscribersAsync();
            };

            RefreshCommand = new AsyncRelayCommand(LoadSubscribersAsync);
            AddSubscriberCommand = new AsyncRelayCommand(AddSubscriberAsync, () => CanManage);
            EditSubscriberCommand = new AsyncRelayCommand(EditSubscriberAsync, () => CanManage && SelectedSubscriber != null);
            ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => CanManage && SelectedSubscriber != null);
            DeleteSubscriberCommand = new AsyncRelayCommand(DeleteSubscriberAsync, () => CanManage && SelectedSubscriber != null);

            _ = LoadSubscribersAsync();
        }

        public async Task LoadSubscribersAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            string query = SearchQuery;
            int? typeId = SelectedSubscriberTypeId > 0 ? SelectedSubscriberTypeId : (int?)null;
            bool onlyDebt = OnlyWithDebt;

            try
            {
                var list = await Task.Run(() => _subscriberRepository.GetAll(query, typeId, onlyDebt));
                Subscribers = new ObservableCollection<Subscriber>(list);

                if (SelectedSubscriber != null)
                {
                    
                    var current = list.Find(s => s.Id == SelectedSubscriber.Id);
                    SelectedSubscriber = current;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки списка абонентов: " + ex.Message;
                _logger.LogError("Ошибка выборки абонентов", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSubscriberMetersAsync()
        {
            if (SelectedSubscriber == null)
            {
                SelectedSubscriberMeters.Clear();
                return;
            }

            int subId = SelectedSubscriber.Id;
            try
            {
                var meters = await Task.Run(() => _meterRepository.GetBySubscriberId(subId));
                SelectedSubscriberMeters = new ObservableCollection<Meter>(meters);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка загрузки счетчиков для абонента {subId}", ex);
            }
        }

        private async Task AddSubscriberAsync()
        {
            if (OpenEditDialogAsync == null) return;

            var newSubscriber = new Subscriber
            {
                SubscriberTypeId = 1,
                IsActive = true
            };

            bool saved = await OpenEditDialogAsync(newSubscriber);
            if (saved)
            {
                StatusMessage = $"Абонент '{newSubscriber.Name}' успешно добавлен. Личный кабинет создан (логин: {newSubscriber.AccountNo}).";
                await LoadSubscribersAsync();
                OnSubscriberChanged?.Invoke();
            }
        }

        private async Task EditSubscriberAsync()
        {
            if (OpenEditDialogAsync == null || SelectedSubscriber == null) return;

            var clone = new Subscriber
            {
                Id = SelectedSubscriber.Id,
                AccountNo = SelectedSubscriber.AccountNo,
                Name = SelectedSubscriber.Name,
                SubscriberTypeId = SelectedSubscriber.SubscriberTypeId,
                SubscriberTypeName = SelectedSubscriber.SubscriberTypeName,
                Address = SelectedSubscriber.Address,
                Phone = SelectedSubscriber.Phone,
                IsActive = SelectedSubscriber.IsActive
            };

            bool saved = await OpenEditDialogAsync(clone);
            if (saved)
            {
                StatusMessage = $"Данные абонента '{clone.Name}' обновлены.";
                await LoadSubscribersAsync();
                OnSubscriberChanged?.Invoke();
            }
        }

                private async Task DeleteSubscriberAsync()
        {
            if (SelectedSubscriber == null)
            {
                ErrorMessage = "Выберите абонента для удаления.";
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                $"Вы действительно хотите удалить абонента '{SelectedSubscriber.Name}' (л/с {SelectedSubscriber.AccountNo})?\n\nВнимание: Все приборы учёта, внесённые показания, история платежей и учётная запись абонента также будут удалены!",
                "Подтверждение удаления абонента",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                int subId = SelectedSubscriber.Id;
                string subName = SelectedSubscriber.Name;
                await System.Threading.Tasks.Task.Run(() => _subscriberRepository.Delete(subId));
                SelectedSubscriber = null;
                StatusMessage = $"Абонент '{subName}' и все связанные данные успешно удалены.";
                await LoadSubscribersAsync();
                OnSubscriberChanged?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка при удалении абонента: " + ex.Message;
                _logger.LogError("Ошибка удаления абонента", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ToggleActiveAsync()
        {
            if (SelectedSubscriber == null) return;

            IsBusy = true;
            try
            {
                bool newStatus = !SelectedSubscriber.IsActive;
                await Task.Run(() => _subscriberRepository.SetActive(SelectedSubscriber.Id, newStatus));
                SelectedSubscriber.IsActive = newStatus;
                StatusMessage = $"Статус абонента изменен на '{(newStatus ? "Активен" : "Отключен")}'.";
                await LoadSubscribersAsync();
                OnSubscriberChanged?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка изменения статуса: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
