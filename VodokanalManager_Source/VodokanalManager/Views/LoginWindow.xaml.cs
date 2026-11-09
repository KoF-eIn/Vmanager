using System.Windows;
using VodokanalManager.ViewModels;

namespace VodokanalManager.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void UserPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = UserPasswordBox.Password;
            }
        }
    }
}
