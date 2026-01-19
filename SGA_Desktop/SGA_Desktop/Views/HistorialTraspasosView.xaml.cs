using System.Windows.Controls;
using System.Windows;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using System.ComponentModel;

namespace SGA_Desktop.Views
{
    public partial class HistorialTraspasosView : Page
    {
        private TraspasoHistoricoViewModel? _viewModel;

        public HistorialTraspasosView()
        {
            InitializeComponent();
            _viewModel = new TraspasoHistoricoViewModel(new TraspasosService());
            DataContext = _viewModel;
            
            // Suscribirse al evento PropertyChanged para detectar cuando se recargan los datos
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Cuando se recarga (AplicarFiltrosAsync), hacer scroll al principio
            if (e.PropertyName == nameof(TraspasoHistoricoViewModel.DebeHacerScrollAlInicio))
            {
                // Usar Dispatcher para asegurar que se ejecute en el hilo de UI después de que se actualice la lista
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    // Scroll para Aurora SGA (Traspasos + Ajustes)
                    if (ListBoxTraspasosYAjustes != null && ListBoxTraspasosYAjustes.Items.Count > 0)
                    {
                        ListBoxTraspasosYAjustes.ScrollIntoView(ListBoxTraspasosYAjustes.Items[0]);
                    }
                    
                    // Scroll para StorageControl
                    if (ListBoxTraspasosStorageControl != null && ListBoxTraspasosStorageControl.Items.Count > 0)
                    {
                        ListBoxTraspasosStorageControl.ScrollIntoView(ListBoxTraspasosStorageControl.Items[0]);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}

