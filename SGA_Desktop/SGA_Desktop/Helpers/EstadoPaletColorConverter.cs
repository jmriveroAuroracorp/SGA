using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte el estado de un palet a un color
    /// </summary>
    public class EstadoPaletColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string estado)
            {
                var estadoUpper = estado.ToUpper();
                return estadoUpper switch
                {
                    "ABIERTO" => new SolidColorBrush(Color.FromRgb(40, 167, 69)), // Verde
                    "CERrado" => new SolidColorBrush(Color.FromRgb(220, 53, 69)), // Rojo
                    _ => new SolidColorBrush(Color.FromRgb(108, 117, 125)) // Gris
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 