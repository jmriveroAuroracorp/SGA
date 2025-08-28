using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Converter para validar entrada de decimales en TextBox
    /// </summary>
    public class DecimalInputConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString(CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // Si está vacío, devolver null
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }

                // Permitir entrada temporal como ".5" o "0.5" o "5."
                if (decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                {
                    return result;
                }
                
                // Si no se puede parsear pero es un formato válido temporal, devolver el valor original
                // Esto permite entrada como ".5" que se convertirá a 0.5
                if (IsValidDecimalFormat(stringValue))
                {
                    return value; // Mantener el valor original para permitir entrada temporal
                }
            }
            
            // Si no se puede convertir, devolver el valor original
            return value;
        }

        /// <summary>
        /// Valida si un texto tiene un formato válido para entrada decimal
        /// </summary>
        private bool IsValidDecimalFormat(string input)
        {
            if (string.IsNullOrEmpty(input))
                return true;

            // Permitir formatos como: "123", "123.45", ".45", "123."
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            return regex.IsMatch(input);
        }
    }
} 