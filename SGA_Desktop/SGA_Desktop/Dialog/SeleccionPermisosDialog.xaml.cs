using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SGA_Desktop.Models;

namespace SGA_Desktop.Dialog
{
    public partial class SeleccionPermisosDialog : Window
    {
        private readonly List<PermisoDisponibleDto> _todosLosPermisos;
        private readonly List<short> _permisosYaAsignados;
        private readonly ObservableCollection<PermisoSeleccionDto> _permisosFiltrados;
        private readonly Dictionary<short, bool> _estadoSeleccionGlobal; // Mantener estado de selección global

        public List<short> PermisosSeleccionados { get; private set; }

        public SeleccionPermisosDialog(List<PermisoDisponibleDto> todosLosPermisos, 
                                      List<short> permisosYaAsignados)
        {
            InitializeComponent();
            
            _todosLosPermisos = todosLosPermisos ?? new List<PermisoDisponibleDto>();
            _permisosYaAsignados = permisosYaAsignados ?? new List<short>();
            _permisosFiltrados = new ObservableCollection<PermisoSeleccionDto>();
            _estadoSeleccionGlobal = new Dictionary<short, bool>();
            
            PermisosSeleccionados = new List<short>();
            
            ItemsControlPermisos.ItemsSource = _permisosFiltrados;
            
            CargarDatos();
        }

        private void CargarDatos()
        {
            try
            {
                // Inicializar estado global con los permisos ya asignados
                foreach (var permiso in _permisosYaAsignados)
                {
                    _estadoSeleccionGlobal[permiso] = true;
                }
                
                // Cargar permisos
                CargarPermisos();
                
                // Actualizar resumen
                ActualizarResumenSeleccion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar los datos: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CargarPermisos()
        {
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            var textoFiltro = TxtFiltro.Text?.ToLower() ?? string.Empty;
            
            var permisosFiltrados = _todosLosPermisos.AsEnumerable();
            
            // Filtro por texto
            if (!string.IsNullOrEmpty(textoFiltro))
            {
                permisosFiltrados = permisosFiltrados.Where(p => 
                    p.Codigo.ToString().Contains(textoFiltro) ||
                    p.Descripcion.ToLower().Contains(textoFiltro));
            }
            
            // Ordenar por código de permiso
            permisosFiltrados = permisosFiltrados.OrderBy(p => p.Codigo);
            
            // Actualizar la colección filtrada
            _permisosFiltrados.Clear();
            foreach (var permiso in permisosFiltrados)
            {
                var permisoSeleccion = new PermisoSeleccionDto
                {
                    Codigo = permiso.Codigo,
                    Descripcion = permiso.Descripcion,
                    IsSelected = _estadoSeleccionGlobal.ContainsKey(permiso.Codigo) && _estadoSeleccionGlobal[permiso.Codigo]
                };
                
                // Suscribirse al evento de cambio de selección
                permisoSeleccion.OnSelectionChanged += (sender) => {
                    _estadoSeleccionGlobal[sender.Codigo] = sender.IsSelected;
                    ActualizarResumenSeleccion();
                };
                
                _permisosFiltrados.Add(permisoSeleccion);
            }
            
            ActualizarResumenSeleccion();
        }

        private void ActualizarResumenSeleccion()
        {
            var seleccionadosVisibles = _permisosFiltrados.Count(p => p.IsSelected);
            var totalVisibles = _permisosFiltrados.Count;
            var totalSeleccionadosGlobal = _estadoSeleccionGlobal.Count(kvp => kvp.Value);
            var totalGlobal = _todosLosPermisos.Count;
            
            TxtResumenSeleccion.Text = $"Seleccionados: {totalSeleccionadosGlobal} de {totalGlobal} permisos";
            
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

        private void LimpiarFiltros_Click(object sender, RoutedEventArgs e)
        {
            TxtFiltro.Text = string.Empty;
            AplicarFiltros();
        }

        private void SeleccionarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var permiso in _permisosFiltrados)
            {
                permiso.IsSelected = true;
            }
            ActualizarResumenSeleccion();
        }

        private void DeseleccionarTodos_Click(object sender, RoutedEventArgs e)
        {
            foreach (var permiso in _permisosFiltrados)
            {
                permiso.IsSelected = false;
            }
            ActualizarResumenSeleccion();
        }

        private void ChkSeleccionarTodos_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var permiso in _permisosFiltrados)
            {
                permiso.IsSelected = true;
            }
            ActualizarResumenSeleccion();
        }

        private void ChkSeleccionarTodos_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var permiso in _permisosFiltrados)
            {
                permiso.IsSelected = false;
            }
            ActualizarResumenSeleccion();
        }

        private void AplicarSeleccion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PermisosSeleccionados.Clear();
                
                // Usar el estado global en lugar de solo los elementos filtrados
                foreach (var kvp in _estadoSeleccionGlobal.Where(kvp => kvp.Value))
                {
                    PermisosSeleccionados.Add(kvp.Key);
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

    public class PermisoSeleccionDto : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public short Codigo { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        
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
        public event Action<PermisoSeleccionDto>? OnSelectionChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}

