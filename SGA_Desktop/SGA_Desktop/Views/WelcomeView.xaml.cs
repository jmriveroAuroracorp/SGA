using System.Windows.Controls;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    public partial class WelcomeView : Page
    {
        public WelcomeView()
        {
            InitializeComponent();
            DataContext = new ViewModels.WelcomeViewModel();
            
            // Recargar datos al entrar a la vista
            Loaded += WelcomeView_Loaded;
        }

        private void WelcomeView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Recargar el resumen de órdenes cada vez que se navega a esta vista
            if (DataContext is WelcomeViewModel viewModel)
            {
                _ = viewModel.CargarResumenOrdenesAsync();
            }
        }
    }
}
