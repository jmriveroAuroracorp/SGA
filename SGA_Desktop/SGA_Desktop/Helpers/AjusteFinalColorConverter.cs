using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
    public class AjusteFinalColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal ajuste)
            {
                if (ajuste == 0)
                    return new SolidColorBrush(Colors.Gray); // Sin ajuste
                else if (ajuste > 0)
                    return new SolidColorBrush(Colors.Green); // Ajuste positivo (sobrestock)
                else
                    return new SolidColorBrush(Colors.Red); // Ajuste negativo (faltante)
            }
            
            // Valor null o no válido
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AjusteFinalTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal ajuste)
            {
                if (ajuste > 0)
                    return $"+{ajuste:F4}"; // Agregar + para valores positivos
                else
                    return $"{ajuste:F4}"; // Los negativos ya tienen el signo -
            }
            
            // Valor null o no válido
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 