using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para GestionEmpresasOperarioDialog.xaml
    /// </summary>
    public partial class GestionEmpresasOperarioDialog : Window
    {
        private readonly OperariosConfiguracionService _service;
        private readonly int _operarioId;
        private readonly string _operarioNombre;
        private readonly ObservableCollection<EmpresaOperarioDto> _empresasAsignadas;

        public GestionEmpresasOperarioDialog(int operarioId, string operarioNombre, OperariosConfiguracionService service)
        {
            InitializeComponent();
            
            _service = service;
            _operarioId = operarioId;
            _operarioNombre = operarioNombre;
            _empresasAsignadas = new ObservableCollection<EmpresaOperarioDto>();

            // Configurar controles
            TxtOperarioInfo.Text = $"Operario: {_operarioNombre} (ID: {_operarioId})";

            // Cargar datos iniciales
            _ = CargarDatosInicialesAsync();
        }

        private async Task CargarDatosInicialesAsync()
        {
            await Task.WhenAll(
                CargarEmpresasAsignadasAsync(),
                CargarEmpresasDisponiblesAsync()
            );
        }

        private async Task CargarEmpresasDisponiblesAsync()
        {
            try
            {
                var empresas = await _service.ObtenerEmpresasDisponiblesAsync();
                if (empresas != null)
                {
                    CmbEmpresas.ItemsSource = empresas;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar empresas disponibles: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CargarEmpresasAsignadasAsync()
        {
            try
            {
                var response = await _service.ObtenerEmpresasOperarioAsync(_operarioId);
                
                _empresasAsignadas.Clear();
                SpEmpresasAsignadas.Children.Clear();
                
                if (response != null)
                {
                    // Actualizar información del operario con datos de la BD
                    TxtOperarioInfo.Text = $"Operario: {response.OperarioNombre} (ID: {response.OperarioId})";
                    if (!string.IsNullOrEmpty(response.CodigoCentro))
                    {
                        TxtOperarioInfo.Text += $" - Centro: {response.CodigoCentro}";
                    }

                    // Cargar empresas en el StackPanel
                    foreach (var empresa in response.Empresas)
                    {
                        _empresasAsignadas.Add(empresa);
                        CrearElementoEmpresa(empresa);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar empresas: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CrearElementoEmpresa(EmpresaOperarioDto empresa)
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var empresaText = new TextBlock
            {
                Text = empresa.Empresa,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var codigoText = new TextBlock
            {
                Text = $"(Código: {empresa.CodigoEmpresa})",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(empresaText);
            stackPanel.Children.Add(codigoText);

            var eliminarButton = new Button
            {
                Content = "Eliminar",
                Width = 80,
                Height = 25,
                Background = System.Windows.Media.Brushes.Red,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Tag = empresa
            };
            eliminarButton.Click += EliminarEmpresa_Click;

            Grid.SetColumn(stackPanel, 0);
            Grid.SetColumn(eliminarButton, 1);

            grid.Children.Add(stackPanel);
            grid.Children.Add(eliminarButton);

            border.Child = grid;
            SpEmpresasAsignadas.Children.Add(border);
        }

        private async void AsignarEmpresa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validaciones
                if (CmbEmpresas.SelectedValue == null)
                {
                    MessageBox.Show("Por favor seleccione una empresa.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TxtEmpresaOrigen.Text))
                {
                    MessageBox.Show("El código de empresa origen es obligatorio.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!short.TryParse(TxtEmpresaOrigen.Text, out var empresaOrigen))
                {
                    MessageBox.Show("El código de empresa origen debe ser un número válido.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var codigoEmpresa = (short)CmbEmpresas.SelectedValue;

                // Crear DTO para asignar (sin el nombre, se obtiene automáticamente)
                var asignarDto = new AsignarEmpresaDto
                {
                    CodigoEmpresa = codigoEmpresa,
                    EmpresaOrigen = empresaOrigen
                };

                // Asignar empresa
                var exito = await _service.AsignarEmpresaOperarioAsync(_operarioId, asignarDto);
                
                if (exito)
                {
                    MessageBox.Show("Empresa asignada correctamente.", "Éxito", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Limpiar campos
                    CmbEmpresas.SelectedValue = null;
                    TxtEmpresaOrigen.Clear();
                    
                    // Recargar lista
                    await CargarEmpresasAsignadasAsync();
                }
                else
                {
                    MessageBox.Show("Error al asignar la empresa.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al asignar empresa: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EliminarEmpresa_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is EmpresaOperarioDto empresaSeleccionada)
                {
                    var resultado = MessageBox.Show(
                        $"¿Está seguro de que desea eliminar la empresa '{empresaSeleccionada.Empresa}' del operario?", 
                        "Confirmar eliminación", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);

                    if (resultado == MessageBoxResult.Yes)
                    {
                        var exito = await _service.EliminarEmpresaOperarioAsync(
                            _operarioId, 
                            empresaSeleccionada.CodigoEmpresa, 
                            empresaSeleccionada.EmpresaOrigen);
                        
                        if (exito)
                        {
                            MessageBox.Show("Empresa eliminada correctamente.", "Éxito", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // Recargar lista
                            await CargarEmpresasAsignadasAsync();
                        }
                        else
                        {
                            MessageBox.Show("Error al eliminar la empresa.", "Error", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al eliminar empresa: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Recargar_Click(object sender, RoutedEventArgs e)
        {
            await CargarDatosInicialesAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
