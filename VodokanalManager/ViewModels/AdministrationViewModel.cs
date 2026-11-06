using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class AdministrationViewModel : ObservableObject
    {
        private readonly IUserRepository _userRepository;
        private readonly IDatabase _database;
        private readonly DatabaseInitializer _databaseInitializer;
        private readonly ISettingsService _settingsService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILoggerService _logger;
        private readonly User _currentUser;

        private ObservableCollection<User> _users = new ObservableCollection<User>();
        private User _selectedUser;

        private string _databasePath = string.Empty;
        private string _databaseFileSize = string.Empty;
        private bool _autoFillDemoData;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public ObservableCollection<User> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public User SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public string DatabasePath
        {
            get => _databasePath;
            set => SetProperty(ref _databasePath, value);
        }

        public string DatabaseFileSize
        {
            get => _databaseFileSize;
            set => SetProperty(ref _databaseFileSize, value);
        }

        public bool AutoFillDemoData
        {
            get => _autoFillDemoData;
            set
            {
                if (SetProperty(ref _autoFillDemoData, value))
                {
                    _settingsService.Settings.AutoFillDemoData = value;
                    _settingsService.Save();
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

        public ICommand LoadUsersCommand { get; }
        public ICommand AddStaffUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand ToggleUserActiveCommand { get; }

        public ICommand OpenDatabaseFolderCommand { get; }
        public ICommand SelectDatabaseFileCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand ClearDemoDataCommand { get; }

        public Func<User, Task<bool>> OpenUserEditDialogAsync { get; set; }
        public Action OnDatabaseReloadRequested { get; set; }

        public AdministrationViewModel(
            IUserRepository userRepository,
            IDatabase database,
            DatabaseInitializer databaseInitializer,
            ISettingsService settingsService,
            IPasswordHasher passwordHasher,
            ILoggerService logger,
            User currentUser)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _databaseInitializer = databaseInitializer ?? throw new ArgumentNullException(nameof(databaseInitializer));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _currentUser = currentUser;

            _autoFillDemoData = _settingsService.Settings.AutoFillDemoData;
            UpdateDatabaseInfo();

            LoadUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
            AddStaffUserCommand = new AsyncRelayCommand(AddStaffUserAsync);
            EditUserCommand = new AsyncRelayCommand(EditUserAsync, () => SelectedUser != null);
            ToggleUserActiveCommand = new AsyncRelayCommand(ToggleUserActiveAsync, () => SelectedUser != null && SelectedUser.Id != _currentUser?.Id);

            OpenDatabaseFolderCommand = new RelayCommand(OpenDatabaseFolder);
            SelectDatabaseFileCommand = new RelayCommand(SelectDatabaseFile);
            CreateBackupCommand = new RelayCommand(CreateBackup);
            RestoreBackupCommand = new RelayCommand(RestoreBackup);
            ClearDemoDataCommand = new AsyncRelayCommand(ClearDemoDataAsync);

            _ = LoadUsersAsync();
        }

        public void UpdateDatabaseInfo()
        {
            DatabasePath = _database.DatabasePath;
            if (File.Exists(DatabasePath))
            {
                var fi = new FileInfo(DatabasePath);
                double kb = fi.Length / 1024.0;
                DatabaseFileSize = kb >= 1024 ? $"{kb / 1024.0:F2} МБ" : $"{kb:F1} КБ";
            }
            else
            {
                DatabaseFileSize = "Файл отсутствует";
            }
        }

        public async Task LoadUsersAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var list = await Task.Run(() => _userRepository.GetAll());
                Users = new ObservableCollection<User>(list);
                UpdateDatabaseInfo();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки пользователей: " + ex.Message;
                _logger.LogError("Ошибка выборки пользователей", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddStaffUserAsync()
        {
            if (OpenUserEditDialogAsync == null) return;

            var newUser = new User
            {
                RoleId = 2, // По умолчанию Оператор
                IsActive = true
            };

            bool saved = await OpenUserEditDialogAsync(newUser);
            if (saved)
            {
                StatusMessage = $"Сотрудник '{newUser.FullName}' успешно добавлен.";
                await LoadUsersAsync();
            }
        }

        private async Task EditUserAsync()
        {
            if (OpenUserEditDialogAsync == null || SelectedUser == null) return;

            var clone = new User
            {
                Id = SelectedUser.Id,
                Login = SelectedUser.Login,
                FullName = SelectedUser.FullName,
                RoleId = SelectedUser.RoleId,
                RoleName = SelectedUser.RoleName,
                IsActive = SelectedUser.IsActive,
                SubscriberId = SelectedUser.SubscriberId
            };

            bool saved = await OpenUserEditDialogAsync(clone);
            if (saved)
            {
                StatusMessage = $"Данные пользователя '{clone.Login}' обновлены.";
                await LoadUsersAsync();
            }
        }

        private async Task ToggleUserActiveAsync()
        {
            if (SelectedUser == null) return;

            IsBusy = true;
            try
            {
                bool newStatus = !SelectedUser.IsActive;
                await Task.Run(() => _userRepository.SetActive(SelectedUser.Id, newStatus));
                SelectedUser.IsActive = newStatus;
                StatusMessage = $"Статус учётной записи '{SelectedUser.Login}' изменен на '{(newStatus ? "Активен" : "Заблокирован")}'.";
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка смены активности: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenDatabaseFolder()
        {
            try
            {
                string path = _database.DatabasePath;
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    string dir = Path.GetDirectoryName(path);
                    if (Directory.Exists(dir))
                        Process.Start("explorer.exe", $"\"{dir}\"");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Не удалось открыть проводник: " + ex.Message;
            }
        }

        private void SelectDatabaseFile()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "База данных SQLite (*.db;*.sqlite)|*.db;*.sqlite|Все файлы (*.*)|*.*",
                Title = "Выберите файл базы данных SQLite"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    _database.SetDatabasePath(ofd.FileName);
                    _settingsService.Settings.CustomDatabasePath = ofd.FileName;
                    _settingsService.Save();

                    UpdateDatabaseInfo();
                    StatusMessage = $"Подключена база данных: {ofd.FileName}";
                    OnDatabaseReloadRequested?.Invoke();
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Ошибка подключения к выбранному файлу: " + ex.Message;
                }
            }
        }

        private void CreateBackup()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Резервная копия SQLite (*.db)|*.db",
                FileName = $"Vodokanal_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                Title = "Сохранение резервной копии базы данных"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _databaseInitializer.BackupDatabase(sfd.FileName);
                    StatusMessage = $"Резервная копия успешно создана: {Path.GetFileName(sfd.FileName)}";
                    _logger.LogInfo($"Создана резервная копия БД: {sfd.FileName}");
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Ошибка резервного копирования: " + ex.Message;
                    _logger.LogError("Ошибка бэкапа", ex);
                }
            }
        }

        private void RestoreBackup()
        {
            var ofd = new OpenFileDialog
            {
                Filter = "Резервная копия SQLite (*.db;*.bak)|*.db;*.bak|Все файлы (*.*)|*.*",
                Title = "Выберите файл резервной копии для восстановления"
            };

            if (ofd.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "Внимание! Текущие данные будут заменены данными из выбранной резервной копии.\n\nПродолжить восстановление?",
                    "Подтверждение восстановления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try
                {
                    _databaseInitializer.RestoreDatabase(ofd.FileName);
                    UpdateDatabaseInfo();
                    StatusMessage = "База данных успешно восстановлена из резервной копии.";
                    _logger.LogInfo($"БД восстановлена из копии: {ofd.FileName}");
                    OnDatabaseReloadRequested?.Invoke();
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Ошибка восстановления базы данных: " + ex.Message;
                    _logger.LogError("Ошибка восстановления БД", ex);
                }
            }
        }

        private async Task ClearDemoDataAsync()
        {
            var result = MessageBox.Show(
                "Вы действительно хотите удалить все тестовые данные (абонентов, показания, счетчики и платежи)?\n\nСправочники, тарифы и учётные записи сотрудников будут сохранены.",
                "Подтверждение очистки данных",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await Task.Run(() => _databaseInitializer.ClearDemoData());
                StatusMessage = "Тестовые данные успешно удалены из базы данных.";
                _logger.LogInfo("Тестовые данные очищены пользователем.");
                await LoadUsersAsync();
                OnDatabaseReloadRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка очистки данных: " + ex.Message;
                _logger.LogError("Ошибка очистки тестовых данных", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
