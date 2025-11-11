using System.Windows.Controls;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    public partial class HistorialTraspasosView : Page
    {
        public HistorialTraspasosView()
        {
            InitializeComponent();
            DataContext = new TraspasoHistoricoViewModel(new TraspasosService());
        }
    }
}

