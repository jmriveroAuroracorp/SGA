using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace SGA_Desktop.Helpers
{
	public class EstadoColorConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is string estado)
			{
				return estado switch
				{
					"Abierto" => Brushes.Green,
					"Cerrado" => Brushes.Red,
					"PENDIENTE" => Brushes.Orange,
					"PENDIENTE_ERP" => Brushes.Goldenrod,
					"COMPLETADO" => Brushes.SeaGreen,
					"CANCELADO" => Brushes.Gray,
					"EN_TRANSITO" => Brushes.DodgerBlue,
					_ => Brushes.Black
				};
			}
			return Brushes.Black;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
