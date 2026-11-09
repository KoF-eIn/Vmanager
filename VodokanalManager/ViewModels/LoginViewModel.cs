using System;
using System.Threading.Tasks;
using System.Windows.Input;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels.Common;

namespace VodokanalManager.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISettingsService _settingsService;
        private readonly ILoggerService _logger;

        private string _login = "admin";
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string Login
        {
            get => _login;
            set => SetProperty(ref _login, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand LoginCommand { get; }
        public ICommand QuickFillCommand { get; }

        public Action<User> OnLoginSuccess { get; set; }

        public LoginViewModel(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            ISettingsService settingsService,
            ILoggerService logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!string.IsNullOrWhiteSpace(_settingsService.Settings.LastLogin))
            {
                _login = _settingsService.Settings.LastLogin;
            }

            LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(Login));
            QuickFillCommand = new RelayCommand(param =>
            {
                if (param is string role)
                {
                    switch (role.ToLowerInvariant())
                    {
                        case "admin":
                            Login = "admin";
                            Password = "admin";
                            break;
                        case "operator":
                            Login = "operator";
                            Password = "operator";
                            break;
                        case "kassir":
                            Login = "kassir";
                            Password = "kassir";
                            break;
                        case "subscriber":
                            Login = "100245";
                            Password = "user";
                            break;
                    }
                }
            });
        }

        private async Task ExecuteLoginAsync()
        {
            ErrorMessage = string.Empty;
            IsBusy = true;

            string loginInput = Login?.Trim();
            string passInput = Password;

            try
            {
                User user = await Task.Run(() => _userRepository.Authenticate(loginInput, passInput, _passwordHasher));

                if (user == null)
                {
                    ErrorMessage = "Неверный логин или пароль. Проверьте правильность ввода.";
                    _logger.LogWarning($"Неудачная попытка входа под логином '{loginInput}'.");
                    return;
                }

                _settingsService.Settings.LastLogin = loginInput;
                _settingsService.Save();

                _logger.LogInfo($"Успешная авторизация пользователя '{user.Login}' ({user.RoleName}).");
                OnLoginSuccess?.Invoke(user);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _logger.LogError($"Ошибка авторизации: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
