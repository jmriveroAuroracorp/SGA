using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte un booleano a un Brush para el borde cuando está seleccionado
    /// </summary>
    public class BoolToBorderBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected 
                    ? new SolidColorBrush(Color.FromRgb(111, 196, 250)) // #6fc4fa - ColorSecundario2 (más claro)
                    : new SolidColorBrush(Colors.LightGray);
            }
            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte un booleano a un Thickness para el borde cuando está seleccionado
    /// </summary>
    public class BoolToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected ? new Thickness(2) : new Thickness(1);
            }
            return new Thickness(1);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
