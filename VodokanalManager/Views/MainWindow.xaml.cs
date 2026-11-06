using System.Windows;
using VodokanalManager.ViewModels;

namespace VodokanalManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Настройка модальных окон для SubscribersVM
                if (vm.SubscribersVM != null)
                {
                    vm.SubscribersVM.OpenEditDialogAsync = async (subscriber) =>
                    {
                        var dialog = new SubscriberEditDialog(subscriber) { Owner = this };
                        return dialog.ShowDialog() == true;
                    };
                }

                // Настройка модальных окон для MetersReadingsVM
                if (vm.MetersReadingsVM != null)
                {
                    vm.MetersReadingsVM.OpenAddReadingDialogAsync = async (meter) =>
                    {
                        var dialog = new ReadingEditDialog(meter) { Owner = this };
                        return dialog.ShowDialog() == true;
                    };

                    vm.MetersReadingsVM.OpenAddMeterDialogAsync = async (defaultSub) =>
                    {
                        var dialog = new MeterEditDialog(defaultSub) { Owner = this };
                        return dialog.ShowDialog() == true;
                    };
                }

                // Настройка модальных окон для PaymentsVM
                if (vm.PaymentsVM != null)
                {
                    vm.PaymentsVM.OpenNewPaymentDialogAsync = async (defaultSub) =>
                    {
                        var dialog = new PaymentEditDialog(defaultSub) { Owner = this };
                        return dialog.ShowDialog() == true;
                    };

                    vm.PaymentsVM.OpenReceiptPreview = (flowDoc) =>
                    {
                        var preview = new ReceiptPreviewWindow(flowDoc) { Owner = this };
                        preview.ShowDialog();
                    };
                }

                // Настройка модальных окон для AdministrationVM
                if (vm.AdministrationVM != null)
                {
                    vm.AdministrationVM.OpenUserEditDialogAsync = async (user) =>
                    {
                        var dialog = new UserEditDialog(user) { Owner = this };
                        return dialog.ShowDialog() == true;
                    };
                }

                // Настройка модальных окон для SubscriberPortalVM
                if (vm.SubscriberPortalVM != null)
                {
                    vm.SubscriberPortalVM.OpenReceiptPreview = (flowDoc) =>
                    {
                        var preview = new ReceiptPreviewWindow(flowDoc) { Owner = this };
                        preview.ShowDialog();
                    };
                }
            }
        }
    }
}
