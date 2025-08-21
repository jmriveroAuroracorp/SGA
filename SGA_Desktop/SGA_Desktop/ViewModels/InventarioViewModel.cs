using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
    public partial class InventarioViewModel : ObservableObject
    {
        #region Constants
        private const string TODAS = "Todas";
        #endregion

        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        #endregion

        #region Constructor
        public InventarioViewModel(InventarioService inventarioService, StockService stockService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            
            EmpresaActual = ObtenerNombreEmpresaActual();
            AlmacenesCombo = new ObservableCollection<AlmacenDto>();
            Inventarios = new ObservableCollection<InventarioCabeceraDto>();
            StockUbicaciones = new ObservableCollection<StockUbicacionDto>();

            InventariosView = CollectionViewSource.GetDefaultView(Inventarios);
            InventariosView.Filter = new Predicate<object>(FiltroInventario);

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public InventarioViewModel() : this(new InventarioService(), new StockService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private string empresaActual;

        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
        public ObservableCollection<InventarioCabeceraDto> Inventarios { get; }
        public ObservableCollection<StockUbicacionDto> StockUbicaciones { get; }

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionadoCombo;

        [ObservableProperty]
        private InventarioCabeceraDto? inventarioSeleccionado;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private DateTime fechaDesde = DateTime.Today.AddDays(-30);

        [ObservableProperty]
        private DateTime fechaHasta = DateTime.Today;

        [ObservableProperty]
        private string estadoFiltro = "TODOS"; // TODOS, ABIERTO, PENDIENTE_CIERRE, CERRADO

        // Propiedades para rangos de ubicaciones
        [ObservableProperty]
        private int? pasilloDesde;

        [ObservableProperty]
        private int? pasilloHasta;

        [ObservableProperty]
        private int? estanteriaDesde;

        [ObservableProperty]
        private int? estanteriaHasta;

        [ObservableProperty]
        private int? alturaDesde;

        [ObservableProperty]
        private int? alturaHasta;

        [ObservableProperty]
        private int? posicionDesde;

        [ObservableProperty]
        private int? posicionHasta;

        // Propiedades para rangos disponibles
        [ObservableProperty]
        private RangosDisponiblesDto? rangosDisponibles;

        public ICollectionView InventariosView { get; }
        #endregion

        #region Computed Properties
        public bool CanEnableInputs => !IsCargando;
        public bool CanCargarInventarios => !IsCargando && AlmacenSeleccionadoCombo != null;
        public string TotalInventarios => $"Total: {Inventarios.Count} inventarios";
        public string TotalUbicaciones => $"Ubicaciones: {StockUbicaciones.Count}";
        #endregion

        #region Property Change Callbacks
        partial void OnAlmacenSeleccionadoComboChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            // Notificar cambio en CanCargarInventarios
            OnPropertyChanged(nameof(CanCargarInventarios));
        }

        partial void OnFechaDesdeChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la nueva fecha desde, ajustarla
            if (FechaHasta < newValue)
            {
                FechaHasta = newValue;
            }
        }

        partial void OnFechaHastaChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la fecha desde, ajustarla
            if (newValue < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
        }

        partial void OnIsCargandoChanged(bool oldValue, bool newValue)
        {
            // Notificar cambios en las propiedades que dependen de IsCargando
            OnPropertyChanged(nameof(CanEnableInputs));
            OnPropertyChanged(nameof(CanCargarInventarios));
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando almacenes...";

                await CargarAlmacenesAsync();
                
                // Solo cargar inventarios si hay un almacén seleccionado
                if (AlmacenSeleccionadoCombo != null)
                {
                await CargarInventariosAsync();
                }

                MensajeEstado = "Listo";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                MessageBox.Show($"Error al inicializar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task CargarInventariosAsync()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando inventarios...";

                // Si selecciona "Todas", enviar la lista de almacenes autorizados
                string? codigoAlmacen = null;
                List<string>? codigosAlmacen = null;
                
                if (AlmacenSeleccionadoCombo?.CodigoAlmacen == "Todas")
                {
                    // Enviar lista de almacenes autorizados (excluyendo "Todas")
                    codigosAlmacen = AlmacenesCombo
                        .Where(a => a.CodigoAlmacen != "Todas")
                        .Select(a => a.CodigoAlmacen)
                        .ToList();
                }
                else
                {
                    codigoAlmacen = AlmacenSeleccionadoCombo?.CodigoAlmacen;
                }

                var filtro = new FiltroInventarioDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    CodigoAlmacen = codigoAlmacen,
                    CodigosAlmacen = codigosAlmacen,
                    FechaDesde = FechaDesde,
                    FechaHasta = FechaHasta,
                    EstadoInventario = EstadoFiltro == "TODOS" ? null : EstadoFiltro
                };



                var inventarios = await _inventarioService.ObtenerInventariosAsync(filtro);

                Inventarios.Clear();
                foreach (var inventario in inventarios)
                {
                    Inventarios.Add(inventario);
                }

                // Notificar cambio en TotalInventarios
                OnPropertyChanged(nameof(TotalInventarios));

                MensajeEstado = $"Cargados {inventarios.Count} inventarios";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                Debug.WriteLine($"Error al cargar inventarios: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Error al cargar inventarios: {ex.Message}\n\nStackTrace: {ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task CargarRangosDisponiblesAsync()
        {
            try
            {
                if (AlmacenSeleccionadoCombo == null) return;

                IsCargando = true;
                MensajeEstado = "Cargando rangos disponibles...";

                RangosDisponibles = await _inventarioService.ObtenerRangosDisponiblesAsync(
                    SessionManager.EmpresaSeleccionada!.Value,
                    AlmacenSeleccionadoCombo.CodigoAlmacen);

                MensajeEstado = $"Rangos cargados: {RangosDisponibles.TotalUbicaciones} ubicaciones disponibles";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                MessageBox.Show($"Error al cargar rangos: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
            }
        }



        [RelayCommand]
        private async Task CargarStockUbicacionesAsync()
        {
            try
            {
                if (AlmacenSeleccionadoCombo == null) return;

                IsCargando = true;
                MensajeEstado = "Cargando stock de ubicaciones...";

                var stockData = await _inventarioService.ObtenerStockUbicacionesAsync(
                    SessionManager.EmpresaSeleccionada!.Value,
                    AlmacenSeleccionadoCombo.CodigoAlmacen,
                    PasilloDesde, PasilloHasta,
                    EstanteriaDesde, EstanteriaHasta,
                    AlturaDesde, AlturaHasta,
                    PosicionDesde, PosicionHasta);

                StockUbicaciones.Clear();
                foreach (var stock in stockData)
                {
                    StockUbicaciones.Add(stock);
                }

                MensajeEstado = $"Cargadas {stockData.Count} ubicaciones";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                MessageBox.Show($"Error al cargar stock: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task CrearInventarioAsync()
        {
            try
            {
                var dialog = new CrearInventarioDialog();
                dialog.Owner = Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                
                if (result == true)
                    {
                    // Recargar la lista de inventarios
                        await CargarInventariosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear inventario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ConsolidarInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                var resultado = MessageBox.Show(
                    "¿Está seguro de que desea consolidar este inventario?",
                    "Confirmar consolidación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    var consolidado = await _inventarioService.ConsolidarInventarioAsync(inventario.IdInventario);
                    if (consolidado)
                    {
                        MessageBox.Show("Inventario consolidado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        await CargarInventariosAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al consolidar inventario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CerrarInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                var resultado = MessageBox.Show(
                    "¿Está seguro de que desea cerrar este inventario? Se generarán los ajustes correspondientes.",
                    "Confirmar cierre",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    var cerrado = await _inventarioService.CerrarInventarioAsync(inventario.IdInventario);
                    if (cerrado)
                    {
                        MessageBox.Show("Inventario cerrado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                        await CargarInventariosAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cerrar inventario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ContarInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                var dialog = new ContarInventarioDialog(inventario);
                dialog.Owner = Application.Current.MainWindow;
                
                var result = dialog.ShowDialog();
                
                if (result == true)
                {
                    // Recargar la lista de inventarios para actualizar estados
                    await CargarInventariosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir conteo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task VerInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                // TODO: Abrir diálogo para ver detalles del inventario
                MessageBox.Show($"Ver detalles del inventario {inventario.IdInventario} - En desarrollo", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al ver inventario: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ExportarInventariosAsync()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Archivos Excel (*.xlsx)|*.xlsx",
                    FileName = $"Inventarios_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    await ExportarAExcelAsync(saveFileDialog.FileName);
                    MessageBox.Show("Inventarios exportados correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Private Methods
        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

                AlmacenesCombo.Clear();

                // Añadir opción "Todas"
                AlmacenesCombo.Add(new AlmacenDto
                {
                    CodigoAlmacen = "Todas",
                    NombreAlmacen = "Todas",
                    CodigoEmpresa = empresa
                });

                foreach (var a in resultado)
                    AlmacenesCombo.Add(a);

                AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar almacenes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ObtenerNombreEmpresaActual()
        {
            return SessionManager.EmpresaSeleccionada?.ToString() ?? "Sin empresa";
        }

        private bool FiltroInventario(object item)
        {
            if (item is not InventarioCabeceraDto inventario) return false;

            // TODO: Implementar filtros si es necesario
            return true;
        }

        private async Task ExportarAExcelAsync(string filePath)
        {
            // TODO: Implementar exportación a Excel
            await Task.Delay(1000); // Placeholder
        }
        #endregion
    }
}

