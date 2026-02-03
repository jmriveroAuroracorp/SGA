using System;
using System.Globalization;
using System.Windows.Data;
using SGA_Desktop.Models.Calidad;

namespace SGA_Desktop.Converters
{
    /// <summary>
    /// Compara si un StockCalidadDto es igual al seleccionado
    /// </summary>
    public class StockSeleccionadoConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return false;

            var itemActual = values[0] as StockCalidadDto;
            var itemSeleccionado = values[1] as StockCalidadDto;

            if (itemActual == null || itemSeleccionado == null)
                return false;

            // Comparar por propiedades clave
            return itemActual.CodigoArticulo == itemSeleccionado.CodigoArticulo &&
                   itemActual.LotePartida == itemSeleccionado.LotePartida &&
                   itemActual.CodigoAlmacen == itemSeleccionado.CodigoAlmacen &&
                   itemActual.Ubicacion == itemSeleccionado.Ubicacion;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Compara si un BloqueoCalidadDto es igual al seleccionado
    /// </summary>
    public class BloqueoSeleccionadoConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2)
                return false;

            var itemActual = values[0] as BloqueoCalidadDto;
            var itemSeleccionado = values[1] as BloqueoCalidadDto;

            if (itemActual == null || itemSeleccionado == null)
                return false;

            // Comparar por Id
            return itemActual.Id == itemSeleccionado.Id;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
