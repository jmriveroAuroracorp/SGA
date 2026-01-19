using System;
using System.Globalization;
using System.Windows.Data;
using System.Diagnostics;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Convierte múltiples valores en un array de objetos
    /// </summary>
    public class ArrayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return null;
            
            // Debug para ver qué valores estamos recibiendo
            Debug.WriteLine($"ArrayConverter: Recibidos {values.Length} valores");
            for (int i = 0; i < values.Length; i++)
            {
                Debug.WriteLine($"  Valor {i}: {values[i]} (tipo: {values[i]?.GetType().Name ?? "null"})");
                var tipo = values[i]?.GetType();
                if (tipo != null && tipo.Name == "UbicacionPasilloGroup")
                {
                    // Usar reflexión para obtener las propiedades
                    var headerProp = tipo.GetProperty("HeaderPasillo");
                    var ubicacionesProp = tipo.GetProperty("Ubicaciones");
                    if (headerProp != null && ubicacionesProp != null)
                    {
                        var header = headerProp.GetValue(values[i])?.ToString() ?? "null";
                        var ubicaciones = ubicacionesProp.GetValue(values[i]);
                        var count = ubicaciones?.GetType().GetProperty("Count")?.GetValue(ubicaciones) ?? 0;
                        Debug.WriteLine($"    Grupo: {header} con {count} ubicaciones");
                    }
                }
            }
            
            // Crear una copia del array para asegurar que las referencias se mantengan
            var resultado = new object[values.Length];
            Array.Copy(values, resultado, values.Length);
            
            Debug.WriteLine($"ArrayConverter: Devolviendo array con {resultado.Length} elementos");
            if (resultado[0] != null)
            {
                var tipoResultado = resultado[0].GetType();
                if (tipoResultado.Name == "UbicacionPasilloGroup")
                {
                    var headerProp = tipoResultado.GetProperty("HeaderPasillo");
                    var header = headerProp?.GetValue(resultado[0])?.ToString() ?? "null";
                    Debug.WriteLine($"  Grupo en resultado: {header}");
                }
                else
                {
                    Debug.WriteLine($"  Primer elemento no es grupo, es: {tipoResultado.Name}");
                }
            }
            else
            {
                Debug.WriteLine($"  Grupo en resultado: NULL");
            }
            
            return resultado;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

