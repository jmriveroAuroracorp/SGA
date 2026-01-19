using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.Helpers
{
    public class CodigoEmpresaToNombreConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is short codigoEmpresa && SessionManager.UsuarioActual?.empresas != null)
            {
                var empresa = SessionManager.UsuarioActual.empresas
                    .FirstOrDefault(e => e.Codigo == codigoEmpresa);
                return empresa?.Nombre ?? $"Empresa {codigoEmpresa}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
