using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Manejar bool normal
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;
            
            // Manejar bool? (nullable)
            if (value is bool?)
            {
                var nullableBool = (bool?)value;
                if (nullableBool.HasValue && nullableBool.Value)
                    return Visibility.Visible;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;
            return false;
        }
    }
} 