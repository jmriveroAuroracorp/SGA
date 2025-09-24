using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SGA_Desktop.Dialog;

namespace SGA_Desktop.ViewModels
{
    public partial class VerOrdenTraspasoDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly OrdenTraspasoService _ordenTraspasoService;
        private readonly LoginService _loginService;
        #endregion

        #region Constructor
        public VerOrdenTraspasoDialogViewModel(OrdenTraspasoService ordenTraspasoService, LoginService loginService)
        {
            _ordenTraspasoService = ordenTraspasoService;
            _loginService = loginService;
            
            LineasOrden = new ObservableCollection<LineaOrdenTraspasoDetalleDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public VerOrdenTraspasoDialogViewModel() : this(new OrdenTraspasoService(), new LoginService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private OrdenTraspasoDto? ordenTraspaso;

        public ObservableCollection<LineaOrdenTraspasoDetalleDto> LineasOrden { get; }

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;
        #endregion

        #region Computed Properties
        public string TotalLineas => $"Total: {LineasOrden.Count} líneas";
        #endregion

        #region Property Change Callbacks
        partial void OnOrdenTraspasoChanged(OrdenTraspasoDto? oldValue, OrdenTraspasoDto? newValue)
        {
            if (newValue != null)
            {
                _ = CargarLineasAsync();
            }
        }

        partial void OnIsCargandoChanged(bool oldValue, bool newValue)
        {
            OnPropertyChanged(nameof(TotalLineas));
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
                MensajeEstado = $"Error al inicializar: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task CargarLineasAsync()
        {
            try
            {
                if (OrdenTraspaso == null) return;

                IsCargando = true;
                MensajeEstado = "Cargando líneas de la orden...";

                // Obtener lista de operarios una sola vez (usar el mismo método que ControlesRotativos)
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                var operariosDict = operarios.ToDictionary(o => o.Operario.ToString(), o => ExtraerSoloNombre(o.NombreCompleto ?? "Sin nombre"));

                // Obtener nombre del usuario creador si no lo tenemos
                if (string.IsNullOrEmpty(OrdenTraspaso.NombreUsuarioCreacion) && OrdenTraspaso.UsuarioCreacion > 0)
                {
                    var nombreEncontrado = operariosDict.GetValueOrDefault(OrdenTraspaso.UsuarioCreacion.ToString(), null);
                    
                    // Si no se encontró, usar fallback
                    if (string.IsNullOrEmpty(nombreEncontrado))
                    {
                        nombreEncontrado = $"Usuario ID: {OrdenTraspaso.UsuarioCreacion}";
                    }
                    
                    OrdenTraspaso.NombreUsuarioCreacion = nombreEncontrado;
                    
                    // Debug: Log para depuración
                    System.Diagnostics.Debug.WriteLine($"[VerOrdenTraspasoDialog] Usuario creador ID: {OrdenTraspaso.UsuarioCreacion}");
                    System.Diagnostics.Debug.WriteLine($"[VerOrdenTraspasoDialog] Total operarios cargados: {operarios.Count}");
                    System.Diagnostics.Debug.WriteLine($"[VerOrdenTraspasoDialog] Nombre resuelto: '{nombreEncontrado}'");
                    
                    // Notificar cambio en la orden para actualizar la UI
                    OnPropertyChanged(nameof(OrdenTraspaso));
                }

                // Agrupar líneas por artículo y ordenar por Orden dentro de cada grupo
                var lineasAgrupadas = OrdenTraspaso.Lineas
                    .GroupBy(l => l.CodigoArticulo)
                    .SelectMany(grupo => 
                    {
                        // Dentro de cada grupo, ordenar por Orden (el primero será el padre)
                        return grupo.OrderBy(l => l.Orden);
                    })
                    .ToList();

                // Cargar líneas con información de operarios y marcar cuáles son padre
                LineasOrden.Clear();
                string articuloAnterior = "";
                
                foreach (var linea in lineasAgrupadas)
                {
                    // Obtener nombre del operario si está asignado
                    if (linea.IdOperarioAsignado > 0)
                    {
                        linea.NombreOperario = operariosDict.GetValueOrDefault(linea.IdOperarioAsignado.ToString(), $"ID: {linea.IdOperarioAsignado}");
                    }
                    else
                    {
                        linea.NombreOperario = "Sin asignar";
                    }

                    // Marcar si es el primer elemento del artículo (línea padre)
                    linea.EsPadre = linea.CodigoArticulo != articuloAnterior;
                    articuloAnterior = linea.CodigoArticulo;

                    LineasOrden.Add(linea);
                }

                MensajeEstado = $"Cargadas {LineasOrden.Count} líneas";
                OnPropertyChanged(nameof(TotalLineas));
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al cargar líneas: {ex.Message}");
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task ExportarAExcelAsync()
        {
            try
            {
                // TODO: Implementar exportación a Excel si se necesita
                var infoDialog = new WarningDialog("Información", "Funcionalidad de exportación pendiente de implementar.");
                infoDialog.ShowDialog();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al exportar: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void Cerrar()
        {
            CerrarDialogo();
        }
        #endregion

        #region Private Methods
        private void CerrarDialogo()
        {
            if (Application.Current.Windows.OfType<VerOrdenTraspasoDialog>().FirstOrDefault() is VerOrdenTraspasoDialog dialog)
            {
                dialog.Close();
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Establece la orden de traspaso a mostrar
        /// </summary>
        public void EstablecerOrden(OrdenTraspasoDto orden)
        {
            OrdenTraspaso = orden;
        }
        
        /// <summary>
        /// Extrae solo el nombre del operario, quitando el código al inicio
        /// Ejemplo: "1226 - RIVERO CAMPOS, JOSE MANUEL" -> "RIVERO CAMPOS, JOSE MANUEL"
        /// </summary>
        private static string ExtraerSoloNombre(string nombreCompleto)
        {
            if (string.IsNullOrEmpty(nombreCompleto))
                return "Sin nombre";
                
            // Buscar el patrón "CÓDIGO - NOMBRE" y extraer solo el nombre
            var indiceGuion = nombreCompleto.IndexOf(" - ");
            if (indiceGuion > 0)
            {
                return nombreCompleto.Substring(indiceGuion + 3).Trim();
            }
            
            // Si no tiene el formato esperado, devolver tal como está
            return nombreCompleto.Trim();
        }
        #endregion
    }

}
