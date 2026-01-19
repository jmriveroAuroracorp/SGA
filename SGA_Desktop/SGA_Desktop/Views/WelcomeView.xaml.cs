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
            
            // Recargar datos al entrar a la vista (primera vez)
            Loaded += WelcomeView_Loaded;
            
            // Recargar datos cada vez que la vista se hace visible (navegaciones subsecuentes)
            IsVisibleChanged += WelcomeView_IsVisibleChanged;
        }

        private void WelcomeView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            RecargarResumenes();
        }

        private void WelcomeView_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // Solo recargar cuando se hace visible (evitar recargar al ocultarse)
            if ((bool)e.NewValue && (bool)e.OldValue == false)
            {
                RecargarResumenes();
            }
        }

        private void RecargarResumenes()
        {
            // Recargar el resumen de órdenes, conteos e inventarios cada vez que se navega a esta vista
            if (DataContext is WelcomeViewModel viewModel)
            {
                _ = viewModel.CargarResumenOrdenesAsync();
                _ = viewModel.CargarResumenConteosAsync();
                _ = viewModel.CargarResumenInventariosAsync();
            }
        }
    }
}
