using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
    public partial class CrearOrdenTraspasoDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly OrdenTraspasoService _ordenTraspasoService;
        private readonly LoginService _loginService;
        private readonly StockService _stockService;
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private bool isCargando;

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        [ObservableProperty]
        private string mensajeEstado = "";

        // Información básica
        [ObservableProperty]
        private ObservableCollection<PrioridadItem> prioridadesDisponibles = new();

        [ObservableProperty]
        private PrioridadItem? prioridadSeleccionada;

        [ObservableProperty]
        private DateTime? fechaPlan;

        [ObservableProperty]
        private string tipoOrigen = "SGA";

        [ObservableProperty]
        private ObservableCollection<AlmacenDto> almacenesDisponibles = new();

        [ObservableProperty]
        private AlmacenDto? almacenDestinoSeleccionado;

        [ObservableProperty]
        private ObservableCollection<OperariosAccesoDto> operariosDisponibles = new();

        // Nota: Cada línea tendrá su propia vista de operarios para evitar conflictos
        // El filtrado se manejará a nivel de línea individual

        [RelayCommand]
        private void AbrirDropDown()
        {
            // Este comando se puede usar para abrir el dropdown programáticamente si es necesario
            // El ComboBox se abrirá automáticamente con StaysOpenOnEdit="True"
        }



        [ObservableProperty]
        private string comentarios = "";

        // Propiedad para controlar si el combo de almacén destino está bloqueado
        public bool AlmacenDestinoBloqueado => Lineas.Any();
        
        // Propiedad inversa para habilitar el combo
        public bool AlmacenDestinoHabilitado => !Lineas.Any();

        // Líneas de la orden
        [ObservableProperty]
        private ObservableCollection<CrearLineaOrdenTraspasoDto> lineas = new();

        [ObservableProperty]
        private CrearLineaOrdenTraspasoDto? lineaSeleccionada;

        partial void OnLineaSeleccionadaChanged(CrearLineaOrdenTraspasoDto? value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
            OnPropertyChanged(nameof(PuedeEliminarLinea));
        }

        // Propiedad para habilitar/deshabilitar el botón de eliminar línea
        public bool PuedeEliminarLinea => LineaSeleccionada != null;

        // Método para notificar cambios en las líneas (llamado desde el XAML)
        public void NotificarCambioEnLineas()
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
            OnPropertyChanged(nameof(AlmacenDestinoBloqueado));
            OnPropertyChanged(nameof(AlmacenDestinoHabilitado));
        }

        // Comando manual para evitar problemas con el código generado
        private RelayCommand? _notificarCambioEnLineasCommand;
        public RelayCommand NotificarCambioEnLineasCommand
        {
            get
            {
                return _notificarCambioEnLineasCommand ??= new RelayCommand(() =>
                {
                    // Debug para verificar el estado de las líneas
                    System.Diagnostics.Debug.WriteLine($"=== DEBUG NotificarCambioEnLineas ===");
                    System.Diagnostics.Debug.WriteLine($"Total líneas: {Lineas.Count}");
                    foreach (var linea in Lineas)
                    {
                        System.Diagnostics.Debug.WriteLine($"Línea: {linea.CodigoArticulo} - Usuario: {linea.IdOperarioAsignado}");
                    }
                    System.Diagnostics.Debug.WriteLine($"PuedeCrearOrden: {PuedeCrearOrden}");
                    System.Diagnostics.Debug.WriteLine($"=====================================");
                    
                    NotificarCambioEnLineas();
                });
            }
        }

        // Referencia al diálogo para cerrarlo
        public Window? DialogResult { get; set; }

        // Propiedad para habilitar/deshabilitar el botón de crear orden
        public bool PuedeCrearOrden 
        { 
            get 
            {
                var resultado = !IsCargando && Lineas.Any() && 
                    Lineas.All(l => !string.IsNullOrWhiteSpace(l.CodigoArticulo) && 
                                   l.CantidadPlan > 0 && 
                                   !string.IsNullOrWhiteSpace(l.CodigoAlmacenDestino) &&
                                   l.IdOperarioAsignado > 0);
                
                System.Diagnostics.Debug.WriteLine($"=== DEBUG PuedeCrearOrden ===");
                System.Diagnostics.Debug.WriteLine($"IsCargando: {IsCargando}");
                System.Diagnostics.Debug.WriteLine($"Lineas.Any(): {Lineas.Any()}");
                System.Diagnostics.Debug.WriteLine($"Lineas.Count: {Lineas.Count}");
                foreach (var linea in Lineas)
                {
                    System.Diagnostics.Debug.WriteLine($"Línea: {linea.CodigoArticulo} - Usuario: {linea.IdOperarioAsignado} - Cantidad: {linea.CantidadPlan} - Destino: {linea.CodigoAlmacenDestino}");
                }
                System.Diagnostics.Debug.WriteLine($"Resultado final: {resultado}");
                System.Diagnostics.Debug.WriteLine($"=============================");
                
                return resultado;
            }
        }
        #endregion


        #region Constructor
        public CrearOrdenTraspasoDialogViewModel()
        {
            _ordenTraspasoService = new OrdenTraspasoService();
            _loginService = new LoginService();
            _stockService = new StockService();
            
            CargarDatosIniciales();
            
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }
        #endregion

        #region Private Methods
        private void CargarDatosIniciales()
        {
            // Cargar prioridades disponibles
            PrioridadesDisponibles = new ObservableCollection<PrioridadItem>
            {
                new() { Valor = 1, Texto = "1 - Muy Baja" },
                new() { Valor = 2, Texto = "2 - Baja" },
                new() { Valor = 3, Texto = "3 - Normal" },
                new() { Valor = 4, Texto = "4 - Alta" },
                new() { Valor = 5, Texto = "5 - Muy Alta" }
            };

            // Valores por defecto
            PrioridadSeleccionada = PrioridadesDisponibles.FirstOrDefault(p => p.Valor == 3);
            FechaPlan = DateTime.Today.AddDays(1);
        }

        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenes();
                await CargarOperarios();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
            }
        }

        private async Task CargarAlmacenes()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

                AlmacenesDisponibles.Clear();

                foreach (var a in resultado)
                    AlmacenesDisponibles.Add(a);

                AlmacenDestinoSeleccionado = AlmacenesDisponibles.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
                // En caso de error, agregar almacén por defecto
                AlmacenesDisponibles.Clear();
                var almacenDefecto = new AlmacenDto
                {
                    CodigoAlmacen = "01",
                    NombreAlmacen = "Almacén Principal",
                    CodigoEmpresa = (short)(SessionManager.EmpresaSeleccionada ?? 1)
                };
                AlmacenesDisponibles.Add(almacenDefecto);
                AlmacenDestinoSeleccionado = almacenDefecto;
            }
        }

        private async Task CargarOperarios()
        {
            try
            {
                // Usar el endpoint específico de conteos (mismo que otros ViewModels)
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosDisponibles.Clear();

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }

                // Inicializar filtrado para líneas existentes
                foreach (var linea in Lineas)
                {
                    InicializarFiltradoLinea(linea);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                OperariosDisponibles.Clear();
            }
        }


        [RelayCommand]
        private void EliminarLinea()
        {
            if (LineaSeleccionada != null)
            {
                Lineas.Remove(LineaSeleccionada);
                
                // Renumerar líneas
                for (int i = 0; i < Lineas.Count; i++)
                {
                    Lineas[i].Orden = i + 1;
                }

                LineaSeleccionada = Lineas.FirstOrDefault();
                NotificarCambioEnLineas();
            }
        }

        [RelayCommand]
        private async Task AgregarMultiplesLineas()
        {
            try
            {
                // Obtener la información del destino desde el encabezado
                var codigoDestino = AlmacenDestinoSeleccionado?.CodigoAlmacen ?? "";
                var nombreDestino = AlmacenDestinoSeleccionado?.NombreAlmacen ?? "";
                
                var dialog = new AgregarLineasOrdenTraspasoDialog(codigoDestino, nombreDestino);
                
                // Establecer el owner para que se centre correctamente
                var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                           ?? System.Windows.Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                
                if (result == true && dialog.DataContext is AgregarLineasOrdenTraspasoDialogViewModel viewModel)
                {
                    // Agregar las líneas seleccionadas
                    foreach (var lineaItem in viewModel.LineasPendientes)
                    {
                        var nuevaLinea = new CrearLineaOrdenTraspasoDto
                        {
                            Orden = Lineas.Count + 1,
                            CodigoArticulo = lineaItem.CodigoArticulo,
                            DescripcionArticulo = lineaItem.DescripcionArticulo,
                            CantidadPlan = lineaItem.CantidadPlan,
                            CodigoAlmacenOrigen = lineaItem.CodigoAlmacenOrigen,
                            CodigoAlmacenDestino = lineaItem.CodigoAlmacenDestino,
                            UbicacionOrigen = lineaItem.UbicacionOrigen,
                            UbicacionDestino = lineaItem.UbicacionDestino,
                            Partida = lineaItem.Partida,
                            FechaCaducidad = lineaItem.FechaCaducidad,
                            PaletOrigen = "",
                            PaletDestino = "",
                            IdOperarioAsignado = 0, // Se asignará manualmente por línea
                            OperarioSeleccionado = null // Inicializar sin operario seleccionado
                        };

                        // Inicializar el filtrado individual para esta línea
                        InicializarFiltradoLinea(nuevaLinea);

                        Lineas.Add(nuevaLinea);
                    }

                    // Seleccionar la última línea agregada
                    LineaSeleccionada = Lineas.LastOrDefault();
                    NotificarCambioEnLineas();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al agregar múltiples líneas: {ex.Message}");
                var errorDialog = new WarningDialog("Error", $"Error al agregar líneas: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task CrearOrden()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Validando datos...";

                // Validaciones
                if (!ValidarDatos())
                {
                    IsCargando = false;
                    return;
                }

                MensajeEstado = "Creando orden de traspaso...";

                // Debug: verificar IdOperarioAsignado de las líneas
                System.Diagnostics.Debug.WriteLine("=== DEBUG CrearOrden - Líneas ===");
                foreach (var linea in Lineas)
                {
                    System.Diagnostics.Debug.WriteLine($"Línea: {linea.CodigoArticulo} - IdOperarioAsignado: {linea.IdOperarioAsignado}");
                }
                System.Diagnostics.Debug.WriteLine("=================================");

                var dto = new CrearOrdenTraspasoDto
                {
                    CodigoEmpresa = (short)(SessionManager.EmpresaSeleccionada ?? 1),
                    Prioridad = (short)(PrioridadSeleccionada?.Valor ?? 3),
                    FechaPlan = FechaPlan,
                    TipoOrigen = TipoOrigen,
                    UsuarioCreacion = SessionManager.UsuarioActual?.operario ?? 1,
                    Comentarios = Comentarios,
                    CodigoAlmacenDestino = AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    Lineas = Lineas.ToList()
                };

                var ordenCreada = await _ordenTraspasoService.CrearOrdenTraspasoAsync(dto);

                // Mostrar mensaje de éxito
                var successDialog = new WarningDialog(
                    "Orden Creada", 
                    $"La orden '{ordenCreada.CodigoOrden}' ha sido creada exitosamente.");
                successDialog.ShowDialog();

                // Cerrar el diálogo con resultado exitoso
                if (DialogResult != null)
                {
                    DialogResult.DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog(
                    "Error al crear orden", 
                    $"No se pudo crear la orden de traspaso: {ex.Message}");
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                MensajeEstado = string.Empty;
            }
        }

        // Comandos manuales para evitar problemas con el código generado
        private RelayCommand? _cancelarCommand;
        public RelayCommand CancelarCommand
        {
            get
            {
                return _cancelarCommand ??= new RelayCommand(() =>
                {
                    System.Diagnostics.Debug.WriteLine("CancelarCommand ejecutado");
                    if (DialogResult != null)
                    {
                        DialogResult.DialogResult = false;
                    }
                });
            }
        }

        private RelayCommand? _cerrarCommand;
        public RelayCommand CerrarCommand
        {
            get
            {
                return _cerrarCommand ??= new RelayCommand(() =>
                {
                    System.Diagnostics.Debug.WriteLine("CerrarCommand ejecutado");
                    if (DialogResult != null)
                    {
                        DialogResult.DialogResult = false;
                    }
                });
            }
        }


        private bool ValidarDatos()
        {
            if (Lineas.Count == 0)
            {
                MessageBox.Show("Debe agregar al menos una línea a la orden.", "Validación", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            foreach (var linea in Lineas)
            {
                if (string.IsNullOrWhiteSpace(linea.CodigoArticulo))
                {
                    MessageBox.Show("Todas las líneas deben tener un código de artículo.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (linea.CantidadPlan <= 0)
                {
                    MessageBox.Show("Todas las líneas deben tener una cantidad mayor a 0.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(linea.CodigoAlmacenDestino))
                {
                    MessageBox.Show("Todas las líneas deben tener almacén de destino.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (linea.IdOperarioAsignado <= 0)
                {
                    MessageBox.Show("Todas las líneas deben tener un usuario asignado.", "Validación", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private void InicializarFiltradoLinea(CrearLineaOrdenTraspasoDto linea)
        {
            // Crear una copia de los operarios para esta línea
            linea.OperariosDisponiblesLinea = new ObservableCollection<OperariosAccesoDto>(OperariosDisponibles);
            
            // Crear vista filtrada para esta línea específica
            linea.OperariosViewLinea = CollectionViewSource.GetDefaultView(linea.OperariosDisponiblesLinea);
            linea.OperariosViewLinea.Filter = obj => FiltraOperarioLinea(obj, linea);
        }

        private bool FiltraOperarioLinea(object obj, CrearLineaOrdenTraspasoDto linea)
        {
            if (string.IsNullOrWhiteSpace(linea.FiltroOperarioLinea)) return true;
            if (obj is not OperariosAccesoDto operario) return false;

            // Búsqueda acento-insensible, sin mayúsc/minúsc, en cualquier parte del texto
            var compare = CultureInfo.CurrentCulture.CompareInfo;
            var options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

            bool contiene(string s) =>
                !string.IsNullOrEmpty(s) &&
                compare.IndexOf(s, linea.FiltroOperarioLinea, options) >= 0;

            return contiene(operario.NombreOperario) || contiene(operario.NombreCompleto);
        }

        [RelayCommand]
        private void LimpiarFiltroLinea(CrearLineaOrdenTraspasoDto linea)
        {
            if (linea != null)
            {
                linea.FiltroOperarioLinea = ""; // Limpiar el filtro para permitir escribir desde cero
                linea.IsDropDownOpenLinea = true;
                linea.OperariosViewLinea?.Refresh();
            }
        }

        [RelayCommand]
        private void ActualizarFiltroLinea(CrearLineaOrdenTraspasoDto linea)
        {
            if (linea?.OperariosViewLinea != null)
            {
                linea.OperariosViewLinea.Refresh();
            }
        }

        #endregion
    }
}
