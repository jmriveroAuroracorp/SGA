using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Converters
{
    /// <summary>
    /// Convierte el origen del ajuste a color de fondo para el badge
    /// </summary>
    public class OrigenToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string origen)
            {
                return origen switch
                {
                    "INVENTARIO" => new SolidColorBrush(Color.FromRgb(52, 152, 219)),   // Azul
                    "CONTEO" => new SolidColorBrush(Color.FromRgb(155, 89, 182)),        // Morado
                    "MANUAL" => new SolidColorBrush(Color.FromRgb(149, 165, 166)),       // Gris
                    _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
                };
            }
            return new SolidColorBrush(Color.FromRgb(149, 165, 166));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

