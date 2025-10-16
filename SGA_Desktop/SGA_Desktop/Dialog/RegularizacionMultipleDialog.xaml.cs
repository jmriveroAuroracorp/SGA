using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SGA_Desktop.Dialog
{
    public partial class RegularizacionMultipleDialog : Window
    {
        public RegularizacionMultipleDialog(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && !textBox.IsKeyboardFocused)
            {
                textBox.Focus();
                textBox.SelectAll();
                e.Handled = true;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }
    }
} 