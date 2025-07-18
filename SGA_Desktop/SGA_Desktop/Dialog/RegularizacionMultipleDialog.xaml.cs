using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class RegularizacionMultipleDialog : Window
    {
        public RegularizacionMultipleDialog(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
} 