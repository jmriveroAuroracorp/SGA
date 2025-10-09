using System.Windows.Controls;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    /// <summary>
    /// Lógica de interacción para CalidadView.xaml
    /// </summary>
    public partial class CalidadView : Page
    {
        public CalidadView()
        {
            InitializeComponent();
            DataContext = new CalidadViewModel();
        }
    }
}
