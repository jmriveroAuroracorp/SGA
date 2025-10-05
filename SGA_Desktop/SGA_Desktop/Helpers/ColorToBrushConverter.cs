using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convertidor que convierte un string de color a un SolidColorBrush
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorString);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    // Si falla la conversión, usar color por defecto
                    return Brushes.Gray;
                }
            }
            
            return Brushes.Gray; // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
