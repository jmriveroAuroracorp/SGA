using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para PaletLineasDialog.xaml
	/// </summary>
	public partial class PaletLineasDialog : Window
	{
		public PaletLineasDialog()
		{
			InitializeComponent();
		}

		// 🔷 NUEVO: Métodos para mejorar la experiencia de usuario en TextBox
		private void TextBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if (sender is TextBox textBox)
			{
				textBox.SelectAll();
			}
		}

		private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
			{
				textBox.Focus();
				textBox.SelectAll();
				e.Handled = true;
			}
		}
	}
}
