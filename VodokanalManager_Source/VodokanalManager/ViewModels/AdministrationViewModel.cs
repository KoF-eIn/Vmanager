using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private List<User> _allUsers = new List<User>();
        private ObservableCollection<User> _users = new ObservableCollection<User>();
        private User _selectedUser;

        private string _userSearchQuery = string.Empty;
        private int _selectedRoleFilterIndex = 0; 

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

        public string UserSearchQuery
        {
            get => _userSearchQuery;
            set
            {
                if (SetProperty(ref _userSearchQuery, value))
                {
                    ApplyUserFilter();
                }
            }
        }

        public int SelectedRoleFilterIndex
        {
            get => _selectedRoleFilterIndex;
            set
            {
                if (SetProperty(ref _selectedRoleFilterIndex, value))
                {
                    ApplyUserFilter();
                }
            }
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
        public ICommand DeleteUserCommand { get; }

        public ICommand OpenDatabaseFolderCommand { get; }
        public ICommand SelectDatabaseFileCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand LoadDemoDataCommand { get; }
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
            DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync, () => SelectedUser != null && SelectedUser.Id != _currentUser?.Id);

            OpenDatabaseFolderCommand = new RelayCommand(OpenDatabaseFolder);
            SelectDatabaseFileCommand = new RelayCommand(SelectDatabaseFile);
            CreateBackupCommand = new RelayCommand(CreateBackup);
            RestoreBackupCommand = new RelayCommand(RestoreBackup);
            LoadDemoDataCommand = new AsyncRelayCommand(LoadDemoDataAsync);
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
                _allUsers = list ?? new List<User>();
                ApplyUserFilter();
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

        private void ApplyUserFilter()
        {
            IEnumerable<User> filtered = _allUsers;

            if (_selectedRoleFilterIndex == 1)
            {
                
                filtered = filtered.Where(u => u.RoleId != 4);
            }
            else if (_selectedRoleFilterIndex == 2)
            {
                
                filtered = filtered.Where(u => u.RoleId == 4);
            }

            if (!string.IsNullOrWhiteSpace(_userSearchQuery))
            {
                string q = _userSearchQuery.Trim().ToLowerInvariant();
                filtered = filtered.Where(u =>
                    (u.Login != null && u.Login.ToLowerInvariant().Contains(q)) ||
                    (u.FullName != null && u.FullName.ToLowerInvariant().Contains(q)) ||
                    (u.RoleName != null && u.RoleName.ToLowerInvariant().Contains(q)));
            }

            Users = new ObservableCollection<User>(filtered);
        }

        private async Task AddStaffUserAsync()
        {
            if (OpenUserEditDialogAsync == null) return;

            var newUser = new User
            {
                RoleId = 2, 
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
                InitialPassword = SelectedUser.InitialPassword,
                SubscriberId = SelectedUser.SubscriberId
            };

            bool saved = await OpenUserEditDialogAsync(clone);
            if (saved)
            {
                StatusMessage = $"Учётная запись '{clone.Login}' обновлена.";
                await LoadUsersAsync();
            }
        }

        private async Task ToggleUserActiveAsync()
        {
            if (SelectedUser == null) return;
            if (SelectedUser.Id == _currentUser?.Id)
            {
                ErrorMessage = "Нельзя заблокировать собственную текущую учётную запись.";
                return;
            }

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
                ErrorMessage = "Ошибка изменения статуса пользователя: " + ex.Message;
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
                string dir = Path.GetDirectoryName(DatabasePath);
                if (Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", dir);
                }
                else
                {
                    ErrorMessage = "Папка с базой данных не найдена: " + dir;
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
                Title = "Выберите файл базы данных SQLite",
                Filter = "База данных SQLite (*.db;*.sqlite)|*.db;*.sqlite|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    _settingsService.Settings.CustomDatabasePath = ofd.FileName;
                    _settingsService.Save();

                    _database.SetDatabasePath(ofd.FileName);
                    _database.Reconnect();
                    _databaseInitializer.Initialize(_settingsService.Settings.AutoFillDemoData);

                    UpdateDatabaseInfo();
                    StatusMessage = "Подключение к выбранному файлу базы данных выполнено.";
                    _logger.LogInfo($"Пользователь сменил базу данных на: {ofd.FileName}");

                    OnDatabaseReloadRequested?.Invoke();
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Ошибка переключения базы данных: " + ex.Message;
                    _logger.LogError("Ошибка смены файла БД", ex);
                }
            }
        }

        private void CreateBackup()
        {
            string defaultName = $"Vodokanal_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var sfd = new SaveFileDialog
            {
                Title = "Сохранение резервной копии базы данных",
                Filter = "Резервная копия SQLite (*.db)|*.db",
                FileName = defaultName
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _databaseInitializer.BackupDatabase(sfd.FileName);
                    StatusMessage = $"Резервная копия успешно создана: {Path.GetFileName(sfd.FileName)}";
                    _logger.LogInfo($"Создан бэкап БД: {sfd.FileName}");
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Ошибка создания резервной копии: " + ex.Message;
                    _logger.LogError("Ошибка резервного копирования", ex);
                }
            }
        }

        private void RestoreBackup()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Выберите файл резервной копии для восстановления",
                Filter = "Резервная копия SQLite (*.db)|*.db|Все файлы (*.*)|*.*",
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == true)
            {
                var confirm = MessageBox.Show(
                    "Внимание! Текущая база данных будет полностью заменена данными из выбранной копии.\n\nПродолжить восстановление?",
                    "Подтверждение восстановления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes) return;

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

        private async Task LoadDemoDataAsync()
        {
            var result = MessageBox.Show(
                "Загрузить демонстрационные данные (тестовые абоненты, приборы учёта и показания)?\n\nВсе созданные вами абоненты и данные сохранятся.",
                "Загрузка демонстрационных данных",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await Task.Run(() => _databaseInitializer.LoadDemoData());
                StatusMessage = "Тестовые демо-данные успешно добавлены в базу данных.";
                _logger.LogInfo("Тестовые демо-данные добавлены пользователем.");
                await LoadUsersAsync();
                OnDatabaseReloadRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка загрузки тестовых данных: " + ex.Message;
                _logger.LogError("Ошибка загрузки демо-данных", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ClearDemoDataAsync()
        {
            var result = MessageBox.Show(
                "Вы действительно хотите удалить демонстрационные данные?\n\nБудут удалены ТОЛЬКО встроенные тестовые записи (IsDemo = 1).\nВсе внесённые вами абоненты, их счётчики, показания и оплаты останутся нетронутыми.",
                "Подтверждение очистки демо-данных",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                await Task.Run(() => _databaseInitializer.ClearDemoData());
                StatusMessage = "Тестовые данные успешно удалены. Все реальные абоненты сохранены.";
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
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null)
            {
                ErrorMessage = "Выберите учётную запись для удаления.";
                return;
            }

            if (SelectedUser.Id == _currentUser?.Id)
            {
                ErrorMessage = "Нельзя удалить текущего пользователя, под которым выполнен вход в систему!";
                return;
            }

            var result = MessageBox.Show(
                $"Вы действительно хотите удалить учётную запись '{SelectedUser.Login}' ({SelectedUser.FullName}, {SelectedUser.RoleName})?",
                "Подтверждение удаления пользователя",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                int userId = SelectedUser.Id;
                string login = SelectedUser.Login;
                await System.Threading.Tasks.Task.Run(() => _userRepository.DeleteUser(userId));
                SelectedUser = null;
                StatusMessage = $"Учётная запись '{login}' успешно удалена.";
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Ошибка удаления пользователя: " + ex.Message;
                _logger.LogError("Ошибка удаления пользователя", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}