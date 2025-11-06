using System;
using System.Globalization;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Helper para formatear decimales de forma adaptativa,
    /// mostrando solo los decimales necesarios (elimina ceros finales)
    /// </summary>
    public static class DecimalFormatHelper
    {
        /// <summary>
        /// Formatea un decimal mostrando hasta 8 decimales significativos,
        /// eliminando ceros innecesarios por la derecha sin redondear.
        /// 
        /// Ejemplos:
        /// - 12.0 → "12"
        /// - 12.35998 → "12.35998"
        /// - 100.5000 → "100.5"
        /// - 200.123400 → "200.1234"
        /// </summary>
        /// <param name="valor">El valor decimal a formatear</param>
        /// <returns>String formateado con decimales significativos (máximo 8)</returns>
        public static string FormatearCantidad(decimal valor)
        {
            // Usar formato "0.########" que muestra hasta 8 decimales
            // pero elimina automáticamente los ceros finales
            return valor.ToString("0.########", CultureInfo.InvariantCulture);
        }
    }
}

