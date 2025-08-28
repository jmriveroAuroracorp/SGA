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

        partial void OnIdInventarioFiltroChanged(string oldValue, string newValue)
        {
            // Refrescar la vista cuando cambie el filtro por ID
            InventariosView.Refresh();
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
                    FechaDesde = FechaDesde.Date, // Solo la fecha, hora 00:00:00
                    FechaHasta = FechaHasta.Date.AddDays(1).AddSeconds(-1), // Último segundo del día (23:59:59)
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

        private bool FiltroInventario(object item)
        {
            if (item is not InventarioCabeceraDto inventario) return false;

            // Filtro por ID de inventario
            if (!string.IsNullOrWhiteSpace(IdInventarioFiltro))
            {
                if (!inventario.IdInventarioCorto.Contains(IdInventarioFiltro, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // TODO: Implementar otros filtros si es necesario
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

