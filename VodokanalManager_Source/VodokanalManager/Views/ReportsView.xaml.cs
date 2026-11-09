using System.Windows;
using System.Windows.Controls;
using VodokanalManager.ViewModels;

namespace VodokanalManager.Views
{
    public partial class ReportsView : UserControl
    {
        public ReportsView()
        {
            InitializeComponent();
        }

        private void ReportType1_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReportsViewModel vm) vm.SelectedReportIndex = 0;
        }

        private void ReportType2_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReportsViewModel vm) vm.SelectedReportIndex = 1;
        }

        private void ReportType3_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReportsViewModel vm) vm.SelectedReportIndex = 2;
        }

        private void ReportType4_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReportsViewModel vm) vm.SelectedReportIndex = 3;
        }
    }
}
