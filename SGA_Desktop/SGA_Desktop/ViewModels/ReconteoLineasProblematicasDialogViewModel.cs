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
using System.Windows.Controls;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
    public partial class ReconteoLineasProblematicasDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService; // ← NUEVO
        private List<Models.LineaProblematicaDto> _todasLasLineas = new();
        private readonly Dictionary<string, decimal> _cachePreciosMedios = new(); // ← NUEVO
        #endregion

        #region Constructor
        public ReconteoLineasProblematicasDialogViewModel(InventarioService inventarioService, StockService stockService)
            : this(inventarioService, stockService, new LoginService()) { }

        public ReconteoLineasProblematicasDialogViewModel(InventarioService inventarioService, StockService stockService, LoginService loginService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            _loginService = loginService; // ← NUEVO
            
            LineasProblematicas = new ObservableCollection<LineaProblematicaDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ReconteoLineasProblematicasDialogViewModel() : this(new InventarioService(), new StockService(), new LoginService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private InventarioCabeceraDto? inventario;

        public ObservableCollection<LineaProblematicaDto> LineasProblematicas { get; }

        [ObservableProperty]
        private LineaProblematicaDto? lineaSeleccionada;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private decimal unidadesGlobales = 0;

        [ObservableProperty]
        private bool puedeGuardar = false;

        [ObservableProperty]
        private decimal limiteOperarioEuros = 1000m; // TODO: Obtener desde API

        [ObservableProperty]
        private decimal limiteOperarioUnidades = 0m; // Límite en unidades

        [ObservableProperty] 
        private decimal valorDiferenciasActual = 0;

        [ObservableProperty]
        private decimal unidadesDiferenciasActual = 0;

        [ObservableProperty]
        private bool limiteSuperado = false;
        #endregion

        #region Computed Properties
        public string TotalLineasProblematicas => $"Total: {LineasProblematicas.Count} líneas problemáticas";
        public string LineasRecontadas => $"Recontadas: {LineasProblematicas.Count(l => l.CantidadReconteo.HasValue)}";
        public string EstadoLimite 
        {
            get
            {
                var estado = "";
                
                if (LimiteOperarioEuros > 0)
                {
                    var estadoEuros = ValorDiferenciasActual > LimiteOperarioEuros ? "⚠️" : "✅";
                    estado += $"{estadoEuros} Euros: {ValorDiferenciasActual:C2} / {LimiteOperarioEuros:C2}";
                }
                
                if (LimiteOperarioUnidades > 0)
                {
                    if (!string.IsNullOrEmpty(estado)) estado += " | ";
                    var estadoUnidades = UnidadesDiferenciasActual > LimiteOperarioUnidades ? "⚠️" : "✅";
                    estado += $"{estadoUnidades} Unidades: {UnidadesDiferenciasActual:F2} / {LimiteOperarioUnidades:F2}";
                }
                
                return string.IsNullOrEmpty(estado) ? "Sin límites establecidos" : estado;
            }
        }
        #endregion

        #region Property Change Callbacks
        partial void OnInventarioChanged(InventarioCabeceraDto? oldValue, InventarioCabeceraDto? newValue)
        {
            if (newValue != null)
            {
                _ = CargarLineasProblematicasAsync();
            }
        }

        partial void OnUnidadesGlobalesChanged(decimal oldValue, decimal newValue)
        {
            if (newValue > 0 && LineaSeleccionada != null)
            {
                LineaSeleccionada.CantidadReconteo = newValue;
                ValidarFormulario();
            }
        }

        partial void OnLineaSeleccionadaChanged(LineaProblematicaDto? oldValue, LineaProblematicaDto? newValue)
        {
            ValidarFormulario();
        }

        partial void OnIsCargandoChanged(bool oldValue, bool newValue)
        {
            ValidarFormulario(); // Revalidar cuando cambie IsCargando
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                // Cargar límites del operario actual
                if (SessionManager.UsuarioActual?.operario != null)
                {
                    var operarioId = SessionManager.UsuarioActual.operario;
                    LimiteOperarioEuros = await _loginService.ObtenerLimiteInventarioOperarioAsync(operarioId);
                    LimiteOperarioUnidades = await _loginService.ObtenerLimiteUnidadesOperarioAsync(operarioId);
                }
                else
                {
                    LimiteOperarioEuros = 0m; // Sin operario = sin límite
                    LimiteOperarioUnidades = 0m; // Sin operario = sin límite
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al inicializar: {ex.Message}");
                var ownerInit = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                            ?? Application.Current.MainWindow;
                if (ownerInit != null && ownerInit != errorDialog)
                    errorDialog.Owner = ownerInit;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void SeleccionarLinea(LineaProblematicaDto? linea)
        {
            if (linea == null) return;

            // Deseleccionar todas las líneas
            foreach (var item in LineasProblematicas)
            {
                item.IsSelected = false;
            }
            
            // Seleccionar la línea actual
            linea.IsSelected = true;
            LineaSeleccionada = linea;
        }

        [RelayCommand]
        private async Task CargarLineasProblematicasAsync()
        {
            try
            {
                if (Inventario == null) return;

                IsCargando = true;
                MensajeEstado = "Cargando líneas problemáticas...";

                // Obtener líneas problemáticas del inventario
                var lineas = await _inventarioService.ObtenerLineasProblematicasAsync(Inventario.IdInventario);

                // Guardar todas las líneas
                _todasLasLineas = lineas;
                
                // Actualizar la colección visible
                LineasProblematicas.Clear();
                foreach (var linea in lineas)
                {
                    var lineaDto = new LineaProblematicaDto
                    {
                        CodigoArticulo = linea.CodigoArticulo,
                        DescripcionArticulo = linea.DescripcionArticulo,
                        CodigoAlmacen = linea.CodigoAlmacen,
                        CodigoUbicacion = linea.CodigoUbicacion,
                        Partida = linea.Partida,
                        FechaCaducidad = linea.FechaCaducidad,
                        PaletId = linea.PaletId,
                        StockAlCrearInventario = linea.StockAlCrearInventario,
                        StockActual = linea.StockActual,
                        Palets = linea.Palets ?? new(),
                        StockTotalActual = linea.StockTotalActual,
                        StockPaletizadoActual = linea.StockPaletizadoActual,
                        // 🔷 CORREGIDO: Usar CantidadContada del API si tiene un valor guardado (diferente de 0 o diferente de StockActual).
                        // Si CantidadContada es 0 y es igual a StockActual, probablemente no hay reconteo guardado aún,
                        // así que usamos StockActual como valor inicial.
                        // Si CantidadContada tiene un valor (aunque sea 0 pero diferente de StockActual), usamos ese valor.
                        CantidadReconteo = (linea.CantidadContada != 0 || linea.CantidadContada != linea.StockActual) 
                                          ? linea.CantidadContada 
                                          : linea.StockActual
                    };
                    
                    // Suscribirse a los cambios de CantidadReconteo
                    lineaDto.PropertyChanged += OnLineaPropertyChanged;
                    
                    LineasProblematicas.Add(lineaDto);
                }

                // Forzar actualización de las propiedades computadas
                OnPropertyChanged(nameof(TotalLineasProblematicas));
                OnPropertyChanged(nameof(LineasRecontadas));

                // Validar formulario después de cargar las líneas
                ValidarFormulario();

                MensajeEstado = $"Cargadas {lineas.Count} líneas problemáticas";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al cargar líneas problemáticas: {ex.Message}");
                var ownerLoad = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                            ?? Application.Current.MainWindow;
                if (ownerLoad != null && ownerLoad != errorDialog)
                    errorDialog.Owner = ownerLoad;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task ActualizarStockAsync()
        {
            try
            {
                if (Inventario == null) return;

                IsCargando = true;
                MensajeEstado = "Actualizando información de stock...";

                // Recargar líneas problemáticas para obtener stock actualizado
                await CargarLineasProblematicasAsync();

                MensajeEstado = "Stock actualizado correctamente";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al actualizar stock: {ex.Message}");
                var ownerUpdate = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                              ?? Application.Current.MainWindow;
                if (ownerUpdate != null && ownerUpdate != errorDialog)
                    errorDialog.Owner = ownerUpdate;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task GuardarReconteoAsync()
        {
            try
            {
                if (!PuedeGuardar) return;

                // NUEVA VALIDACIÓN: Verificar límites del operario antes de guardar
                var limiteSuperado = await ValidarLimitesOperarioAntesDeGuardarAsync();
                if (limiteSuperado)
                {
                    return; // No guardar si se superan los límites
                }

                var lineasRecontadas = LineasProblematicas
                    .Where(l => l.CantidadReconteo.HasValue)
                    .ToList();

                if (!lineasRecontadas.Any())
                {
                    var infoDialog = new WarningDialog("Info", "No hay líneas con reconteo para guardar");
                    var ownerInfo = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                ?? Application.Current.MainWindow;
                    if (ownerInfo != null && ownerInfo != infoDialog)
                        infoDialog.Owner = ownerInfo;
                    infoDialog.ShowDialog();
                    return;
                }

                var confirmacion = new ConfirmationDialog("Confirmar reconteo", $"¿Está seguro de que desea guardar el reconteo de {lineasRecontadas.Count} líneas?");
                var ownerConfirm = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;
                if (ownerConfirm != null && ownerConfirm != confirmacion)
                    confirmacion.Owner = ownerConfirm;
                if (confirmacion.ShowDialog() != true) return;

                IsCargando = true;
                MensajeEstado = "Guardando reconteo...";

                var dto = new GuardarReconteoDto
                {
                    IdInventario = Inventario!.IdInventario,
                    LineasRecontadas = lineasRecontadas.Select(l => new LineaReconteoDto
                    {
                        CodigoArticulo = l.CodigoArticulo,
                        CodigoUbicacion = l.CodigoUbicacion,
                        CodigoAlmacen = l.CodigoAlmacen,
                        Partida = l.Partida,
                        PaletId = l.PaletId,
                        CantidadReconteo = l.CantidadReconteo.Value,
                        UsuarioReconteo = SessionManager.UsuarioActual!.operario
                    }).ToList()
                };

                var resultado = await _inventarioService.GuardarReconteoAsync(dto);

                if (resultado)
                {
                    // Después de guardar el reconteo, consolidar automáticamente el inventario
                    MensajeEstado = "Consolidando inventario...";
                    
                    var (success, tieneAdvertencias, lineasConStockCambiado) = await _inventarioService.ConsolidarInventarioAsync(Inventario!.IdInventario);
                    
                    if (success)
                    {
                        if (tieneAdvertencias)
                        {
                            var warningDialog = new WarningDialog("Éxito con advertencias", "Reconteo guardado e inventario consolidado correctamente. Se detectaron nuevas variaciones de stock que requieren atención.");
                            var ownerWarning = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                           ?? Application.Current.MainWindow;
                            if (ownerWarning != null && ownerWarning != warningDialog)
                                warningDialog.Owner = ownerWarning;
                            warningDialog.ShowDialog();
                        }
                        else
                        {
                            var successDialog = new WarningDialog("Éxito", "Reconteo guardado e inventario consolidado correctamente.");
                            var ownerSuccess = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                           ?? Application.Current.MainWindow;
                            if (ownerSuccess != null && ownerSuccess != successDialog)
                                successDialog.Owner = ownerSuccess;
                            successDialog.ShowDialog();
                        }
                        CerrarDialogo(true);
                    }
                    else
                    {
                        var warningDialog = new WarningDialog("Advertencia", "Reconteo guardado correctamente, pero hubo un error al consolidar el inventario.");
                        var ownerAdv = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                    ?? Application.Current.MainWindow;
                        if (ownerAdv != null && ownerAdv != warningDialog)
                            warningDialog.Owner = ownerAdv;
                        warningDialog.ShowDialog();
                        CerrarDialogo(true);
                    }
                }
                else
                {
                    var errorDialog = new WarningDialog("Error", "Error al guardar el reconteo.");
                    var ownerError = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                 ?? Application.Current.MainWindow;
                    if (ownerError != null && ownerError != errorDialog)
                        errorDialog.Owner = ownerError;
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al guardar reconteo: {ex.Message}");
                var ownerCatch = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                if (ownerCatch != null && ownerCatch != errorDialog)
                    errorDialog.Owner = ownerCatch;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                ValidarFormulario(); // Revalidar después de cambiar IsCargando
            }
        }

        [RelayCommand]
        private async Task GuardarYContinuarAsync()
        {
            try
            {
                if (!PuedeGuardar) return;

                // NUEVA VALIDACIÓN: Verificar límites del operario antes de guardar
                var limiteSuperado = await ValidarLimitesOperarioAntesDeGuardarAsync();
                if (limiteSuperado)
                {
                    return; // No guardar si se superan los límites
                }

                var lineasRecontadas = LineasProblematicas
                    .Where(l => l.CantidadReconteo.HasValue)
                    .ToList();

                if (!lineasRecontadas.Any())
                {
                    var infoDialog = new WarningDialog("Info", "No hay líneas con reconteo para guardar");
                    var ownerInfo = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                ?? Application.Current.MainWindow;
                    if (ownerInfo != null && ownerInfo != infoDialog)
                        infoDialog.Owner = ownerInfo;
                    infoDialog.ShowDialog();
                    return;
                }

                var confirmacion = new ConfirmationDialog("Confirmar reconteo parcial", $"¿Desea guardar el progreso de {lineasRecontadas.Count} líneas y seguir reinventariando?");
                var ownerConfirm = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                               ?? Application.Current.MainWindow;
                if (ownerConfirm != null && ownerConfirm != confirmacion)
                    confirmacion.Owner = ownerConfirm;
                if (confirmacion.ShowDialog() != true) return;

                IsCargando = true;
                MensajeEstado = "Guardando progreso...";

                var dto = new GuardarReconteoDto
                {
                    IdInventario = Inventario!.IdInventario,
                    LineasRecontadas = lineasRecontadas.Select(l => new LineaReconteoDto
                    {
                        CodigoArticulo = l.CodigoArticulo,
                        CodigoUbicacion = l.CodigoUbicacion,
                        CodigoAlmacen = l.CodigoAlmacen,
                        Partida = l.Partida,
                        PaletId = l.PaletId,
                        CantidadReconteo = l.CantidadReconteo.Value,
                        UsuarioReconteo = SessionManager.UsuarioActual!.operario
                    }).ToList()
                };

                var resultado = await _inventarioService.GuardarReconteoAsync(dto);

                if (resultado)
                {
                    var successDialog = new WarningDialog("Éxito", "Progreso guardado. Puedes seguir reinventariando.");
                    var ownerSuccess = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                   ?? Application.Current.MainWindow;
                    if (ownerSuccess != null && ownerSuccess != successDialog)
                        successDialog.Owner = ownerSuccess;
                    successDialog.ShowDialog();

                    // Recargar líneas problemáticas para reflejar cambios sin cerrar el diálogo
                    await CargarLineasProblematicasAsync();
                }
                else
                {
                    var errorDialog = new WarningDialog("Error", "Error al guardar el progreso.");
                    var ownerError = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                 ?? Application.Current.MainWindow;
                    if (ownerError != null && ownerError != errorDialog)
                        errorDialog.Owner = ownerError;
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al guardar progreso: {ex.Message}");
                var ownerCatch = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                if (ownerCatch != null && ownerCatch != errorDialog)
                    errorDialog.Owner = ownerCatch;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                ValidarFormulario();
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            CerrarDialogo(false);
        }
        #endregion

        #region Private Methods
        private void ValidarFormulario()
        {
            var tieneLineasRecontadas = LineasProblematicas.Any(l => l.CantidadReconteo.HasValue);
            var nuevoPuedeGuardar = Inventario != null && tieneLineasRecontadas && !IsCargando;
            
            // Solo actualizar si hay cambio real
            if (PuedeGuardar != nuevoPuedeGuardar)
            {
                PuedeGuardar = nuevoPuedeGuardar;
            }
            
            // Notificar cambios en las propiedades computadas
            OnPropertyChanged(nameof(TotalLineasProblematicas));
            OnPropertyChanged(nameof(LineasRecontadas));
        }

        private async Task<decimal> ObtenerPrecioMedioAsync(string codigoArticulo, string codigoAlmacen)
        {
            var clave = $"{codigoArticulo}|{codigoAlmacen}";
            if (_cachePreciosMedios.TryGetValue(clave, out var precio))
                return precio;

            try
            {
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var valor = await _stockService.ObtenerPrecioMedioAsync(empresa, codigoArticulo, codigoAlmacen);
                _cachePreciosMedios[clave] = valor;
                return valor;
            }
            catch
            {
                return 0m; // si falla, 0€, la validación por € no bloqueará
            }
        }

        /// <summary>
        /// Calcula el valor total de todas las diferencias de inventario
        /// Cuenta tanto sobrestocks como faltantes (valor absoluto)
        /// </summary>
        private async Task<decimal> CalcularValorTotalDiferenciasAsync()
        {
            decimal valorTotal = 0;
            
            foreach (var linea in LineasProblematicas)
            {
                if (!linea.CantidadReconteo.HasValue) continue;
                
                var cantidadContada = linea.CantidadReconteo.Value;
                
                // 🔧 CONTROLAR tanto sobrestocks como faltantes (valor absoluto)
                var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                
                if (diferencia > 0.01m) // Tolerancia para evitar diferencias mínimas por redondeo
                {
                    var precioMedio = await ObtenerPrecioMedioAsync(linea.CodigoArticulo, linea.CodigoAlmacen ?? "");
                    valorTotal += diferencia * precioMedio;
                }
            }
            
            return valorTotal;
        }

        /// <summary>
        /// Calcula el total de unidades de diferencias de inventario
        /// Cuenta tanto sobrestocks como faltantes (valor absoluto)
        /// </summary>
        private decimal CalcularUnidadesTotalDiferencias()
        {
            decimal unidadesTotal = 0;
            
            foreach (var linea in LineasProblematicas)
            {
                if (!linea.CantidadReconteo.HasValue) continue;
                
                var cantidadContada = linea.CantidadReconteo.Value;
                
                // Controlar tanto sobrestocks como faltantes (valor absoluto)
                var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                
                if (diferencia > 0.01m) // Tolerancia para evitar diferencias mínimas por redondeo
                {
                    unidadesTotal += diferencia;
                }
            }
            
            return unidadesTotal;
        }

        /// <summary>
        /// Genera una clave única para identificar una línea problemática
        /// Usa la misma lógica que el API para diferenciar líneas por artículo/ubicación/partida/fecha/palet
        /// </summary>
        private string GenerarClaveUnicaLinea(LineaProblematicaDto linea)
        {
            return $"{linea.CodigoArticulo}|{linea.CodigoUbicacion}|{linea.Partida ?? ""}|{linea.FechaCaducidad?.ToString("yyyy-MM-dd") ?? ""}|{linea.PaletId?.ToString() ?? "NULL"}";
        }

        /// <summary>
        /// Calcula las diferencias del mismo artículo en la sesión actual de inventario
        /// Excluye la línea que se está validando para evitar contarla dos veces
        /// Usa clave única compuesta para diferenciar líneas del mismo artículo con diferentes PaletId
        /// </summary>
        private (decimal unidades, decimal euros) CalcularDiferenciasArticuloEnSesion(string codigoArticulo, LineaProblematicaDto lineaExcluir)
        {
            decimal totalUnidades = 0;
            decimal totalEuros = 0;
            
            // Generar clave única de la línea a excluir
            var claveExcluir = GenerarClaveUnicaLinea(lineaExcluir);
            
            // Buscar todas las líneas del mismo artículo en la sesión actual (excluyendo la que estamos validando)
            var lineasMismoArticulo = LineasProblematicas
                .Where(l => l.CodigoArticulo == codigoArticulo && GenerarClaveUnicaLinea(l) != claveExcluir)
                .ToList();
            
            foreach (var linea in lineasMismoArticulo)
            {
                if (!linea.CantidadReconteo.HasValue) continue;
                
                var cantidadContada = linea.CantidadReconteo.Value;
                var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                
                if (diferencia > 0.01m) // Tolerancia para diferencias mínimas
                {
                    totalUnidades += diferencia;
                    
                    // Para euros, usar precio medio en cache si existe
                    var clave = $"{codigoArticulo}|{linea.CodigoAlmacen ?? ""}";
                    if (_cachePreciosMedios.TryGetValue(clave, out var precio))
                    {
                        totalEuros += diferencia * precio;
                    }
                    // Si no hay precio en cache, no sumar euros (será cálculo conservador)
                }
            }
            
            return (totalUnidades, totalEuros);
        }

        /// <summary>
        /// Obtiene la cantidad contada de una línea
        /// </summary>
        private decimal ObtenerCantidadContada(LineaProblematicaDto linea)
        {
            if (linea.CantidadReconteo.HasValue)
                return linea.CantidadReconteo.Value;

            // Si no tiene valor pero tiene texto, intentar parsearlo
            if (!string.IsNullOrWhiteSpace(linea.CantidadReconteoTexto))
            {
                if (decimal.TryParse(linea.CantidadReconteoTexto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                    return result;
            }

            // Si no se puede obtener, usar el stock actual
            return linea.StockActual;
        }

        private async Task ValidarLimiteReconteoAsync(LineaProblematicaDto linea)
        {
            try
            {
                if (SessionManager.UsuarioActual?.operario == null) return;
                if (Inventario == null) return;
                if (!linea.CantidadReconteo.HasValue) return;

                bool limiteSuperadoEuros = false;
                bool limiteSuperadoUnidades = false;
                string tipoLimiteSuperado = "";

                var operarioId = SessionManager.UsuarioActual.operario;
                var codigoArticulo = linea.CodigoArticulo;
                var cantidadContada = linea.CantidadReconteo.Value;
                var nuevaDiferencia = Math.Abs(cantidadContada - linea.StockActual);

                // Obtener diferencias acumuladas del artículo en el día (excluyendo inventario actual)
                var (unidadesAcumuladas, eurosAcumulados) = await _loginService.ObtenerDiferenciasOperarioArticuloDiaAsync(operarioId, codigoArticulo, Inventario.IdInventario);

                // AÑADIR: diferencias del mismo artículo en la sesión actual (excluyendo la línea que estamos validando)
                var diferenciasEnSesion = CalcularDiferenciasArticuloEnSesion(codigoArticulo, linea);
                unidadesAcumuladas += diferenciasEnSesion.unidades;
                eurosAcumulados += diferenciasEnSesion.euros;

                // Validar límite de euros (acumulado del día + nueva diferencia)
                if (LimiteOperarioEuros > 0)
                {
                    var precioMedio = await ObtenerPrecioMedioAsync(codigoArticulo, linea.CodigoAlmacen ?? "");
                    var nuevaDiferenciaEuros = nuevaDiferencia * precioMedio;
                    var totalEurosArticulo = eurosAcumulados + nuevaDiferenciaEuros;

                    // También calcular total global para mostrar en UI
                    var valorTotalGlobal = await CalcularValorTotalDiferenciasAsync();
                    ValorDiferenciasActual = valorTotalGlobal;

                    if (totalEurosArticulo > LimiteOperarioEuros)
                    {
                        limiteSuperadoEuros = true;
                        tipoLimiteSuperado = $"valor en euros para el artículo {codigoArticulo}";
                    }
                }

                // Validar límite de unidades (acumulado del día + nueva diferencia)
                if (LimiteOperarioUnidades > 0)
                {
                    var totalUnidadesArticulo = unidadesAcumuladas + nuevaDiferencia;

                    // También calcular total global para mostrar en UI
                    var unidadesTotalGlobal = CalcularUnidadesTotalDiferencias();
                    UnidadesDiferenciasActual = unidadesTotalGlobal;

                    if (totalUnidadesArticulo > LimiteOperarioUnidades)
                    {
                        limiteSuperadoUnidades = true;
                        if (limiteSuperadoEuros)
                            tipoLimiteSuperado = $"valor en euros y unidades para el artículo {codigoArticulo}";
                        else
                            tipoLimiteSuperado = $"unidades para el artículo {codigoArticulo}";
                    }
                }

                // Si se supera algún límite
                if (limiteSuperadoEuros || limiteSuperadoUnidades)
                {
                    LimiteSuperado = true;
                    
                    // Resetear el valor que causó el problema
                    linea.CantidadReconteoTexto = Helpers.DecimalFormatHelper.FormatearCantidad(linea.StockActual);
                    
                    // Mostrar warning más específico
                    var warning = new WarningDialog(
                        "⚠️ Límite Diario Superado", 
                        $"Las diferencias diarias superan su límite autorizado de {tipoLimiteSuperado}.\n\n" +
                        $"Se ha restablecido la cantidad original para:\n" +
                        $"• {linea.CodigoArticulo} - {linea.DescripcionArticulo}\n" +
                        $"• Ubicación: {linea.CodigoUbicacion}\n\n" +
                        $"💡 El límite se aplica por artículo y por día en todos los almacenes.");
                    warning.ShowDialog();
                    
                    // Recalcular después del reset
                    if (LimiteOperarioEuros > 0)
                        ValorDiferenciasActual = await CalcularValorTotalDiferenciasAsync();
                    if (LimiteOperarioUnidades > 0)
                        UnidadesDiferenciasActual = CalcularUnidadesTotalDiferencias();
                }
                else
                {
                    LimiteSuperado = false;
                }
                
                OnPropertyChanged(nameof(EstadoLimite));
            }
            catch (Exception ex)
            {
                // Log error pero no interrumpir el flujo
                System.Diagnostics.Debug.WriteLine($"Error validando límite: {ex.Message}");
            }
        }

        private async void OnLineaPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineaProblematicaDto.CantidadReconteo))
            {
                if (sender is LineaProblematicaDto linea)
                {
                    await ValidarLimiteReconteoAsync(linea); // Nueva validación
                }
                ValidarFormulario();
            }
        }

        /// <summary>
        /// Valida que el operario no supere sus límites al guardar el reconteo
        /// </summary>
        private async Task<bool> ValidarLimitesOperarioAntesDeGuardarAsync()
        {
            try
            {
                if (SessionManager.UsuarioActual?.operario == null) return false;

                var operarioId = SessionManager.UsuarioActual.operario;
                
                // Obtener límites del operario
                var limiteEuros = await _loginService.ObtenerLimiteInventarioOperarioAsync(operarioId);
                var limiteUnidades = await _loginService.ObtenerLimiteUnidadesOperarioAsync(operarioId);
                
                if (limiteEuros <= 0 && limiteUnidades <= 0) return false; // Sin límites

                // Calcular diferencias totales del inventario actual
                decimal totalEurosDiferencias = 0;
                decimal totalUnidadesDiferencias = 0;
                
                foreach (var linea in LineasProblematicas)
                {
                    if (!linea.CantidadReconteo.HasValue) continue;
                    
                    var cantidadContada = linea.CantidadReconteo.Value;
                    var diferencia = Math.Abs(cantidadContada - linea.StockActual);
                    
                    if (diferencia > 0.01m) // Tolerancia para diferencias mínimas
                    {
                        totalUnidadesDiferencias += diferencia;
                        
                        if (limiteEuros > 0)
                        {
                            var precioMedio = await ObtenerPrecioMedioAsync(linea.CodigoArticulo, linea.CodigoAlmacen ?? "");
                            totalEurosDiferencias += diferencia * precioMedio;
                        }
                    }
                }
                
                // Verificar si se superan los límites
                bool limiteEurosSuperado = limiteEuros > 0 && totalEurosDiferencias > limiteEuros;
                bool limiteUnidadesSuperado = limiteUnidades > 0 && totalUnidadesDiferencias > limiteUnidades;
                
                if (limiteEurosSuperado || limiteUnidadesSuperado)
                {
                    string mensaje = "⚠️ Límite Diario Superado\n\n";
                    mensaje += "Las diferencias del inventario superan su límite autorizado:\n\n";
                    
                    if (limiteEurosSuperado)
                        mensaje += $"• Euros: {totalEurosDiferencias:C2} / {limiteEuros:C2}\n";
                    if (limiteUnidadesSuperado)
                        mensaje += $"• Unidades: {totalUnidadesDiferencias:F2} / {limiteUnidades:F2}\n";
                    
                    mensaje += "\nNo se puede guardar el inventario.";
                    
                    var warning = new WarningDialog("Límite Superado", mensaje);
                    var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                             ?? Application.Current.MainWindow;
                    if (owner != null && owner != warning)
                        warning.Owner = owner;
                    warning.ShowDialog();
                    
                    return true; // Límite superado
                }
                
                return false; // No se supera el límite
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validando límites antes de guardar: {ex.Message}");
                return false; // En caso de error, permitir guardar
            }
        }

        private void CerrarDialogo(bool resultado)
        {
            // Buscar la ventana actual
            foreach (var window in Application.Current.Windows)
            {
                if (window is Dialog.ReconteoLineasProblematicasDialog dialog)
                {
                    dialog.DialogResult = resultado;
                    dialog.Close();
                    break;
                }
            }
        }
        #endregion
    }

    // DTOs específicos para esta funcionalidad
    public partial class LineaProblematicaDto : ObservableObject
    {
        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        [ObservableProperty]
        private string descripcionArticulo = string.Empty;

        [ObservableProperty]
        private string codigoAlmacen = string.Empty;

        [ObservableProperty]
        private string codigoUbicacion = string.Empty;

        [ObservableProperty]
        private string partida = string.Empty;

        [ObservableProperty]
        private DateTime? fechaCaducidad;

        [ObservableProperty]
        private Guid? paletId;

        [ObservableProperty]
        private decimal stockAlCrearInventario;

        [ObservableProperty]
        private decimal stockActual;

        [ObservableProperty]
        private List<PaletDetalleDto> palets = new();

        [ObservableProperty]
        private decimal stockTotalActual;

        [ObservableProperty]
        private decimal stockPaletizadoActual;

        /// <summary>
        /// Stock suelto calculado (total - paletizado)
        /// </summary>
        public decimal StockSueltoActual => StockTotalActual - StockPaletizadoActual;

        partial void OnStockTotalActualChanged(decimal oldValue, decimal newValue)
        {
            OnPropertyChanged(nameof(StockSueltoActual));
        }

        partial void OnStockPaletizadoActualChanged(decimal oldValue, decimal newValue)
        {
            OnPropertyChanged(nameof(StockSueltoActual));
        }

        /// <summary>
        /// Indica si el stock está en al menos un palet
        /// </summary>
        public bool TienePalets => Palets?.Any() == true;

        /// <summary>
        /// Indica si el stock está distribuido en múltiples palets
        /// </summary>
        public bool TieneMultiplesPalets => Palets?.Count > 1;

        /// <summary>
        /// Texto resumido de los palets para mostrar en la UI
        /// Si la línea tiene PaletId, muestra solo ese palet específico
        /// Si no tiene PaletId, indica que es stock suelto
        /// </summary>
        public string PaletsResumen
        {
            get
            {
                // Si la línea tiene un PaletId específico, mostrar solo ese palet
                if (PaletId.HasValue)
                {
                    // Buscar el palet específico de esta línea
                    var paletEspecifico = Palets?.FirstOrDefault(p => p.PaletId == PaletId.Value);
                    if (paletEspecifico != null)
                    {
                        return $"{paletEspecifico.CodigoPalet} ({paletEspecifico.Cantidad:F2})";
                    }
                    // Si el palet no se encuentra en la lista, puede que haya sido eliminado
                    return "Palet no encontrado";
                }
                
                // Si no tiene PaletId, es stock suelto
                return "Stock suelto";
            }
        }

        partial void OnPaletsChanged(List<PaletDetalleDto> oldValue, List<PaletDetalleDto> newValue)
        {
            OnPropertyChanged(nameof(TienePalets));
            OnPropertyChanged(nameof(TieneMultiplesPalets));
            OnPropertyChanged(nameof(PaletsResumen));
        }

        private decimal? _cantidadReconteo;
        public decimal? CantidadReconteo
        {
            get => _cantidadReconteo;
            set
            {
                if (SetProperty(ref _cantidadReconteo, value))
                {
                    // Notificar cambio para que se ejecute la validación
                    OnPropertyChanged(nameof(CantidadReconteoTexto));
                }
            }
        }

        [ObservableProperty]
        private bool isSelected;

        private string? _cantidadReconteoTexto;

        public string CantidadReconteoTexto
        {
            get => _cantidadReconteoTexto ?? (CantidadReconteo.HasValue ? Helpers.DecimalFormatHelper.FormatearCantidad(CantidadReconteo.Value) : "0");
            set
            {
                _cantidadReconteoTexto = value;
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cantidad))
                {
                    CantidadReconteo = cantidad;
                }
                else
                {
                    CantidadReconteo = null;
                }
            }
        }
    }

    public class GuardarReconteoDto
    {
        public Guid IdInventario { get; set; }
        public List<LineaReconteoDto> LineasRecontadas { get; set; } = new();
    }

    public class LineaReconteoDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string CodigoUbicacion { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public Guid? PaletId { get; set; }
        public decimal CantidadReconteo { get; set; }
        public int UsuarioReconteo { get; set; }
    }
} 