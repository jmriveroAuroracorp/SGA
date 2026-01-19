using System;
using System.Globalization;
using System.Windows.Data;
using SGA_Desktop.Models;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Converter que obtiene propiedades de AjusteDto de forma segura
    /// Devuelve el valor de la propiedad si el objeto es AjusteDto, o un valor por defecto si no lo es
    /// </summary>
    public class AjustePropertyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return GetDefaultValue(targetType);

            if (value is AjusteDto ajuste)
            {
                string propertyName = parameter.ToString();
                var property = typeof(AjusteDto).GetProperty(propertyName);
                if (property != null)
                {
                    var propertyValue = property.GetValue(ajuste);
                    return propertyValue ?? GetDefaultValue(targetType);
                }
            }

            return GetDefaultValue(targetType);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private object GetDefaultValue(Type targetType)
        {
            if (targetType == typeof(string))
                return string.Empty;
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return 0m;
            if (targetType == typeof(int) || targetType == typeof(int?))
                return 0;
            return null;
        }
    }
}

