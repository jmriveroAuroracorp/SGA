using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Si hay parámetro, usar lógica de comparación
            if (parameter is string expectedValue && value is string stringValue)
            {
                return stringValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase) 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
            
            // Si no hay parámetro, mostrar si el string no está vacío
            if (value is string str)
            {
                return !string.IsNullOrWhiteSpace(str) ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 