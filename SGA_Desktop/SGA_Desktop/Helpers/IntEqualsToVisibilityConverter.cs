using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte un entero a Visibility comparándolo con el parámetro: si son iguales = Visible, si no = Collapsed
    /// </summary>
    public class IntEqualsToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int paramValue))
                {
                    return intValue == paramValue ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

