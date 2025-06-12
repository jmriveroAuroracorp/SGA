using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SGA_Desktop.Helpers          // ← pon aquí el namespace real
{
	public class CodigoToLogoConverter : IValueConverter
	{
		public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is short codigo)
			{
				// logo en  Assets/1.png   Assets/3.png   Assets/999.png …
				var uri = new Uri($"pack://application:,,,/Assets/{codigo}.png", UriKind.Absolute);
				return new BitmapImage(uri);
			}

			// fallback genérico: Assets/1.png
			return new BitmapImage(new Uri("pack://application:,,,/Assets/1.png", UriKind.Absolute));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotImplementedException();
	}
}
