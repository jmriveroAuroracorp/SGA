using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SGA_Desktop.Models;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte el tipo del objeto a Visibility: Visible si es TraspasoDto, Collapsed si es AjusteDto
    /// </summary>
    public class IsTraspasoDtoToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return Visibility.Collapsed;

            // Si es TraspasoDto, mostrar; si es AjusteDto u otro tipo, ocultar
            return value is TraspasoDto ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

