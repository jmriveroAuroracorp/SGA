using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para SeleccionOperarioDialog.xaml
    /// </summary>
    public partial class SeleccionOperarioDialog : Window
    {
        private readonly OperariosConfiguracionService _service;
        private readonly ObservableCollection<OperarioDisponibleDto> _operarios;

        public OperarioDisponibleDto? OperarioSeleccionado { get; private set; }

        public SeleccionOperarioDialog(OperariosConfiguracionService service)
        {
            InitializeComponent();
            
            _service = service;
            _operarios = new ObservableCollection<OperarioDisponibleDto>();

            // Configurar controles
            DgOperarios.ItemsSource = _operarios;

            // Cargar datos iniciales
            _ = CargarOperariosAsync();
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                var operarios = await _service.ObtenerOperariosDisponiblesAsync();
                
                _operarios.Clear();
                if (operarios != null)
                {
                    foreach (var operario in operarios)
                    {
                        _operarios.Add(operario);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar operarios: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SeleccionarButton_Click(object sender, RoutedEventArgs e)
        {
            if (DgOperarios.SelectedItem is OperarioDisponibleDto operarioSeleccionado)
            {
                OperarioSeleccionado = operarioSeleccionado;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Por favor seleccione un operario.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DgOperarios_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Doble clic para seleccionar rápidamente
            SeleccionarButton_Click(sender, new RoutedEventArgs());
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
