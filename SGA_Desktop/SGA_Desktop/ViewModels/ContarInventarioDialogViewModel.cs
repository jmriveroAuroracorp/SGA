using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
    public partial class ContarInventarioDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private List<ArticuloInventarioDto> _todosLosArticulos = new();
        #endregion

        #region Constructor
        public ContarInventarioDialogViewModel(InventarioService inventarioService, StockService stockService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            
            ArticulosInventario = new ObservableCollection<ArticuloInventarioDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ContarInventarioDialogViewModel() : this(new InventarioService(), new StockService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private InventarioCabeceraDto? inventario;

        public ObservableCollection<ArticuloInventarioDto> ArticulosInventario { get; }

        [ObservableProperty]
        private string filtroUbicacion = string.Empty;

        [ObservableProperty]
        private string filtroArticulo = string.Empty;

        [ObservableProperty]
        private ArticuloInventarioDto? articuloSeleccionado;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private bool sumarUnidades = false;

        [ObservableProperty]
        private decimal unidadesGlobales = 0;

        [ObservableProperty]
        private bool puedeGuardar = false;
        #endregion

        #region Computed Properties
        public bool CanCargarArticulos => !IsCargando && Inventario != null;
        public string TotalArticulos => $"Total: {ArticulosInventario.Count} artículos";
        public string ArticulosContados => $"Contados: {ArticulosInventario.Count(a => a.CantidadInventario > 0)}";
        #endregion

        #region Property Change Callbacks
        partial void OnInventarioChanged(InventarioCabeceraDto? oldValue, InventarioCabeceraDto? newValue)
        {
            if (newValue != null)
            {
                _ = CargarArticulosAsync();
            }
            OnPropertyChanged(nameof(CanCargarArticulos));
        }

        partial void OnIsCargandoChanged(bool oldValue, bool newValue)
        {
            OnPropertyChanged(nameof(CanCargarArticulos));
        }

        partial void OnFiltroUbicacionChanged(string oldValue, string newValue)
        {
            AplicarFiltros();
        }

        partial void OnFiltroArticuloChanged(string oldValue, string newValue)
        {
            AplicarFiltros();
        }

        partial void OnUnidadesGlobalesChanged(decimal oldValue, decimal newValue)
        {
            if (SumarUnidades && newValue > 0)
            {
                AplicarUnidadesGlobales();
            }
        }

        partial void OnSumarUnidadesChanged(bool oldValue, bool newValue)
        {
            if (newValue && UnidadesGlobales > 0)
            {
                AplicarUnidadesGlobales();
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                // Ya no necesitamos cargar almacenes para filtros
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CargarArticulosAsync()
        {
            try
            {
                if (Inventario == null) return;

                IsCargando = true;
                MensajeEstado = "Cargando artículos del inventario...";

                // Crear filtro para obtener artículos del inventario
                var filtro = new FiltroArticulosInventarioDto
                {
                    IdInventario = Inventario.IdInventario,
                    CodigoUbicacion = FiltroUbicacion,
                    CodigoArticulo = FiltroArticulo
                };

                // Obtener artículos reales del servicio
                var articulos = await _inventarioService.ObtenerArticulosInventarioAsync(filtro);

                // Guardar todos los artículos y aplicar filtros
                _todosLosArticulos = articulos;
                AplicarFiltros();

                MensajeEstado = $"Cargados {articulos.Count} artículos";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                MessageBox.Show($"Error al cargar artículos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private void LimpiarFiltros()
        {
            FiltroUbicacion = string.Empty;
            FiltroArticulo = string.Empty;
        }

        [RelayCommand]
        private void BuscarUbicacion()
        {
            // Este comando ya no es necesario con filtrado automático
            // Pero lo mantenemos por si se quiere usar para algo específico
        }

        [RelayCommand]
        private void CambiarUbicacion()
        {
            if (ArticuloSeleccionado == null)
            {
                MessageBox.Show("Seleccione un artículo para cambiar su ubicación", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // TODO: Implementar cambio de ubicación
            MessageBox.Show($"Cambiar ubicación del artículo {ArticuloSeleccionado.CodigoArticulo} - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void AnadirArticulo()
        {
            // TODO: Implementar añadir artículo al inventario
            MessageBox.Show("Añadir artículo al inventario - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task GuardarConteoAsync()
        {
            try
            {
                if (!PuedeGuardar) return;

                var articulosModificados = ArticulosInventario
                    .Where(a => a.CantidadInventario > 0)
                    .ToList();

                if (!articulosModificados.Any())
                {
                    MessageBox.Show("No hay artículos con conteo para guardar", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirmacion = MessageBox.Show(
                    $"¿Está seguro de que desea guardar el conteo de {articulosModificados.Count} artículos?",
                    "Confirmar guardado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmacion != MessageBoxResult.Yes) return;

                IsCargando = true;
                MensajeEstado = "Guardando conteo...";

                var dto = new GuardarConteoInventarioDto
                {
                    IdInventario = Inventario!.IdInventario,
                    Articulos = articulosModificados.Select(a => new ArticuloConteoDto
                    {
                        CodigoArticulo = a.CodigoArticulo,
                        CodigoUbicacion = a.CodigoUbicacion,
                        Partida = a.Partida,
                        CantidadInventario = a.CantidadInventario.Value,
                        UsuarioConteo = SessionManager.UsuarioActual!.operario
                    }).ToList()
                };

                var resultado = await _inventarioService.GuardarConteoInventarioAsync(dto);

                if (resultado)
                {
                    MessageBox.Show("Conteo guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                    CerrarDialogo(true);
                }
                else
                {
                    MessageBox.Show("Error al guardar el conteo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                MessageBox.Show($"Error al guardar conteo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            CerrarDialogo(false);
        }
        #endregion

        #region Private Methods
        private void AplicarFiltros()
        {
            if (_todosLosArticulos == null) return;

            var articulosFiltrados = _todosLosArticulos.AsEnumerable();

            // Aplicar filtro de ubicación
            if (!string.IsNullOrWhiteSpace(FiltroUbicacion))
            {
                articulosFiltrados = articulosFiltrados.Where(a => 
                    a.CodigoUbicacion.Contains(FiltroUbicacion, StringComparison.OrdinalIgnoreCase));
            }

            // Aplicar filtro de artículo
            if (!string.IsNullOrWhiteSpace(FiltroArticulo))
            {
                articulosFiltrados = articulosFiltrados.Where(a => 
                    a.CodigoArticulo.Contains(FiltroArticulo, StringComparison.OrdinalIgnoreCase) ||
                    a.DescripcionArticulo.Contains(FiltroArticulo, StringComparison.OrdinalIgnoreCase));
            }

            // Actualizar la colección visible
            ArticulosInventario.Clear();
            foreach (var articulo in articulosFiltrados)
            {
                ArticulosInventario.Add(articulo);
            }

            // Notificar cambios en las propiedades computadas
            OnPropertyChanged(nameof(TotalArticulos));
            OnPropertyChanged(nameof(ArticulosContados));
        }

        private void AplicarUnidadesGlobales()
        {
            if (ArticuloSeleccionado != null)
            {
                ArticuloSeleccionado.CantidadInventario = UnidadesGlobales;
            }
        }

        private void ValidarFormulario()
        {
            var tieneArticulosContados = ArticulosInventario.Any(a => a.CantidadInventario > 0);
            PuedeGuardar = Inventario != null && tieneArticulosContados && !IsCargando;
        }

        private void CerrarDialogo(bool resultado)
        {
            if (Application.Current.Windows.OfType<ContarInventarioDialog>().FirstOrDefault() is ContarInventarioDialog dialog)
            {
                dialog.DialogResult = resultado;
                dialog.Close();
            }
        }
        #endregion
    }
} 