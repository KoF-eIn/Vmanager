using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using VodokanalManager.Data;
using VodokanalManager.Data.Repositories;
using VodokanalManager.Models;
using VodokanalManager.Services;
using VodokanalManager.ViewModels;
using VodokanalManager.Views;

namespace VodokanalManager
{
    public partial class App : Application
    {
        private ISettingsService _settingsService;
        private ILoggerService _loggerService;
        private IPasswordHasher _passwordHasher;
        private IDatabase _database;
        private DatabaseInitializer _databaseInitializer;
        private IReceiptPrintService _receiptPrintService;
        private ICsvExportService _csvExportService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Установка русской локали для интерфейса, дат и чисел
            var russianCulture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentCulture = russianCulture;
            Thread.CurrentThread.CurrentUICulture = russianCulture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(russianCulture.IetfLanguageTag)));

            // 2. Инициализация ключевых сервисов
            _settingsService = new SettingsService();
            _loggerService = new LoggerService();
            _passwordHasher = new PasswordHasher();
            _receiptPrintService = new ReceiptPrintService();
            _csvExportService = new CsvExportService();

            // 3. Глобальные обработчики исключений
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                _loggerService.LogError("Неперехваченное системное исключение (AppDomain)", ex);
                MessageBox.Show(
                    "Произошла непредвиденная ошибка в работе программы.\nПодробности записаны в журнал log.txt.",
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                _loggerService.LogError("Неперехваченное исключение интерфейса (Dispatcher)", args.Exception);
                MessageBox.Show(
                    "Ошибка выполнения операции:\n" + args.Exception.Message,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                args.Handled = true;
            };

            // 4. Подключение и инициализация базы данных
            try
            {
                string customPath = _settingsService.Settings.CustomDatabasePath;
                _database = new SqliteDatabase(customPath);
                _databaseInitializer = new DatabaseInitializer(_database);

                // Автоматическое создание схемы и демо-данных при первом запуске
                _databaseInitializer.Initialize(_settingsService.Settings.AutoFillDemoData);

                _loggerService.LogInfo($"База данных готова к работе: {_database.DatabasePath}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Ошибка первичной инициализации базы данных", ex);
                MessageBox.Show(
                    "Не удалось инициализировать базу данных:\n" + ex.Message,
                    "Ошибка базы данных",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // 5. Запуск окна авторизации
            ShowLoginWindow();
        }

        private void ShowLoginWindow()
        {
            var userRepo = new UserRepository(_database);
            var loginVm = new LoginViewModel(userRepo, _passwordHasher, _settingsService, _loggerService);
            var loginWindow = new LoginWindow { DataContext = loginVm };

            loginVm.OnLoginSuccess = (user) =>
            {
                loginWindow.Hide();
                ShowMainWindow(user, loginWindow);
            };

            loginWindow.Show();
        }

        private void ShowMainWindow(User user, Window loginWindow)
        {
            var mainVm = new MainViewModel(
                user,
                _database,
                _databaseInitializer,
                _settingsService,
                _passwordHasher,
                _loggerService,
                _receiptPrintService,
                _csvExportService);

            var mainWindow = new MainWindow { DataContext = mainVm };

            mainVm.OnLogoutRequested = () =>
            {
                mainWindow.Close();
                ShowLoginWindow();
            };

            mainWindow.Closed += (s, e) =>
            {
                if (Application.Current.Windows.Count == 0 || (Application.Current.Windows.Count == 1 && Application.Current.Windows[0] == loginWindow && !loginWindow.IsVisible))
                {
                    Shutdown();
                }
            };

            loginWindow.Close();
            mainWindow.Show();
        }
    }
}
