using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SGA_Desktop.Models;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte el tipo del objeto a Visibility: Visible si es AjusteDto, Collapsed si es TraspasoDto
    /// </summary>
    public class IsAjusteDtoToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            // Si es AjusteDto, mostrar; si es TraspasoDto u otro tipo, ocultar
            return value is AjusteDto ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

