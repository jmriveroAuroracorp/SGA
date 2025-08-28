using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Converter que devuelve rojo para valores negativos y azul para valores positivos o cero
    /// </summary>
    public class NegativeValueColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue < 0 ? Brushes.Red : Brushes.Blue;
            }
            
            if (value is double doubleValue)
            {
                return doubleValue < 0 ? Brushes.Red : Brushes.Blue;
            }
            
            if (value is int intValue)
            {
                return intValue < 0 ? Brushes.Red : Brushes.Blue;
            }
            
            // Por defecto, azul
            return Brushes.Blue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 