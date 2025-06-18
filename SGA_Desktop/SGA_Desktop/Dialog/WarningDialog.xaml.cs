using System.Windows;

namespace SGA_Desktop.Dialog
{
	public partial class WarningDialog : Window
	{
		public static readonly DependencyProperty IconGlyphProperty =
			DependencyProperty.Register(
				nameof(IconGlyph),
				typeof(string),
				typeof(WarningDialog),
				new PropertyMetadata("\uE814")); // ícono por defecto: signo de advertencia

		public string IconGlyph
		{
			get => (string)GetValue(IconGlyphProperty);
			set => SetValue(IconGlyphProperty, value);
		}

		public WarningDialog(string title, string message, string iconGlyph = "\uE814")
		{
			InitializeComponent();
			Title = title;
			TitleText.Text = title;
			MessageText.Text = message;
			IconGlyph = iconGlyph;
		}

		private void AcceptButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
