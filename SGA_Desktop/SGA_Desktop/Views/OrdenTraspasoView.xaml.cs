using System.Windows.Controls;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Views
{
    /// <summary>
    /// Lógica de interacción para OrdenTraspasoView.xaml
    /// </summary>
    public partial class OrdenTraspasoView : Page
    {
        private static ViewModels.OrdenTraspasoViewModel? _viewModelInstance;

        public OrdenTraspasoView()
        {
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoView: Constructor iniciado");
            InitializeComponent();
            
            // Usar la misma instancia del ViewModel para mantener el estado
            if (_viewModelInstance == null)
            {
                _viewModelInstance = new ViewModels.OrdenTraspasoViewModel();
                System.Diagnostics.Debug.WriteLine("OrdenTraspasoView: Nueva instancia de ViewModel creada");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OrdenTraspasoView: Reutilizando instancia existente del ViewModel");
            }
            
            DataContext = _viewModelInstance;
            
            // Suscribirse al evento Loaded para recargar datos cada vez que se navega a la vista
            Loaded += OrdenTraspasoView_Loaded;
            
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoView: Constructor completado");
        }

        private void OrdenTraspasoView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoView: Vista cargada");
            // El ViewModel ahora maneja automáticamente la carga de datos cuando hay filtros pendientes
        }
    }
}
