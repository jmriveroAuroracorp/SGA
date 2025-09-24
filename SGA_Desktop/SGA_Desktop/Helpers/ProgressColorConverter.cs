using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convertidor que devuelve un color basado en el porcentaje de progreso
    /// </summary>
    public class ProgressColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double porcentaje)
            {
                return porcentaje switch
                {
                    >= 100 => new SolidColorBrush(Color.FromRgb(25, 135, 84)), // Verde - Completado
                    >= 75 => new SolidColorBrush(Color.FromRgb(13, 110, 253)),  // Azul - Casi completado
                    >= 50 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),   // Amarillo - En progreso
                    >= 25 => new SolidColorBrush(Color.FromRgb(253, 126, 20)),  // Naranja - Iniciado
                    > 0 => new SolidColorBrush(Color.FromRgb(220, 53, 69)),     // Rojo - Muy poco progreso
                    _ => new SolidColorBrush(Color.FromRgb(108, 117, 125))      // Gris - Sin progreso
                };
            }

            return new SolidColorBrush(Color.FromRgb(108, 117, 125)); // Gris por defecto
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
