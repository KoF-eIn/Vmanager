using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VodokanalManager.ViewModels.Common
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool flag && flag;
            if (Invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is Visibility v && v == Visibility.Visible;
            return Invert ? !isVisible : isVisible;
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            return string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DecimalToRubConverter : IValueConverter
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal dec)
                return $"{dec.ToString("N2", RussianCulture)} ₽";

            if (value is long l)
                return $"{(l / 100m).ToString("N2", RussianCulture)} ₽";

            return "0,00 ₽";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DebtToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush DangerBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        private static readonly SolidColorBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            decimal debt = 0m;
            if (value is decimal d) debt = d;
            else if (value is long l) debt = l / 100m;

            return debt > 0 ? DangerBrush : SuccessBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
