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
	public class ZeroToCollapsedConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is decimal d)
				return d == 0 ? Visibility.Collapsed : Visibility.Visible;

			if (value is double dd)
				return dd == 0 ? Visibility.Collapsed : Visibility.Visible;

			if (value is int i)
				return i == 0 ? Visibility.Collapsed : Visibility.Visible;

			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

}
