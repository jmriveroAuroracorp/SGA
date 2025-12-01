using System.Windows.Controls;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    public partial class RendimientosView : Page
    {
        public RendimientosView()
        {
            InitializeComponent();
            DataContext = new RendimientosViewModel();
        }
    }
}

