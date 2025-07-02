using System;
using System.Globalization;
using System.Windows.Data;

namespace SGA_Desktop.Helpers
{
	public class BoolToYesNoConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> (value is bool b && b) ? "Sí" : "No";

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}
