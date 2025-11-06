using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    public class CantidadDisplayConverter : IMultiValueConverter
    {
        // values[0] = CantidadContada (decimal?)
        // values[1] = CantidadContadaTexto (string)
        // values[2] = StockActual (decimal)
        // values[3] = ConteoACiegas (bool)
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var cantidadContada = values[0] as decimal?;
                var cantidadTexto = values[1] as string;
                var stockActual = values[2] is decimal d ? d : 0m;
                var esInicializadoACero = values[3] is bool b && b;

                // Si hay texto en edición, mostrarlo tal cual
                if (!string.IsNullOrWhiteSpace(cantidadTexto))
                    return cantidadTexto;

                // Si hay cantidad contada, mostrarla formateada
                if (cantidadContada.HasValue)
                    return DecimalFormatHelper.FormatearCantidad(cantidadContada.Value);

                // Si es inventario inicializado a 0, mostrar 0
                if (esInicializadoACero)
                    return "0";

                // Si es inventario normal, mostrar StockActual por defecto
                return DecimalFormatHelper.FormatearCantidad(stockActual);
            }
            catch
            {
                return string.Empty;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            // Solo devolvemos el texto al segundo valor (CantidadContadaTexto) y el resto sin cambios
            var texto = value?.ToString() ?? string.Empty;
            return new object[] { Binding.DoNothing, texto, Binding.DoNothing, Binding.DoNothing };
        }
    }
} 