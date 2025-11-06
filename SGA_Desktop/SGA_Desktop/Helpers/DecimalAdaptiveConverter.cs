using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Converter para formatear decimales de forma adaptativa en XAML,
    /// mostrando solo los decimales necesarios (elimina ceros finales)
    /// </summary>
    public class DecimalAdaptiveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return DecimalFormatHelper.FormatearCantidad(decimalValue);
            }
            
            if (value is double doubleValue)
            {
                return DecimalFormatHelper.FormatearCantidad((decimal)doubleValue);
            }
            
            if (value is float floatValue)
            {
                return DecimalFormatHelper.FormatearCantidad((decimal)floatValue);
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

