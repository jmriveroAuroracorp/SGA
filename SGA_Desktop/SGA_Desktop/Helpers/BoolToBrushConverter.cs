using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string colorString)
            {
                var colors = colorString.Split('|');
                if (colors.Length == 2)
                {
                    var trueColor = colors[0].Trim();
                    var falseColor = colors[1].Trim();
                    
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(boolValue ? trueColor : falseColor);
                        return new SolidColorBrush(color);
                    }
                    catch
                    {
                        // Si falla la conversión, usar colores por defecto
                        return boolValue ? Brushes.DarkBlue : Brushes.Gray;
                    }
                }
            }
            
            return Brushes.Black; // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 