using System;
using System.Globalization;
using System.Windows.Data;
using SGA_Desktop.Models;

namespace SGA_Desktop.Helpers
{
    public class PorcentajeDistribucionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return "0.0%";
            
            bool porUnidades = (bool)values[0];
            var item = values[1];
            
            double porcentaje = 0;
            if (item is AlmacenDistribucionDto almacen)
            {
                porcentaje = porUnidades ? almacen.PorcentajeDelTotal : almacen.PorcentajePorTraspasos;
            }
            else if (item is UbicacionDistribucionDto ubicacion)
            {
                porcentaje = porUnidades ? ubicacion.PorcentajeDelTotal : ubicacion.PorcentajePorTraspasos;
            }
            else if (item is FlujoDistribucionDto flujo)
            {
                porcentaje = porUnidades ? flujo.PorcentajeDelTotal : flujo.PorcentajePorTraspasos;
            }
            
            return $"{porcentaje:F1}%";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

