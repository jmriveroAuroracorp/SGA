using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

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

        [ObservableProperty]
        private ObservableCollection<OperariosAccesoDto> operariosFiltrados = new();

        [ObservableProperty]
        private string filtroOperarios = "";

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

                // Inicializar la lista filtrada con todos los operarios
                AplicarFiltroOperarios();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                OperariosDisponibles.Clear();
                OperariosFiltrados.Clear();
            }
        }

        private void FiltrarOperarios(string filtro)
        {
            FiltroOperarios = filtro;
            AplicarFiltroOperarios();
        }

        private void AplicarFiltroOperarios()
        {
            if (string.IsNullOrWhiteSpace(FiltroOperarios))
            {
                // Si no hay filtro, mostrar todos los operarios
                OperariosFiltrados.Clear();
                foreach (var operario in OperariosDisponibles)
                {
                    OperariosFiltrados.Add(operario);
                }
            }
            else
            {
                // Filtrar operarios que contengan el texto (case insensitive)
                var filtroLower = FiltroOperarios.ToLower();
                OperariosFiltrados.Clear();
                
                foreach (var operario in OperariosDisponibles)
                {
                    if (operario.NombreOperario?.ToLower().Contains(filtroLower) == true)
                    {
                        OperariosFiltrados.Add(operario);
                    }
                }
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
                            IdOperarioAsignado = 0 // Se asignará manualmente por línea
                        };

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

                // Verificar stock disponible antes de crear la orden
                MensajeEstado = "Verificando stock disponible...";
                if (!await VerificarStockDisponible())
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

        private RelayCommand? _filtrarOperariosCommand;
        public RelayCommand FiltrarOperariosCommand
        {
            get
            {
                return _filtrarOperariosCommand ??= new RelayCommand<string>((filtro) =>
                {
                    FiltrarOperarios(filtro ?? "");
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

        private async Task<bool> VerificarStockDisponible()
        {
            try
            {
                var codigoEmpresa = SessionManager.EmpresaSeleccionada ?? 1;
                var almacenesAutorizados = ObtenerAlmacenesAutorizados();

                foreach (var linea in Lineas)
                {
                    // Debug: mostrar almacenes autorizados
                    System.Diagnostics.Debug.WriteLine($"=== DEBUG VerificarStock para {linea.CodigoArticulo} ===");
                    System.Diagnostics.Debug.WriteLine($"Almacenes autorizados: {string.Join(", ", almacenesAutorizados)}");

                    // Consultar stock actual del artículo
                    var stockActual = await _stockService.ObtenerPorArticuloAsync(
                        codigoEmpresa,
                        linea.CodigoArticulo,
                        null, // partida
                        null, // codigoAlmacen
                        null, // codigoUbicacion
                        null  // descripcion
                    );

                    System.Diagnostics.Debug.WriteLine($"Stock antes del filtro: {stockActual.Count} registros");
                    foreach (var s in stockActual.Take(3))
                    {
                        System.Diagnostics.Debug.WriteLine($"  - Almacén: {s.CodigoAlmacen}, Stock: {s.UnidadSaldo}");
                    }

                    // Filtrar por almacenes autorizados
                    if (almacenesAutorizados.Any())
                    {
                        stockActual = stockActual.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();
                    }

                    System.Diagnostics.Debug.WriteLine($"Stock después del filtro: {stockActual.Count} registros");
                    foreach (var s in stockActual)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - Almacén: {s.CodigoAlmacen}, Stock: {s.UnidadSaldo}");
                    }

                    // Calcular stock total disponible
                    var stockTotal = stockActual.Sum(s => s.UnidadSaldo);
                    System.Diagnostics.Debug.WriteLine($"Stock total: {stockTotal}");
                    System.Diagnostics.Debug.WriteLine($"===============================================");

                    if (stockTotal < linea.CantidadPlan)
                    {
                        var errorDialog = new WarningDialog(
                            "Stock Insuficiente",
                            $"El artículo '{linea.CodigoArticulo}' - '{linea.DescripcionArticulo}' no tiene suficiente stock.\n\n" +
                            $"Stock disponible: {stockTotal}\n" +
                            $"Cantidad solicitada: {linea.CantidadPlan}\n\n" +
                            "Por favor, ajuste las cantidades o elimine la línea.");
                        errorDialog.ShowDialog();
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error Verificando Stock",
                    $"Error al verificar el stock disponible: {ex.Message}");
                errorDialog.ShowDialog();
                return false;
            }
        }

        private List<string> ObtenerAlmacenesAutorizados()
        {
            var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
            var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";

            // Si no hay almacenes individuales, devolver lista vacía (sin restricciones)
            if (!almacenesIndividuales.Any())
            {
                return new List<string>(); // Sin restricciones de almacén
            }

            // Crear lista de almacenes autorizados
            var almacenesAutorizados = new List<string>();

            // Agregar almacenes individuales
            foreach (var almacen in almacenesIndividuales)
            {
                if (!string.IsNullOrWhiteSpace(almacen))
                {
                    almacenesAutorizados.Add(almacen.Trim());
                }
            }

            // Si hay centro logístico, agregar almacenes del centro
            if (!string.IsNullOrWhiteSpace(centro) && centro != "0")
            {
                // Agregar almacenes del centro logístico (formato: centro + sufijo)
                var sufijosCentro = new[] { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10" };
                foreach (var sufijo in sufijosCentro)
                {
                    var almacenCentro = $"{centro}{sufijo}";
                    if (!almacenesAutorizados.Contains(almacenCentro))
                    {
                        almacenesAutorizados.Add(almacenCentro);
                    }
                }
            }

            return almacenesAutorizados;
        }
        #endregion
    }
}
