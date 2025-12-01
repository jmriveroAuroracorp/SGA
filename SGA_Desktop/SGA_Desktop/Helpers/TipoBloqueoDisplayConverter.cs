using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    public class TipoBloqueoDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string tipoBloqueo)
            {
                return tipoBloqueo switch
                {
                    "SOLO_PULMON" => "PULMON",
                    "TOTAL" => "TOTAL",
                    _ => tipoBloqueo
                };
            }
            return "TOTAL";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

