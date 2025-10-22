using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace SGA_Desktop.Helpers
{
	public class PermisosToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (parameter is string codigoFuncionalidad)
			{
				switch (codigoFuncionalidad)
				{
					case "ConsultaStock": return PermisosHelper.PuedeAccederAConsultaStock() ? Visibility.Visible : Visibility.Collapsed;
					case "Traspasos": return PermisosHelper.PuedeAccederATraspasos() ? Visibility.Visible : Visibility.Collapsed;
					//case "Inventario": return PermisosHelper.PuedeAccederAInventario() ? Visibility.Visible : Visibility.Collapsed;
					case "Pesaje": return PermisosHelper.PuedeAccederAPesaje() ? Visibility.Visible : Visibility.Collapsed;
					case "ImpresionEtiquetas": return PermisosHelper.PuedeAccederAImpresionEtiquetas() ? Visibility.Visible : Visibility.Collapsed;
					case "Calidad": return PermisosHelper.PuedeAccederACalidad() ? Visibility.Visible : Visibility.Collapsed;
					case "ConfiguracionOperarios": return PermisosHelper.PuedeAccederAConfiguracionOperarios() ? Visibility.Visible : Visibility.Collapsed;
						// etc...
				}
			}
			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// Como no necesitamos convertir de vuelta, lanzamos excepción
			throw new NotImplementedException("ConvertBack no está implementado para este converter");
		}
	}
}
