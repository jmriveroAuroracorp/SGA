using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convertidor que calcula el ancho de la barra de progreso basado en el porcentaje y el ancho del contenedor
    /// </summary>
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || values[0] == null || values[1] == null)
                return 0.0;

            if (values[0] is double porcentaje && values[1] is double anchoTotal)
            {
                // Calcular el ancho proporcional, con un mínimo para que se vea algo si hay progreso
                if (porcentaje <= 0)
                    return 0.0;
                
                // Restar 2 píxeles del ancho total para dejar margen para las esquinas redondeadas
                var anchoDisponible = anchoTotal - 2;
                var ancho = (porcentaje / 100.0) * anchoDisponible;
                
                // Mínimo de 2 píxeles si hay algo de progreso
                return Math.Max(ancho, porcentaje > 0 ? 2.0 : 0.0);
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 