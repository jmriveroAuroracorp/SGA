using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Converters
{
    /// <summary>
    /// Convierte el tipo de traspaso a color de fondo
    /// </summary>
    public class TipoTraspasoToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string tipo)
            {
                return tipo switch
                {
                    "ARTICULO" => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // #2196F3
                    "PALET" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),   // #9C27B0
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))          // Gris por defecto
                };
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte el tipo de traspaso a texto legible
    /// </summary>
    public class TipoTraspasoToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string tipo)
            {
                return tipo switch
                {
                    "ARTICULO" => "ARTÍCULO",
                    "PALET" => "PALET",
                    _ => tipo
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte el estado del traspaso a color de fondo
    /// </summary>
    public class EstadoToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string estado)
            {
                return estado switch
                {
                    "COMPLETADO" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // #4CAF50
                    "PENDIENTE" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),   // #FF9800
                    "PENDIENTE_ERP" => new SolidColorBrush(Color.FromRgb(63, 81, 181)), // #3F51B5
                    "ERROR_ERP" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // #F44336
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))             // Gris por defecto
                };
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Convierte el estado del traspaso a texto legible
    /// </summary>
    public class EstadoToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string estado)
            {
                return estado switch
                {
                    "COMPLETADO" => "COMPLETADO",
                    "PENDIENTE" => "PENDIENTE",
                    "PENDIENTE_ERP" => "PEND. ERP",
                    "ERROR_ERP" => "ERROR ERP",
                    _ => estado
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
