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
    public class TariffsViewModel : ObservableObject
    {
        private readonly ITariffRepository _tariffRepository;
        private readonly ILoggerService _logger;
        private readonly User _currentUser;

        private ObservableCollection<Tariff> _tariffs = new ObservableCollection<Tariff>();
        private Tariff _selectedTariff;

        private int _newServiceTypeId = 1;
        private DateTime _newValidFrom = DateTime.Today;
        private decimal _newPriceRub = 40.00m;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<Tariff> Tariffs
        {
            get => _tariffs;
            set => SetProperty(ref _tariffs, value);
        }

        public Tariff SelectedTariff
        {
            get => _selectedTariff;
            set => SetProperty(ref _selectedTariff, value);
        }

        public int NewServiceTypeId
        {
            get => _newServiceTypeId;
            set => SetProperty(ref _newServiceTypeId, value);
        }

        public DateTime NewValidFrom
        {
            get => _newValidFrom;
            set => SetProperty(ref _newValidFrom, value);
        }

        public decimal NewPriceRub
        {
            get => _newPriceRub;
            set => SetProperty(ref _newPriceRub, value);
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

        public bool CanManage => _currentUser != null && _currentUser.IsAdmin;

        public ICommand RefreshCommand { get; }
        public ICommand AddTariffCommand { get; }

        public TariffsViewModel(
            ITariffRepository tariffRepository,
            ILoggerService logger,
            User currentUser)
        {
            _tariffRepository = tariffRepository ?? throw new ArgumentNullException(nameof(tariffRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser;

            RefreshCommand = new AsyncRelayCommand(LoadTariffsAsync);
            AddTariffCommand = new AsyncRelayCommand(AddTariffAsync, () => CanManage);

            _ = LoadTariffsAsync();
        }

        public async Task LoadTariffsAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var list = await Task.Run(() => _tariffRepository.GetAll());
                Tariffs = new ObservableCollection<Tariff>(list);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки тарифов: " + ex.Message;
                _logger.LogError("Ошибка выборки тарифов", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddTariffAsync()
        {
            if (NewPriceRub <= 0)
            {
                ErrorMessage = "Тариф должен быть больше 0 руб.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var tariff = new Tariff
                {
                    ServiceTypeId = NewServiceTypeId,
                    ValidFrom = new DateTime(NewValidFrom.Year, NewValidFrom.Month, 1),
                    PriceRubPerM3 = NewPriceRub
                };

                await Task.Run(() => _tariffRepository.AddTariff(tariff));
                StatusMessage = "Новый тариф успешно утверждён и сохранён.";
                await LoadTariffsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка добавления тарифа: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
