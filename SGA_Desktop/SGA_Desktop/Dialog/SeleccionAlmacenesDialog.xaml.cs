using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SGA_Desktop.Models;
using SGA_Desktop.Services;

namespace SGA_Desktop.Dialog
{
    public partial class SeleccionAlmacenesDialog : Window
    {
        private readonly OperariosConfiguracionService _operariosService;
        private readonly List<AlmacenConfiguracionDto> _todosLosAlmacenes;
        private readonly ObservableCollection<AlmacenOperarioDto> _almacenesYaAsignados;
        private readonly ObservableCollection<AlmacenSeleccionDto> _almacenesFiltrados;
        private readonly Dictionary<string, bool> _estadoSeleccionGlobal; // Mantener estado de selección global

        public List<AlmacenOperarioDto> AlmacenesSeleccionados { get; private set; }

        public SeleccionAlmacenesDialog(List<AlmacenConfiguracionDto> todosLosAlmacenes, 
                                      ObservableCollection<AlmacenOperarioDto> almacenesYaAsignados)
        {
            InitializeComponent();
            
            _operariosService = new OperariosConfiguracionService();
            _todosLosAlmacenes = todosLosAlmacenes ?? new List<AlmacenConfiguracionDto>();
            _almacenesYaAsignados = almacenesYaAsignados ?? new ObservableCollection<AlmacenOperarioDto>();
            _almacenesFiltrados = new ObservableCollection<AlmacenSeleccionDto>();
            _estadoSeleccionGlobal = new Dictionary<string, bool>();
            
            AlmacenesSeleccionados = new List<AlmacenOperarioDto>();
            
            ItemsControlAlmacenes.ItemsSource = _almacenesFiltrados;
            
            _ = CargarDatosAsync();
        }

        private async Task CargarDatosAsync()
        {
            try
            {
                // Cargar empresas para el filtro
                await CargarEmpresasFiltro();
                
                // Cargar almacenes
                CargarAlmacenes();
                
                // Actualizar resumen
                ActualizarResumenSeleccion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar los datos: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CargarEmpresasFiltro()
        {
            try
            {
                var empresas = await _operariosService.ObtenerEmpresasDisponiblesAsync();
                
                // Crear una lista con la opción "Todas las empresas" al inicio
                var empresasConTodas = new List<object>
                {
                    new { CodigoEmpresa = (short)0, Nombre = "Todas las empresas" }
                };
                
                // Agregar las empresas reales
                foreach (var empresa in empresas)
                {
                    empresasConTodas.Add(empresa);
                }
                
                // Asignar la lista completa al ItemsSource
                CmbEmpresaFiltro.ItemsSource = empresasConTodas;
                CmbEmpresaFiltro.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar empresas: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarAlmacenes()
        {
            // Solo inicializar estado global si está vacío (primera vez)
            if (_estadoSeleccionGlobal.Count == 0)
            {
                // Inicializar estado global con los almacenes ya asignados
                foreach (var almacen in _almacenesYaAsignados)
                {
                    _estadoSeleccionGlobal[almacen.CodigoAlmacen] = true;
                }
            }
            
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            var textoFiltro = TxtFiltro.Text?.ToLower() ?? string.Empty;
            var empresaSeleccionada = CmbEmpresaFiltro.SelectedValue as short?;
            
            var almacenesFiltrados = _todosLosAlmacenes.AsEnumerable();
            
            // Filtro por texto
            if (!string.IsNullOrEmpty(textoFiltro))
            {
                almacenesFiltrados = almacenesFiltrados.Where(a => 
                    a.CodigoAlmacen.ToString().Contains(textoFiltro) ||
                    a.Descripcion.ToLower().Contains(textoFiltro) ||
                    a.NombreEmpresa.ToLower().Contains(textoFiltro));
            }
            
            // Filtro por empresa
            if (empresaSeleccionada.HasValue && empresaSeleccionada.Value > 0)
            {
                almacenesFiltrados = almacenesFiltrados.Where(a => a.CodigoEmpresa == empresaSeleccionada.Value);
            }
            
            // Actualizar la colección filtrada
            _almacenesFiltrados.Clear();
            foreach (var almacen in almacenesFiltrados)
            {
                var almacenSeleccion = new AlmacenSeleccionDto
                {
                    CodigoAlmacen = almacen.CodigoAlmacen,
                    Descripcion = almacen.Descripcion,
                    NombreEmpresa = almacen.NombreEmpresa,
                    IsSelected = _estadoSeleccionGlobal.ContainsKey(almacen.CodigoAlmacen) && _estadoSeleccionGlobal[almacen.CodigoAlmacen]
                };
                
                // Suscribirse al evento de cambio de selección
                almacenSeleccion.OnSelectionChanged += (sender) => {
                    _estadoSeleccionGlobal[sender.CodigoAlmacen] = sender.IsSelected;
                    ActualizarResumenSeleccion();
                };
                
                _almacenesFiltrados.Add(almacenSeleccion);
            }
            
            ActualizarResumenSeleccion();
        }

        private void ActualizarResumenSeleccion()
        {
            var seleccionadosVisibles = _almacenesFiltrados.Count(a => a.IsSelected);
            var totalVisibles = _almacenesFiltrados.Count;
            var totalSeleccionadosGlobal = _estadoSeleccionGlobal.Count(kvp => kvp.Value);
            var totalGlobal = _todosLosAlmacenes.Count;
            
            TxtResumenSeleccion.Text = $"Seleccionados: {totalSeleccionadosGlobal} de {totalGlobal} almacenes";
            
            // Actualizar checkbox de "Seleccionar todos" basado en los elementos visibles
            if (seleccionadosVisibles == totalVisibles && totalVisibles > 0)
            {
                ChkSeleccionarTodos.IsChecked = true;
            }
            else if (seleccionadosVisibles > 0 && seleccionadosVisibles < totalVisibles)
            {
                ChkSeleccionarTodos.IsChecked = null; // Estado indeterminado
            }
            else
            {
                ChkSeleccionarTodos.IsChecked = false;
            }
        }

        private void TxtFiltro_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void CmbEmpresaFiltro_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void LimpiarFiltros_Click(object sender, RoutedEventArgs e)
        {
            TxtFiltro.Text = string.Empty;
            CmbEmpresaFiltro.SelectedIndex = 0;
            AplicarFiltros();
        }

        private void SeleccionarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var almacen in _almacenesFiltrados)
            {
                almacen.IsSelected = true;
            }
            ActualizarResumenSeleccion();
        }

        private void DeseleccionarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var almacen in _almacenesFiltrados)
            {
                almacen.IsSelected = false;
            }
            ActualizarResumenSeleccion();
        }

        private void ChkSeleccionarTodos_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var almacen in _almacenesFiltrados)
            {
                almacen.IsSelected = true;
            }
            ActualizarResumenSeleccion();
        }

        private void ChkSeleccionarTodos_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var almacen in _almacenesFiltrados)
            {
                almacen.IsSelected = false;
            }
            ActualizarResumenSeleccion();
        }

        private void AplicarSeleccion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AlmacenesSeleccionados.Clear();
                
                // Usar el estado global en lugar de solo los elementos filtrados
                foreach (var kvp in _estadoSeleccionGlobal.Where(kvp => kvp.Value))
                {
                    var codigoAlmacen = kvp.Key;
                    var almacenOriginal = _todosLosAlmacenes.FirstOrDefault(a => a.CodigoAlmacen == codigoAlmacen);
                    
                    if (almacenOriginal != null)
                    {
                        var almacenOperario = new AlmacenOperarioDto
                        {
                            CodigoAlmacen = almacenOriginal.CodigoAlmacen,
                            DescripcionAlmacen = almacenOriginal.Descripcion,
                            NombreEmpresa = almacenOriginal.NombreEmpresa
                        };
                        
                        AlmacenesSeleccionados.Add(almacenOperario);
                    }
                }
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al aplicar la selección: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class AlmacenSeleccionDto : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string NombreEmpresa { get; set; } = string.Empty;
        
        public bool IsSelected 
        { 
            get => _isSelected;
            set 
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    OnSelectionChanged?.Invoke(this);
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public event Action<AlmacenSeleccionDto>? OnSelectionChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
