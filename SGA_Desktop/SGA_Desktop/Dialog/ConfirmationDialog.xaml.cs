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
	/// Lógica de interacción para ConfirmationDialog.xaml
	/// </summary>
	public partial class ConfirmationDialog : Window
	{
		public static readonly DependencyProperty IconGlyphProperty =
	  DependencyProperty.Register(
		  nameof(IconGlyph),
		  typeof(string),
		  typeof(ConfirmationDialog),
		  new PropertyMetadata("\uE7BA")   // por defecto: triángulo
	  );

		public string IconGlyph
		{
			get => (string)GetValue(IconGlyphProperty);
			set => SetValue(IconGlyphProperty, value);
		}
		/// <summary>
		/// Título que aparece en el encabezado de la ventana (y en la barra de título oculta).
		/// </summary>
		public string DialogTitle
		{
			get => TitleText.Text;
			set
			{
				Title = value;            // título de la Window (oculto porque WindowStyle=None)
				TitleText.Text = value;   // TextBlock visible
			}
		}

		public ConfirmationDialog(string title, string message, string iconGlyph = "\uE7BA")
		{
			InitializeComponent();
			TitleText.Text = title; 
			MessageText.Text = message;
			IconText.Text = iconGlyph;

		}

		private void YesButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void NoButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
