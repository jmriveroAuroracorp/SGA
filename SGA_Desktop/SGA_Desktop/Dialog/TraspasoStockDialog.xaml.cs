using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class TraspasoStockDialog : Window
    {
        public TraspasoStockDialog(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
} 