using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class AgregarLineaInventarioDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private readonly InventarioCabeceraDto _inventario;
        #endregion

        #region Constructor
        public AgregarLineaInventarioDialogViewModel(
            InventarioCabeceraDto inventario,
            InventarioService inventarioService,
            StockService stockService)
        {
            _inventario = inventario;
            _inventarioService = inventarioService;
            _stockService = stockService;
            
            CodigoAlmacen = inventario.CodigoAlmacen;
            Ubicaciones = new ObservableCollection<UbicacionDto>();
            AlmacenesDisponibles = new ObservableCollection<AlmacenDto>();
            
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                if (inventario.EsMultialmacen)
                {
                    _ = CargarAlmacenesDelInventarioAsync();
                }
                else
                {
                    _ = CargarAlmacenCompletoAsync();
                    _ = CargarUbicacionesAsync();
                }
            }
        }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        [ObservableProperty]
        private string descripcionArticulo = string.Empty;

        [ObservableProperty]
        private string codigoUbicacion = string.Empty;

        [ObservableProperty]
        private UbicacionDto? ubicacionSeleccionada;

        [ObservableProperty]
        private string codigoAlmacen = string.Empty;

        [ObservableProperty]
        private string? partidaSeleccionada;

        [ObservableProperty]
        private DateTime? fechaCaducidad;

        [ObservableProperty]
        private ObservableCollection<UbicacionDto> ubicaciones;

        [ObservableProperty]
        private ObservableCollection<LoteDto> partidasDisponibles = new ObservableCollection<LoteDto>();

        [ObservableProperty]
        private ObservableCollection<DateTime?> fechasDisponibles = new ObservableCollection<DateTime?>();

        private Dictionary<string, List<DateTime?>> _fechasPorPartida = new Dictionary<string, List<DateTime?>>();

        [ObservableProperty]
        private decimal cantidadContada = 0;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        // Propiedades para multialmacén
        [ObservableProperty]
        private ObservableCollection<AlmacenDto> almacenesDisponibles = new ObservableCollection<AlmacenDto>();

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionado;

        // Propiedad para almacén completo cuando NO es multialmacén
        [ObservableProperty]
        private AlmacenDto? almacenCompleto;

        // Propiedad calculada para saber si es multialmacén
        public bool EsMultialmacen => _inventario?.EsMultialmacen ?? false;

        // Propiedad calculada para mostrar "código - descripción" en ambos casos
        public string AlmacenDisplayText => EsMultialmacen 
            ? (AlmacenSeleccionado?.DescripcionCombo ?? CodigoAlmacen)
            : (AlmacenCompleto?.DescripcionCombo ?? CodigoAlmacen);
        #endregion

        #region Property Change Callbacks
        partial void OnUbicacionSeleccionadaChanged(UbicacionDto? oldValue, UbicacionDto? newValue)
        {
            if (newValue != null)
            {
                CodigoUbicacion = newValue.Ubicacion;
            }
        }

        partial void OnPartidaSeleccionadaChanged(string? oldValue, string? newValue)
        {
            FechasDisponibles.Clear();
            FechaCaducidad = null;

            if (!string.IsNullOrWhiteSpace(newValue) && _fechasPorPartida.ContainsKey(newValue))
            {
                var fechas = _fechasPorPartida[newValue];
                
                // Agregar todas las fechas al ComboBox
                foreach (var fecha in fechas)
                {
                    FechasDisponibles.Add(fecha);
                }

                // Si hay una sola fecha, seleccionarla automáticamente
                if (fechas.Count == 1)
                {
                    FechaCaducidad = fechas[0];
                }
                // Si hay múltiples o ninguna, dejar que el usuario elija o quede vacío
            }
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            if (newValue != null)
            {
                CodigoAlmacen = newValue.CodigoAlmacen;
                // Limpiar selección de ubicación y recargar ubicaciones del nuevo almacén
                UbicacionSeleccionada = null;
                CodigoUbicacion = string.Empty;
                OnPropertyChanged(nameof(AlmacenDisplayText));
                _ = CargarUbicacionesAsync();
            }
        }

        partial void OnAlmacenCompletoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            OnPropertyChanged(nameof(AlmacenDisplayText));
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task CargarUbicacionesAsync()
        {
            try
            {
                IsCargando = true;
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                // Usar el almacén seleccionado si existe, sino el código de almacén
                var codigoAlmacenParaUbicaciones = AlmacenSeleccionado?.CodigoAlmacen ?? CodigoAlmacen;
                var ubicacionesList = await _stockService.ObtenerUbicacionesAsync(codigoAlmacenParaUbicaciones, (short)empresa, soloConStock: false);
                
                Ubicaciones.Clear();
                foreach (var ubicacion in ubicacionesList.OrderBy(u => u.Ubicacion))
                {
                    Ubicaciones.Add(ubicacion);
                }
            }
            catch (Exception ex)
            {
                var error = new WarningDialog("Error", $"Error al cargar ubicaciones: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != error)
                    error.Owner = owner;
                error.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }

        private async Task CargarAlmacenCompletoAsync()
        {
            if (EsMultialmacen) return; // Ya se carga en CargarAlmacenesDelInventarioAsync
            
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);
                AlmacenCompleto = almacenesAutorizados.FirstOrDefault(a => a.CodigoAlmacen == CodigoAlmacen);
                OnPropertyChanged(nameof(AlmacenDisplayText));
            }
            catch (Exception ex)
            {
                // En caso de error, continuar sin nombre (solo mostrará el código)
                System.Diagnostics.Debug.WriteLine($"Error al cargar almacén completo: {ex.Message}");
            }
        }

        private async Task CargarAlmacenesDelInventarioAsync()
        {
            try
            {
                IsCargando = true;
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                // Obtener todos los almacenes autorizados
                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

                // Filtrar solo los almacenes que están en el inventario
                var codigosAlmacenInventario = _inventario.CodigosAlmacen ?? new List<string>();
                var almacenesDelInventario = almacenesAutorizados
                    .Where(a => codigosAlmacenInventario.Contains(a.CodigoAlmacen))
                    .OrderBy(a => a.CodigoAlmacen)
                    .ToList();

                AlmacenesDisponibles.Clear();
                foreach (var almacen in almacenesDelInventario)
                {
                    AlmacenesDisponibles.Add(almacen);
                }

                // Establecer el primer almacén como seleccionado por defecto
                if (AlmacenesDisponibles.Any())
                {
                    AlmacenSeleccionado = AlmacenesDisponibles.First();
                }
                else
                {
                    // Si no se encontraron almacenes, usar el código del inventario como fallback
                    CodigoAlmacen = _inventario.CodigoAlmacen;
                    _ = CargarUbicacionesAsync();
                }
            }
            catch (Exception ex)
            {
                var error = new WarningDialog("Error", $"Error al cargar almacenes: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != error)
                    error.Owner = owner;
                error.ShowDialog();
                // Fallback: usar el código de almacén del inventario
                CodigoAlmacen = _inventario.CodigoAlmacen;
                _ = CargarUbicacionesAsync();
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task BuscarArticuloAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    var warning = new WarningDialog("Buscar artículo", "Introduce un código de artículo para buscar.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                IsCargando = true;
                MensajeEstado = "Buscando artículo...";

                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var codigoArticuloBuscado = CodigoArticulo.Trim();

                // Buscar artículo por código sin filtrar por almacén (para validar que existe en el sistema)
                var articulo = await _stockService.BuscarArticuloPorCodigoAsync(empresa, codigoArticuloBuscado);

                if (articulo == null)
                {
                    DescripcionArticulo = string.Empty;
                    PartidasDisponibles.Clear();
                    PartidaSeleccionada = null;
                    var warning = new WarningDialog(
                        "Artículo no encontrado",
                        $"No se encontró el artículo '{codigoArticuloBuscado}' en el sistema.\n\nVerifica que el código sea correcto.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    MensajeEstado = "Artículo no encontrado";
                }
                else
                {
                    CodigoArticulo = articulo.CodigoArticulo;
                    DescripcionArticulo = articulo.DescripcionArticulo;
                    MensajeEstado = "Artículo encontrado. Cargando lotes...";

                    // Cargar lotes activos del artículo
                    await CargarLotesActivosAsync();
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var error = new WarningDialog("Error", $"Error al buscar artículo: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != error)
                    error.Owner = owner;
                error.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }

        private async Task CargarLotesActivosAsync()
        {
            try
            {
                PartidasDisponibles.Clear();
                PartidaSeleccionada = null;
                _fechasPorPartida.Clear();
                FechaCaducidad = null;

                var lotes = await _stockService.ObtenerLotesActivosAsync(
                    (short)SessionManager.EmpresaSeleccionada!.Value,
                    CodigoArticulo,
                    incluirHistoricos: true); // Para inventarios, incluir lotes históricos

                if (lotes == null || !lotes.Any())
                {
                    MensajeEstado = "Artículo encontrado. No hay lotes activos disponibles.";
                    return;
                }

                // Agrupar lotes por partida para manejar múltiples fechas
                var lotesAgrupados = lotes
                    .Where(l => !string.IsNullOrWhiteSpace(l.Partida))
                    .GroupBy(l => l.Partida)
                    .ToList();

                foreach (var grupo in lotesAgrupados)
                {
                    var partida = grupo.Key;
                    if (string.IsNullOrWhiteSpace(partida))
                        continue;

                    var fechas = grupo.Where(l => l.FechaCaducidad.HasValue)
                                     .Select(l => l.FechaCaducidad.Value)
                                     .Distinct()
                                     .OrderBy(f => f)
                                     .Cast<DateTime?>()
                                     .ToList();

                    // Agregar la primera entrada del grupo a PartidasDisponibles (para mostrar en el ComboBox)
                    PartidasDisponibles.Add(new LoteDto
                    {
                        Partida = partida,
                        FechaCaducidad = fechas.FirstOrDefault()
                    });

                    // Guardar todas las fechas asociadas a cada partida
                    _fechasPorPartida[partida] = fechas;
                }

                if (PartidasDisponibles.Count == 0)
                {
                    MensajeEstado = "Artículo encontrado. No hay lotes activos disponibles.";
                }
                else
                {
                    MensajeEstado = $"Artículo encontrado. {PartidasDisponibles.Count} lote(s) disponible(s).";
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error al cargar lotes: {ex.Message}";
                // No mostrar error crítico, solo log
            }
        }

        [RelayCommand]
        private async Task GuardarAsync()
        {
            try
            {
                // Validaciones
                if (string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    var warning = new WarningDialog("Validación", "Debes buscar y seleccionar un artículo.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                if (UbicacionSeleccionada == null)
                {
                    var warning = new WarningDialog("Validación", "Debes seleccionar una ubicación.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                if (string.IsNullOrWhiteSpace(PartidaSeleccionada))
                {
                    var warning = new WarningDialog("Validación", "Debes seleccionar una partida.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                if (!FechaCaducidad.HasValue)
                {
                    var warning = new WarningDialog("Validación", "La fecha de caducidad es obligatoria.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                if (CantidadContada <= 0)
                {
                    var warning = new WarningDialog("Validación", "La cantidad contada debe ser mayor que 0.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                // 🔷 CORREGIDO: Validar almacén en inventarios multialmacén
                if (EsMultialmacen && AlmacenSeleccionado == null)
                {
                    var warning = new WarningDialog("Validación", "Debes seleccionar un almacén.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    return;
                }

                IsCargando = true;
                MensajeEstado = "Guardando línea...";

                // Crear DTO para guardar
                // Normalizar fecha para que solo tenga la parte de fecha (sin hora) y evitar problemas de conversión en SQL
                DateTime? fechaCaducidadNormalizada = FechaCaducidad.HasValue 
                    ? FechaCaducidad.Value.Date 
                    : null;

                // 🔷 CORREGIDO: Asegurar que siempre se use el almacén correcto
                string codigoAlmacenParaGuardar;
                if (EsMultialmacen)
                {
                    codigoAlmacenParaGuardar = AlmacenSeleccionado!.CodigoAlmacen;
                }
                else
                {
                    codigoAlmacenParaGuardar = CodigoAlmacen;
                }

                var dto = new GuardarConteoInventarioDto
                {
                    IdInventario = _inventario.IdInventario,
                    Articulos = new System.Collections.Generic.List<ArticuloConteoDto>
                    {
                        new ArticuloConteoDto
                        {
                            CodigoArticulo = CodigoArticulo,
                            CodigoUbicacion = CodigoUbicacion,
                            CodigoAlmacen = codigoAlmacenParaGuardar,
                            Partida = PartidaSeleccionada ?? string.Empty,
                            FechaCaducidad = fechaCaducidadNormalizada,
                            PaletId = null,
                            CantidadInventario = CantidadContada,
                            UsuarioConteo = SessionManager.UsuarioActual!.operario
                        }
                    }
                };

                var resultado = await _inventarioService.GuardarConteoInventarioAsync(dto);

                if (resultado)
                {
                    MensajeEstado = "Línea guardada correctamente";
                    CerrarDialogo(true);
                }
                else
                {
                    var error = new WarningDialog("Error", "Error al guardar la línea.");
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != error)
                        error.Owner = owner;
                    error.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var error = new WarningDialog("Error", $"Error al guardar línea: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != error)
                    error.Owner = owner;
                error.ShowDialog();
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
        private void CerrarDialogo(bool resultado)
        {
            if (Application.Current.Windows.OfType<AgregarLineaInventarioDialog>().FirstOrDefault() is AgregarLineaInventarioDialog dialog)
            {
                dialog.DialogResult = resultado;
                dialog.Close();
            }
        }
        #endregion
    }
}

