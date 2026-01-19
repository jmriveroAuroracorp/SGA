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
using ClosedXML.Excel;

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

            // Suscribirse a solicitudes de filtro
            InventarioFiltroStore.FiltroSolicitado += OnFiltroSolicitado;

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
        private DateTime fechaDesde = DateTime.Today.AddDays(-2);

        [ObservableProperty]
        private DateTime fechaHasta = DateTime.Today;

        [ObservableProperty]
        private string estadoFiltro = "TODOS"; // TODOS, ABIERTO, EN_CONTEO, CONSOLIDADO, CERRADO

        [ObservableProperty]
        private string idInventarioFiltro = string.Empty;

        [ObservableProperty]
        private bool verTodosLosInventarios = false; // Por defecto, solo ver los propios

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
        public string TotalInventarios
        {
            get
            {
                var total = Inventarios?.Count ?? 0;
                return $"Total: {total} inventario{(total != 1 ? "s" : "")}";
            }
        }
        public string TotalUbicaciones => $"Ubicaciones: {StockUbicaciones.Count}";

        public bool TieneFiltrosActivos
        {
            get
            {
                // Verificar si hay filtros activos
                var almacenActivo = AlmacenSeleccionadoCombo != null && 
                                    AlmacenSeleccionadoCombo.CodigoAlmacen != "Todas";
                var estadoActivo = !string.IsNullOrEmpty(EstadoFiltro) && EstadoFiltro != "TODOS";
                var idActivo = !string.IsNullOrWhiteSpace(IdInventarioFiltro);
                // Siempre mostrar fechas como filtro activo (incluso si son los valores por defecto)
                var fechasActivas = true; // Siempre hay un rango de fechas aplicado

                return almacenActivo || estadoActivo || idActivo || fechasActivas;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

                // Siempre mostrar las fechas (incluso si son los valores por defecto)
                filtros.Add($"Fechas: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}");

                if (AlmacenSeleccionadoCombo != null && AlmacenSeleccionadoCombo.CodigoAlmacen != "Todas")
                {
                    filtros.Add($"Almacén: {AlmacenSeleccionadoCombo.CodigoAlmacen}");
                }

                if (!string.IsNullOrEmpty(EstadoFiltro) && EstadoFiltro != "TODOS")
                {
                    filtros.Add($"Estado: {EstadoFiltro}");
                }

                if (!string.IsNullOrWhiteSpace(IdInventarioFiltro))
                {
                    filtros.Add($"ID: {IdInventarioFiltro}");
                }

                if (VerTodosLosInventarios)
                {
                    filtros.Add("Ver todos");
                }
                else
                {
                    filtros.Add("Solo propios");
                }

                return string.Join(" | ", filtros);
            }
        }
        #endregion

        #region Property Change Callbacks
        partial void OnAlmacenSeleccionadoComboChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            // Notificar cambio en CanCargarInventarios
            OnPropertyChanged(nameof(CanCargarInventarios));
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnFechaDesdeChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la nueva fecha desde, ajustarla
            if (FechaHasta < newValue)
            {
                FechaHasta = newValue;
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnFechaHastaChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la fecha desde, ajustarla
            if (newValue < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnIsCargandoChanged(bool oldValue, bool newValue)
        {
            // Notificar cambios en las propiedades que dependen de IsCargando
            OnPropertyChanged(nameof(CanEnableInputs));
            OnPropertyChanged(nameof(CanCargarInventarios));
        }

        partial void OnEstadoFiltroChanged(string oldValue, string newValue)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnIdInventarioFiltroChanged(string oldValue, string newValue)
        {
            // Refrescar la vista cuando cambie el filtro por ID
            InventariosView.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
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
                
                // Solo cargar inventarios si NO hay un filtro especial pendiente
                // (OnFiltroSolicitado se encargará de cargar con el filtro)
                if (string.IsNullOrEmpty(_filtroEspecial))
                {
                    // Solo cargar inventarios si hay un almacén seleccionado
                    if (AlmacenSeleccionadoCombo != null)
                    {
                        await CargarInventariosAsync();
                    }
                }
                else
                {
                    // Hay filtro especial, OnFiltroSolicitado cargará los datos
                    MensajeEstado = "Aplicando filtro...";
                }

                MensajeEstado = "Listo";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al inicializar: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
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

                // Si hay un filtro especial activo (desde WelcomeView), usar "Todas" automáticamente
                // Si selecciona "Todas", enviar la lista de almacenes autorizados
                string? codigoAlmacen = null;
                List<string>? codigosAlmacen = null;
                
                // Si hay filtro especial o si no hay almacén seleccionado, usar todos los almacenes
                if (!string.IsNullOrEmpty(_filtroEspecial) || AlmacenSeleccionadoCombo == null)
                {
                    // Si los almacenes ya están cargados, usar todos
                    if (AlmacenesCombo?.Any() == true)
                    {
                        codigosAlmacen = AlmacenesCombo
                            .Where(a => a.CodigoAlmacen != "Todas")
                            .Select(a => a.CodigoAlmacen)
                            .ToList();
                    }
                }
                else if (AlmacenSeleccionadoCombo.CodigoAlmacen == "Todas")
                {
                    // Enviar lista de almacenes autorizados (excluyendo "Todas")
                    codigosAlmacen = AlmacenesCombo
                        .Where(a => a.CodigoAlmacen != "Todas")
                        .Select(a => a.CodigoAlmacen)
                        .ToList();
                }
                else
                {
                    codigoAlmacen = AlmacenSeleccionadoCombo.CodigoAlmacen;
                }

                // Si hay un filtro especial activo (desde WelcomeView), pasar null como fechas
                // para que el backend no filtre por fecha y cargue todos los inventarios
                DateTime? fechaDesdeParam = string.IsNullOrEmpty(_filtroEspecial) ? FechaDesde.Date : null;
                DateTime? fechaHastaParam = string.IsNullOrEmpty(_filtroEspecial) ? FechaHasta.Date.AddDays(1).AddSeconds(-1) : null;
                
                var filtro = new FiltroInventarioDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                    CodigoAlmacen = codigoAlmacen,
                    CodigosAlmacen = codigosAlmacen,
                    FechaDesde = fechaDesdeParam,
                    FechaHasta = fechaHastaParam,
                    EstadoInventario = EstadoFiltro == "TODOS" ? null : EstadoFiltro,
                    // Enviar el usuario actual solo si NO está marcado "Ver todos"
                    UsuarioCreacionId = VerTodosLosInventarios ? null : SessionManager.UsuarioActual?.operario
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
                var errorDialog = new WarningDialog("Error", $"Error al cargar inventarios: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
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
                var errorDialog = new WarningDialog("Error", $"Error al cargar rangos: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
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
                var errorDialog = new WarningDialog("Error", $"Error al cargar stock: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
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
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                
                if (result == true)
                    {
                    // Recargar la lista de inventarios
                        await CargarInventariosAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al crear inventario: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task ConsolidarInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                var confirmDialog = new ConfirmationDialog("Confirmar consolidación", "¿Está seguro de que desea consolidar este inventario?");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != confirmDialog)
                    confirmDialog.Owner = owner;
                
                if (confirmDialog.ShowDialog() != true)
                    return;
                {
                    // Primero verificar si hay advertencias SIN consolidar
                    var (success, tieneAdvertencias, lineasConStockCambiado) = await _inventarioService.VerificarAdvertenciasConsolidacionAsync(inventario.IdInventario);
                    
                    if (success)
                    {
                        if (tieneAdvertencias)
                        {
                            var mensaje = $"⚠️ Se detectaron {lineasConStockCambiado.Count} líneas donde el stock real ha cambiado desde que se creó el inventario.\n\n";
                            mensaje += "Esto puede indicar que:\n";
                            mensaje += "• Se han realizado movimientos de stock durante el conteo\n";
                            mensaje += "• Otros usuarios han trabajado en el mismo almacén\n\n";
                            mensaje += "¿Desea revisar y ajustar los valores antes de consolidar el inventario?";
                            
                            var respuestaDialog = new ConfirmationDialog("Stock Cambiado Durante Inventario", mensaje);
                            var ownerRespuesta = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                             ?? Application.Current.MainWindow;
                            if (ownerRespuesta != null && ownerRespuesta != respuestaDialog)
                                respuestaDialog.Owner = ownerRespuesta;
                            
                            if (respuestaDialog.ShowDialog() == true)
                            {
                                // Abrir pantalla de reconteo para ajustar valores
                                var dialog = new ReconteoLineasProblematicasDialog(inventario);
                                var ownerDialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                             ?? Application.Current.MainWindow;
                                if (ownerDialog != null && ownerDialog != dialog)
                                    dialog.Owner = ownerDialog;
                                
                                var result = dialog.ShowDialog();
                                
                                if (result == true)
                                {
                                    // El reconteo se guardó y consolidó automáticamente
                                    var successDialog = new WarningDialog("Éxito", "Inventario consolidado correctamente con los valores ajustados.");
                                    var ownerSuccess = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                                   ?? Application.Current.MainWindow;
                                    if (ownerSuccess != null && ownerSuccess != successDialog)
                                        successDialog.Owner = ownerSuccess;
                                    successDialog.ShowDialog();
                                }
                                else
                                {
                                    // Si se canceló el reconteo, NO consolidar el inventario
                                    var infoDialog = new WarningDialog("Información", "Consolidación cancelada. El inventario permanece sin consolidar.");
                                    var ownerInfo = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                                ?? Application.Current.MainWindow;
                                    if (ownerInfo != null && ownerInfo != infoDialog)
                                        infoDialog.Owner = ownerInfo;
                                    infoDialog.ShowDialog();
                                }
                            }
                            else
                            {
                                // El usuario no quiere revisar, consolidar con los valores originales
                                var (successConsolidacion, _, _) = await _inventarioService.ConsolidarInventarioAsync(inventario.IdInventario);
                                if (successConsolidacion)
                                {
                                    var infoDialog = new WarningDialog("Información", "Inventario consolidado con los valores originales.");
                                    var ownerInfo = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                                ?? Application.Current.MainWindow;
                                    if (ownerInfo != null && ownerInfo != infoDialog)
                                        infoDialog.Owner = ownerInfo;
                                    infoDialog.ShowDialog();
                                }
                            }
                        }
                        else
                        {
                            // No hay advertencias, consolidar directamente
                            var (successConsolidacion, _, _) = await _inventarioService.ConsolidarInventarioAsync(inventario.IdInventario);
                            if (successConsolidacion)
                            {
                                var successDialog = new WarningDialog("Éxito", "Inventario consolidado correctamente.");
                                var ownerSuccess = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                               ?? Application.Current.MainWindow;
                                if (ownerSuccess != null && ownerSuccess != successDialog)
                                    successDialog.Owner = ownerSuccess;
                                successDialog.ShowDialog();
                            }
                        }
                        
                        await CargarInventariosAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al consolidar inventario: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task CerrarInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                var confirmDialog = new ConfirmationDialog("Confirmar cierre", "¿Está seguro de que desea cerrar este inventario? Se generarán los ajustes correspondientes.");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != confirmDialog)
                    confirmDialog.Owner = owner;
                
                if (confirmDialog.ShowDialog() == true)
                {
                    var cerrado = await _inventarioService.CerrarInventarioAsync(inventario.IdInventario);
                    if (cerrado)
                    {
                        var successDialog = new WarningDialog("Éxito", "Inventario cerrado correctamente.");
                        var ownerSuccess = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                       ?? Application.Current.MainWindow;
                        if (ownerSuccess != null && ownerSuccess != successDialog)
                            successDialog.Owner = ownerSuccess;
                        successDialog.ShowDialog();
                        await CargarInventariosAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al cerrar inventario: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task ContarInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                var dialog = new ContarInventarioDialog(inventario);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                
                if (result == true)
                {
                    // Recargar la lista de inventarios para actualizar estados
                    await CargarInventariosAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al abrir conteo: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task VerInventarioAsync(InventarioCabeceraDto inventario)
        {
            try
            {
                // Abrir diálogo para ver detalles del inventario
                var dialog = new VerInventarioDialog(inventario);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al ver inventario: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
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
                    var successDialog = new WarningDialog("Éxito", "Inventarios exportados correctamente.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != successDialog)
                        successDialog.Owner = owner;
                    successDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al exportar: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task AbrirFiltros()
        {
            try
            {
                // Crear el ViewModel del diálogo con los valores actuales
                var dialogViewModel = new FiltrosInventarioDialogViewModel(
                    AlmacenSeleccionadoCombo,
                    FechaDesde,
                    FechaHasta,
                    EstadoFiltro,
                    IdInventarioFiltro,
                    VerTodosLosInventarios
                );

                // Crear y mostrar el diálogo
                var dialog = new FiltrosInventarioDialog(dialogViewModel);

                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo y esperar resultado
                var result = dialog.ShowDialog();

                // Si el usuario hizo clic en "Aplicar" (result == true), aplicar los filtros
                if (result == true)
                {
                    // Actualizar los filtros del ViewModel principal con los valores del diálogo
                    AlmacenSeleccionadoCombo = dialogViewModel.AlmacenSeleccionadoCombo;
                    FechaDesde = dialogViewModel.FechaDesde;
                    FechaHasta = dialogViewModel.FechaHasta;
                    EstadoFiltro = dialogViewModel.EstadoFiltro;
                    IdInventarioFiltro = dialogViewModel.IdInventarioFiltro;
                    VerTodosLosInventarios = dialogViewModel.VerTodosLosInventarios;

                    // Notificar cambios en propiedades calculadas
                    OnPropertyChanged(nameof(TieneFiltrosActivos));
                    OnPropertyChanged(nameof(ResumenFiltrosActivos));

                    // Recargar los inventarios con los nuevos filtros
                    await CargarInventariosAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al abrir el diálogo de filtros: {ex.Message}");
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                if (mainWindow != null && mainWindow != errorDialog)
                    errorDialog.Owner = mainWindow;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void LimpiarFiltros()
        {
            EstadoFiltro = "TODOS";
            IdInventarioFiltro = string.Empty;
            VerTodosLosInventarios = false; // Por defecto, solo ver los propios
            
            // Verificar que las colecciones no estén vacías antes de acceder a FirstOrDefault()
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
            }
            
            // Establecer las fechas: desde hace 2 días hasta hoy
            FechaDesde = DateTime.Today.AddDays(-2);
            FechaHasta = DateTime.Today;

            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
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

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

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
                var errorDialog = new WarningDialog("Error", $"Error al cargar almacenes: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        private string ObtenerNombreEmpresaActual()
        {
            return SessionManager.EmpresaSeleccionada?.ToString() ?? "Sin empresa";
        }

        private string _filtroEspecial = string.Empty;
        private bool _ajustandoFiltrosDesdeEvento = false;

        private async void OnFiltroSolicitado(object? sender, FiltroInventarioEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnFiltroSolicitado recibido: {e.TipoFiltro}");
            
            _ajustandoFiltrosDesdeEvento = true;

            // Aplicar el filtro especial
            _filtroEspecial = e.TipoFiltro switch
            {
                TipoFiltroInventario.Abiertos => "ABIERTO",
                TipoFiltroInventario.EnConteo => "EN_CONTEO",
                TipoFiltroInventario.Consolidados => "CONSOLIDADO",
                TipoFiltroInventario.PendientesCierre => "PENDIENTE_CIERRE",
                TipoFiltroInventario.Cerrados => "CERRADO",
                _ => string.Empty
            };

            System.Diagnostics.Debug.WriteLine($"Filtro especial asignado: {_filtroEspecial}");

            // Ajustar el filtro de estado según el tipo de filtro solicitado
            EstadoFiltro = _filtroEspecial;

            // Asegurar que se muestren solo los propios inventarios del usuario
            VerTodosLosInventarios = false;

            // Forzar notificación de cambio de propiedad
            OnPropertyChanged(nameof(EstadoFiltro));
            OnPropertyChanged(nameof(VerTodosLosInventarios));

            _ajustandoFiltrosDesdeEvento = false;

            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));

            // Asegurar que los almacenes estén cargados antes de cargar inventarios
            if (AlmacenesCombo?.Any() != true)
            {
                System.Diagnostics.Debug.WriteLine("Almacenes no cargados, cargando primero...");
                await CargarAlmacenesAsync();
            }
            
            // SIEMPRE recargar los datos con el filtro aplicado
            // Si está cargando (por ejemplo, cargando almacenes), esperar a que termine
            if (IsCargando)
            {
                // Esperar a que termine la carga actual (máximo 5 segundos)
                var intentos = 0;
                while (IsCargando && intentos < 50)
                {
                    await Task.Delay(100);
                    intentos++;
                }
            }
            
            System.Diagnostics.Debug.WriteLine("Cargando datos con filtro aplicado...");
            MensajeEstado = "Cargando inventarios...";
            await CargarInventariosAsync();
            System.Diagnostics.Debug.WriteLine($"Datos cargados con filtro. Total inventarios: {Inventarios?.Count}");
            InventariosView?.Refresh();
        }

        private bool FiltroInventario(object item)
        {
            if (item is not InventarioCabeceraDto inventario) return false;

            // Filtro por ID de inventario
            if (!string.IsNullOrWhiteSpace(IdInventarioFiltro))
            {
                if (!inventario.IdInventarioCorto.Contains(IdInventarioFiltro, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Filtro por fechas (solo si no hay filtro especial)
            // Cuando hay filtro especial, el backend ya no filtró por fechas, así que tampoco lo hacemos aquí
            if (string.IsNullOrEmpty(_filtroEspecial))
            {
                if (inventario.FechaCreacion.Date < FechaDesde.Date || 
                    inventario.FechaCreacion.Date > FechaHasta.Date)
                    return false;
            }

            return true;
        }

        private async Task ExportarAExcelAsync(string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Inventarios");

                // Configurar encabezados
                var headers = new[]
                {
                    "ID Inventario",
                    "Almacén",
                    "Tipo",
                    "Estado",
                    "Rango Ubicaciones",
                    "Comentarios",
                    "Usuario Creación",
                    "Fecha Creación",
                    "Fecha Cierre",
                    "Total Líneas"
                };

                // Escribir encabezados
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
                    worksheet.Cell(1, i + 1).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                }

                // Escribir datos
                int row = 2;
                foreach (var inventario in Inventarios)
                {
                    // Obtener el total de líneas para este inventario
                    var totalLineas = await ObtenerTotalLineasInventarioAsync(inventario.IdInventario);

                    worksheet.Cell(row, 1).Value = inventario.IdInventarioCorto;
                    worksheet.Cell(row, 2).Value = inventario.CodigoAlmacen;
                    worksheet.Cell(row, 3).Value = inventario.TipoInventarioFormateado;
                    worksheet.Cell(row, 4).Value = inventario.EstadoFormateado;
                    worksheet.Cell(row, 5).Value = inventario.RangoUbicaciones;
                    worksheet.Cell(row, 6).Value = inventario.Comentarios;
                    worksheet.Cell(row, 7).Value = inventario.UsuarioCreacionNombre;
                    worksheet.Cell(row, 8).Value = inventario.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(row, 9).Value = inventario.FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "";
                    worksheet.Cell(row, 10).Value = totalLineas;

                    // Aplicar formato a las celdas de fecha
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                    if (inventario.FechaCierre.HasValue)
                    {
                        worksheet.Cell(row, 9).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
                    }

                    row++;
                }

                // Autoajustar columnas
                worksheet.Columns().AdjustToContents();

                // Agregar información del filtro aplicado
                var infoRow = row + 2;
                worksheet.Cell(infoRow, 1).Value = "INFORMACIÓN DEL FILTRO APLICADO:";
                worksheet.Cell(infoRow, 1).Style.Font.Bold = true;
                worksheet.Cell(infoRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
                worksheet.Range(infoRow, 1, infoRow, headers.Length).Merge();

                var almacenRow = infoRow + 1;
                worksheet.Cell(almacenRow, 1).Value = $"Almacén: {AlmacenSeleccionadoCombo?.CodigoAlmacen ?? "Todos"}";
                worksheet.Cell(almacenRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(almacenRow, 1, almacenRow, headers.Length).Merge();

                var fechaRow = almacenRow + 1;
                worksheet.Cell(fechaRow, 1).Value = $"Período: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}";
                worksheet.Cell(fechaRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(fechaRow, 1, fechaRow, headers.Length).Merge();

                var estadoRow = fechaRow + 1;
                worksheet.Cell(estadoRow, 1).Value = $"Estado: {EstadoFiltro}";
                worksheet.Cell(estadoRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(estadoRow, 1, estadoRow, headers.Length).Merge();

                var idRow = estadoRow + 1;
                if (!string.IsNullOrWhiteSpace(IdInventarioFiltro))
                {
                    worksheet.Cell(idRow, 1).Value = $"ID Filtro: {IdInventarioFiltro}";
                    worksheet.Cell(idRow, 1).Style.Font.FontColor = XLColor.Gray;
                    worksheet.Range(idRow, 1, idRow, headers.Length).Merge();
                }

                var totalRow = idRow + 1;
                worksheet.Cell(totalRow, 1).Value = $"Total inventarios exportados: {Inventarios.Count}";
                worksheet.Cell(totalRow, 1).Style.Font.Bold = true;
                worksheet.Cell(totalRow, 1).Style.Font.FontColor = XLColor.DarkGreen;
                worksheet.Range(totalRow, 1, totalRow, headers.Length).Merge();

                var fechaExportRow = totalRow + 1;
                worksheet.Cell(fechaExportRow, 1).Value = $"Fecha de exportación: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                worksheet.Cell(fechaExportRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(fechaExportRow, 1, fechaExportRow, headers.Length).Merge();

                // Guardar archivo
                workbook.SaveAs(filePath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al generar el archivo Excel: {ex.Message}");
            }
        }

        private async Task<int> ObtenerTotalLineasInventarioAsync(Guid idInventario)
        {
            try
            {
                // Obtener líneas temporales (no consolidadas)
                var lineasTemp = await _inventarioService.ObtenerLineasTemporalesAsync(idInventario);
                
                // Obtener líneas consolidadas
                var lineasConsolidadas = await _inventarioService.ObtenerLineasInventarioAsync(idInventario);
                
                // Retornar el total de ambas
                return lineasTemp.Count + lineasConsolidadas.Count;
            }
            catch (Exception ex)
            {
                // En caso de error, retornar 0
                return 0;
            }
        }
        #endregion
    }
}

