using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte un booleano a un color de fondo para el estado del operario
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive 
                    ? new SolidColorBrush(Color.FromRgb(40, 167, 69))  // Verde para activo
                    : new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Rojo para inactivo
            }
            return new SolidColorBrush(Color.FromRgb(108, 117, 125)); // Gris por defecto
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte un booleano a un color de borde para el estado del operario
    /// </summary>
    public class BoolToBorderColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive 
                    ? new SolidColorBrush(Color.FromRgb(25, 135, 84))  // Verde oscuro para activo
                    : new SolidColorBrush(Color.FromRgb(176, 42, 55)); // Rojo oscuro para inactivo
            }
            return new SolidColorBrush(Color.FromRgb(73, 80, 87)); // Gris oscuro por defecto
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte un booleano a texto de estado del operario
    /// </summary>
    public class BoolToStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "ACTIVO" : "INACTIVO";
            }
            return "DESCONOCIDO";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte entre enum FiltroEstadoOperario y ComboBoxItem
    /// </summary>
    public class FiltroEstadoToComboBoxConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SGA_Desktop.ViewModels.FiltroEstadoOperario filtro)
            {
                return filtro switch
                {
                    SGA_Desktop.ViewModels.FiltroEstadoOperario.Activos => 0,
                    SGA_Desktop.ViewModels.FiltroEstadoOperario.Inactivos => 1,
                    _ => 0
                };
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index switch
                {
                    0 => SGA_Desktop.ViewModels.FiltroEstadoOperario.Activos,
                    1 => SGA_Desktop.ViewModels.FiltroEstadoOperario.Inactivos,
                    _ => SGA_Desktop.ViewModels.FiltroEstadoOperario.Activos
                };
            }
            return SGA_Desktop.ViewModels.FiltroEstadoOperario.Activos;
        }
    }
}
