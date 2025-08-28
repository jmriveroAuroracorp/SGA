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
        private List<Models.LineaProblematicaDto> _todasLasLineas = new();
        #endregion

        #region Constructor
        public ReconteoLineasProblematicasDialogViewModel(InventarioService inventarioService, StockService stockService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            
            LineasProblematicas = new ObservableCollection<LineaProblematicaDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ReconteoLineasProblematicasDialogViewModel() : this(new InventarioService(), new StockService()) { }
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
        #endregion

        #region Computed Properties
        public string TotalLineasProblematicas => $"Total: {LineasProblematicas.Count} líneas problemáticas";
        public string LineasRecontadas => $"Recontadas: {LineasProblematicas.Count(l => l.CantidadReconteo.HasValue)}";
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
            // Solo validar cuando IsCargando cambie a false
            if (!newValue)
            {
                ValidarFormulario();
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
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
                        StockAlCrearInventario = linea.StockAlCrearInventario,
                        StockActual = linea.StockActual,
                        // Inicializar con el stock actual para que el usuario vea los valores pre-llenados
                        CantidadReconteo = linea.StockActual
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
                        Partida = l.Partida,
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
        private void Cancelar()
        {
            CerrarDialogo(false);
        }
        #endregion

        #region Private Methods
        private void ValidarFormulario()
        {
            var tieneLineasRecontadas = LineasProblematicas.Any(l => l.CantidadReconteo.HasValue);
            var nuevoPuedeGuardar = Inventario != null && tieneLineasRecontadas;
            
            // Solo actualizar si hay cambio real
            if (PuedeGuardar != nuevoPuedeGuardar)
            {
                PuedeGuardar = nuevoPuedeGuardar;
            }
            
            // Notificar cambios en las propiedades computadas
            OnPropertyChanged(nameof(TotalLineasProblematicas));
            OnPropertyChanged(nameof(LineasRecontadas));
        }

        private void OnLineaPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineaProblematicaDto.CantidadReconteo))
            {
                // Solo validar si no está cargando para evitar validaciones innecesarias
                if (!IsCargando)
                {
                    ValidarFormulario();
                }
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
        private decimal stockAlCrearInventario;

        [ObservableProperty]
        private decimal stockActual;

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
            get => _cantidadReconteoTexto ?? CantidadReconteo?.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "0";
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
        public string Partida { get; set; } = string.Empty;
        public decimal CantidadReconteo { get; set; }
        public int UsuarioReconteo { get; set; }
    }
} 