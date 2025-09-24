using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convertidor que calcula el rectángulo de la barra de progreso con esquinas redondeadas
    /// </summary>
    public class ProgressRectConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3 || values[0] == null || values[1] == null || values[2] == null)
                return new Rect(0, 0, 0, 0);

            if (values[0] is double porcentaje && values[1] is double anchoTotal && values[2] is double altoTotal)
            {
                if (porcentaje <= 0)
                    return new Rect(0, 0, 0, altoTotal - 2);

                // Calcular el ancho proporcional, dejando margen para las esquinas redondeadas
                var anchoDisponible = anchoTotal - 2;
                var ancho = (porcentaje / 100.0) * anchoDisponible;
                
                // Mínimo de 2 píxeles si hay algo de progreso
                var anchoFinal = Math.Max(ancho, porcentaje > 0 ? 2.0 : 0.0);
                
                return new Rect(0, 0, anchoFinal, altoTotal - 2);
            }

            return new Rect(0, 0, 0, 0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


