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

        public IDatabase Database => _database;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

var russianCulture = new CultureInfo("ru-RU");
            Thread.CurrentThread.CurrentCulture = russianCulture;
            Thread.CurrentThread.CurrentUICulture = russianCulture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(russianCulture.IetfLanguageTag)));

_settingsService = new SettingsService();
            _loggerService = new LoggerService();
            _passwordHasher = new PasswordHasher();
            _receiptPrintService = new ReceiptPrintService();
            _csvExportService = new CsvExportService();

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
                string msg = args.Exception.Message;
                if (args.Exception.InnerException != null)
                {
                    msg += "\n\nДетали: " + args.Exception.InnerException.Message;
                }
                MessageBox.Show(
                    "Ошибка выполнения операции:\n" + msg,
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                args.Handled = true;
            };

try
            {
                string customPath = _settingsService.Settings.CustomDatabasePath;
                if (!string.IsNullOrWhiteSpace(customPath) && !System.IO.File.Exists(customPath))
                {
                    customPath = null;
                }
                _database = new SqliteDatabase(customPath);
                _databaseInitializer = new DatabaseInitializer(_database);

_databaseInitializer.Initialize(autoFillDemoData: false);

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
