using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    public class EstadoToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string estado)
            {
                return estado switch
                {
                    "ABIERTO" => new SolidColorBrush(Colors.Orange),
                    "EN_CONTEO" => new SolidColorBrush(Colors.Blue),
                    "CONSOLIDADO" => new SolidColorBrush(Colors.Green),
                    "CERRADO" => new SolidColorBrush(Colors.Gray),
                    _ => new SolidColorBrush(Colors.LightGray)
                };
            }

            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 