using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;

namespace SGA_Desktop.ViewModels
{
    public partial class EditarOrdenConteoDialogViewModel : ObservableObject
    {
        #region Servicios
        private readonly ConteosService _conteosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;
        private readonly InventarioService _inventarioService;
        private readonly UbicacionesService _ubicacionesService;
        private Guid _ordenGuid;
        #endregion

        #region Propiedades de la orden a editar
        [ObservableProperty]
        private Guid ordenGuid;

        [ObservableProperty]
        private string titulo = string.Empty;

        [ObservableProperty]
        private bool esConteoUbicacion = true;

        [ObservableProperty]
        private PrioridadItem? prioridadSeleccionada;

        [ObservableProperty]
        private VisibilidadItem? visibilidadSeleccionada;

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionado;

        [ObservableProperty]
        private OperariosAccesoDto? operarioSeleccionado;

        [ObservableProperty]
        private DateTime? fechaPlan;

        [ObservableProperty]
        private string comentario = string.Empty;
        #endregion

        #region Propiedades de filtros de ubicación
        [ObservableProperty]
        private object? pasillo;

        [ObservableProperty]
        private object? estanteria;

        [ObservableProperty]
        private object? altura;

        [ObservableProperty]
        private object? posicion;

        [ObservableProperty]
        private string ubicacionDirecta = "SIN UBICAR";

        [ObservableProperty]
        private bool usarUbicacionDirecta = false;

        // Propiedad calculada para mostrar/ocultar filtros secuenciales
        public bool MostrarFiltrosSecuenciales => !UsarUbicacionDirecta;
        #endregion

        #region Propiedades de filtros de artículo
        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        [ObservableProperty]
        private string articuloBuscado = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ArticuloResumenDto> articulosEncontrados = new();

        [ObservableProperty]
        private ArticuloResumenDto? articuloSeleccionado;
        #endregion

        #region Propiedades de estado
        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private bool puedeActualizarOrden = false;

        // Referencia al diálogo para cerrarlo
        public Window? DialogResult { get; set; }
        #endregion

        #region Colecciones disponibles
        [ObservableProperty]
        private ObservableCollection<PrioridadItem> prioridadesDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<VisibilidadItem> visibilidadesDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<AlmacenDto> almacenesDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<OperariosAccesoDto> operariosDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> pasillosDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> estanteriasDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> alturasDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> posicionesDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<string> ubicacionesDisponibles = new();
        #endregion

        #region Propiedades calculadas
        public bool MostrarConteoUbicacion => EsConteoUbicacion;
        public bool MostrarConteoArticulo => !EsConteoUbicacion;
        public bool MostrarListaArticulos => ArticulosEncontrados.Count > 1;
        public bool MostrarInfoArticulo => ArticuloSeleccionado != null;
        #endregion

        #region Constructor
        public EditarOrdenConteoDialogViewModel(
            ConteosService conteosService,
            StockService stockService,
            LoginService loginService,
            InventarioService inventarioService,
            UbicacionesService ubicacionesService)
        {
            _conteosService = conteosService;
            _stockService = stockService;
            _loginService = loginService;
            _inventarioService = inventarioService;
            _ubicacionesService = ubicacionesService;

            // Inicializar colecciones
            InicializarPrioridades();
            InicializarVisibilidades();
            _ = CargarDatosInicialesAsync();
        }

        public EditarOrdenConteoDialogViewModel() : this(
            new ConteosService(),
            new StockService(),
            new LoginService(),
            new InventarioService(),
            new UbicacionesService())
        {
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task ActualizarOrden()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Actualizando orden...";

                var dto = new CrearOrdenConteoDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada ?? 1,
                    Titulo = Titulo.Trim(),
                    Visibilidad = VisibilidadSeleccionada?.Valor ?? "VISIBLE",
                    Estado = "ASIGNADO",
                    ModoGeneracion = "AUTOMATICO",
                    Alcance = EsConteoUbicacion ? "ALMACEN" : "ARTICULO",
                    FiltrosJson = GenerarFiltrosJson(),
                    FechaPlan = FechaPlan,
                    CreadoPorCodigo = SessionManager.UsuarioActual?.operario.ToString() ?? "ADMIN",
                    Prioridad = (byte)(PrioridadSeleccionada?.Valor ?? 3),
                    CodigoOperario = OperarioSeleccionado?.Operario == 0 ? null : OperarioSeleccionado?.Operario.ToString(),
                    CodigoAlmacen = EsConteoUbicacion ? AlmacenSeleccionado?.CodigoAlmacen : null,
                    Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim()
                };

                // Debug: Log del DTO que se está enviando
                Debug.WriteLine($"DTO para actualizar orden:");
                Debug.WriteLine($"  - CodigoEmpresa: {dto.CodigoEmpresa}");
                Debug.WriteLine($"  - Titulo: {dto.Titulo}");
                Debug.WriteLine($"  - Visibilidad: {dto.Visibilidad}");
                Debug.WriteLine($"  - Estado: {dto.Estado}");
                Debug.WriteLine($"  - ModoGeneracion: {dto.ModoGeneracion}");
                Debug.WriteLine($"  - Alcance: {dto.Alcance}");
                Debug.WriteLine($"  - FiltrosJson: {dto.FiltrosJson}");
                Debug.WriteLine($"  - FechaPlan: {dto.FechaPlan}");
                Debug.WriteLine($"  - CreadoPorCodigo: {dto.CreadoPorCodigo}");
                Debug.WriteLine($"  - Prioridad: {dto.Prioridad}");
                Debug.WriteLine($"  - CodigoOperario: {dto.CodigoOperario}");
                Debug.WriteLine($"  - CodigoAlmacen: {dto.CodigoAlmacen}");
                Debug.WriteLine($"  - Comentario: {dto.Comentario}");
                Debug.WriteLine($"  - CodigoArticulo: {dto.CodigoArticulo}");

                // Si el alcance es ARTICULO, agregar el código del artículo
                if (!EsConteoUbicacion && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    dto.CodigoArticulo = CodigoArticulo.Trim();
                }

                // SEGUNDO CHECK: Verificar que la orden aún se puede editar antes de actualizar
                var ordenActual = await _conteosService.ObtenerOrdenAsync(_ordenGuid);
                if (ordenActual == null)
                {
                    var errorDialog = new WarningDialog(
                        "Error", 
                        "No se pudo obtener la información actual de la orden.");
                    errorDialog.ShowDialog();
                    return;
                }

                if (ordenActual.Estado != "PLANIFICADO" && ordenActual.Estado != "ASIGNADO")
                {
                    var errorDialog = new WarningDialog(
                        "No se puede actualizar", 
                        $"La orden '{ordenActual.Titulo}' ha cambiado de estado a '{ordenActual.EstadoFormateado}' y ya no se puede editar.\n\nSolo se pueden editar órdenes en estado 'Asignado'.");
                    errorDialog.ShowDialog();
                    
                    // Cerrar el diálogo de edición ya que no se puede actualizar
                    var editWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                    editWindow?.Close();
                    return;
                }

                // Actualizar la orden
                var ordenActualizada = await _conteosService.ActualizarOrdenAsync(_ordenGuid, dto);

                // Mostrar mensaje de éxito
                var successDialog = new WarningDialog(
                    "Orden Actualizada", 
                    $"La orden '{ordenActualizada.Titulo}' ha sido actualizada exitosamente.");
                successDialog.ShowDialog();

                // Cerrar el diálogo - buscar la ventana padre y cerrarla
                var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                window?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al actualizar orden: {ex.Message}");
                var errorDialog = new WarningDialog(
                    "Error al actualizar orden", 
                    $"No se pudo actualizar la orden de conteo: {ex.Message}");
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                MensajeEstado = string.Empty;
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            // Buscar la ventana padre y cerrarla
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            window?.Close();
        }
        #endregion

        #region Métodos de inicialización
        private void InicializarPrioridades()
        {
            PrioridadesDisponibles.Clear();
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 1, Texto = "1 - Muy Baja" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 2, Texto = "2 - Baja" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 3, Texto = "3 - Normal" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 4, Texto = "4 - Alta" });
            PrioridadesDisponibles.Add(new PrioridadItem { Valor = 5, Texto = "5 - Muy Alta" });
        }

        private void InicializarVisibilidades()
        {
            VisibilidadesDisponibles.Clear();
            VisibilidadesDisponibles.Add(new VisibilidadItem { Valor = "VISIBLE", Texto = "Conteo Visible", Descripcion = "El operario puede ver las cantidades en stock" });
            VisibilidadesDisponibles.Add(new VisibilidadItem { Valor = "CIEGO", Texto = "Conteo Ciego", Descripcion = "El operario NO puede ver las cantidades en stock" });
        }

        private async Task CargarDatosInicialesAsync()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando datos...";

                // Cargar almacenes
                await CargarAlmacenesAsync();

                // Cargar operarios
                await CargarOperariosAsync();

                MensajeEstado = "Datos cargados correctamente";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error al cargar datos: {ex.Message}";
            }
            finally
            {
                IsCargando = false;
            }
        }

        private async Task CargarAlmacenesAsync()
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
            }
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                Debug.WriteLine($"Operarios obtenidos del API: {operarios.Count()}");
                foreach (var op in operarios)
                {
                    Debug.WriteLine($"  - ID: {op.Operario}, Nombre: {op.NombreOperario}");
                }

                OperariosDisponibles.Clear();

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // En caso de error, dejar la lista vacía
                OperariosDisponibles.Clear();
            }
        }
        #endregion

        #region Métodos públicos para cargar datos de la orden
        public async Task CargarOrdenAsync(Guid ordenGuid)
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando orden...";

                _ordenGuid = ordenGuid;
                var orden = await _conteosService.ObtenerOrdenAsync(ordenGuid);

                if (orden == null)
                {
                    MensajeEstado = "Orden no encontrada";
                    return;
                }

                // Cargar datos básicos
                Titulo = orden.Titulo ?? string.Empty;
                EsConteoUbicacion = orden.Alcance != "ARTICULO";
                FechaPlan = orden.FechaPlan;
                Comentario = orden.Comentario ?? string.Empty;

                // Cargar prioridad
                PrioridadSeleccionada = PrioridadesDisponibles.FirstOrDefault(p => p.Valor == orden.Prioridad);

                // IMPORTANTE: Cargar las listas PRIMERO antes de seleccionar valores
                await CargarAlmacenesAsync();
                await CargarOperariosAsync();

                // AHORA SÍ cargar almacén (después de que se hayan cargado los almacenes)
                if (!string.IsNullOrEmpty(orden.CodigoAlmacen))
                {
                    Debug.WriteLine($"Buscando almacén con código: '{orden.CodigoAlmacen}'");
                    Debug.WriteLine($"Almacenes disponibles: {AlmacenesDisponibles.Count}");
                    foreach (var almacen in AlmacenesDisponibles)
                    {
                        Debug.WriteLine($"  - {almacen.CodigoAlmacen}: {almacen.NombreAlmacen}");
                    }
                    
                    // Intentar diferentes formas de comparación
                    AlmacenSeleccionado = AlmacenesDisponibles.FirstOrDefault(a => 
                        a.CodigoAlmacen == orden.CodigoAlmacen ||
                        a.CodigoAlmacen == orden.CodigoAlmacen?.Trim());
                    Debug.WriteLine($"Almacén seleccionado: {(AlmacenSeleccionado != null ? $"{AlmacenSeleccionado.CodigoAlmacen} - {AlmacenSeleccionado.NombreAlmacen}" : "NO ENCONTRADO")}");
                }

                // AHORA SÍ cargar operario (después de que se hayan cargado los operarios)
                if (!string.IsNullOrEmpty(orden.CodigoOperario))
                {
                    Debug.WriteLine($"Buscando operario con código: '{orden.CodigoOperario}'");
                    Debug.WriteLine($"Operarios disponibles: {OperariosDisponibles.Count}");
                    foreach (var operario in OperariosDisponibles)
                    {
                        Debug.WriteLine($"  - {operario.Operario}: {operario.NombreOperario}");
                    }
                    
                    // Intentar diferentes formas de comparación
                    OperarioSeleccionado = OperariosDisponibles.FirstOrDefault(o => 
                        o.Operario.ToString() == orden.CodigoOperario ||
                        o.Operario.ToString() == orden.CodigoOperario?.Trim() ||
                        o.Operario == int.Parse(orden.CodigoOperario ?? "0"));
                    Debug.WriteLine($"Operario seleccionado: {(OperarioSeleccionado != null ? $"{OperarioSeleccionado.Operario} - {OperarioSeleccionado.NombreOperario}" : "NO ENCONTRADO")}");
                }

                // Cargar filtros
                await CargarFiltrosDeOrdenAsync(orden);

                // Cargar rangos si es conteo por ubicación
                if (EsConteoUbicacion && AlmacenSeleccionado != null)
                {
                    await CargarRangosDisponiblesAsync();
                    await CargarUbicacionesDisponiblesAsync();
                }

                // Cargar artículo si es conteo por artículo
                if (!EsConteoUbicacion && !string.IsNullOrEmpty(orden.CodigoArticulo))
                {
                    CodigoArticulo = orden.CodigoArticulo;
                    await BuscarArticuloAsync(orden.CodigoArticulo);
                }

                ActualizarEstadoValidacion();
                MensajeEstado = "Orden cargada correctamente";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error al cargar la orden: {ex.Message}";
            }
            finally
            {
                IsCargando = false;
            }
        }

        private async Task CargarFiltrosDeOrdenAsync(OrdenConteoDto orden)
        {
            if (string.IsNullOrEmpty(orden.FiltrosJson)) return;

            try
            {
                var filtros = JsonSerializer.Deserialize<Dictionary<string, object>>(orden.FiltrosJson);
                if (filtros == null) return;

                // Cargar filtros de ubicación
                if (filtros.ContainsKey("ubicacion"))
                {
                    var ubicacion = filtros["ubicacion"]?.ToString();
                    if (ubicacion == "")
                    {
                        UbicacionDirecta = "SIN UBICAR";
                    }
                    else
                    {
                        UbicacionDirecta = ubicacion ?? "SIN UBICAR";
                        UsarUbicacionDirecta = true;
                    }
                }
                else
                {
                    // Cargar filtros secuenciales
                    if (filtros.ContainsKey("pasillo") && int.TryParse(filtros["pasillo"]?.ToString(), out int pasilloValor))
                    {
                        Pasillo = pasilloValor;
                    }
                    if (filtros.ContainsKey("estanteria") && int.TryParse(filtros["estanteria"]?.ToString(), out int estanteriaValor))
                    {
                        Estanteria = estanteriaValor;
                    }
                    if (filtros.ContainsKey("altura") && int.TryParse(filtros["altura"]?.ToString(), out int alturaValor))
                    {
                        Altura = alturaValor;
                    }
                    if (filtros.ContainsKey("posicion") && int.TryParse(filtros["posicion"]?.ToString(), out int posicionValor))
                    {
                        Posicion = posicionValor;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando filtros: {ex.Message}");
            }
        }
        #endregion

        #region Métodos de carga de rangos y ubicaciones
        private async Task CargarRangosDisponiblesAsync()
        {
            try
            {
                if (AlmacenSeleccionado == null) return;

                var rangos = await _inventarioService.ObtenerRangosDisponiblesAsync(
                    SessionManager.EmpresaSeleccionada!.Value,
                    AlmacenSeleccionado.CodigoAlmacen
                );

                // Limpiar y cargar las colecciones
                PasillosDisponibles.Clear();
                EstanteriasDisponibles.Clear();
                AlturasDisponibles.Clear();
                PosicionesDisponibles.Clear();

                // Agregar opción "Todos" al principio de cada lista
                PasillosDisponibles.Add(new OpcionTodos { Texto = "Todos los pasillos" });
                EstanteriasDisponibles.Add(new OpcionTodos { Texto = "Todas las estanterías" });
                AlturasDisponibles.Add(new OpcionTodos { Texto = "Todas las alturas" });
                PosicionesDisponibles.Add(new OpcionTodos { Texto = "Todas las posiciones" });

                foreach (var pasillo in rangos.Pasillos ?? new List<int>())
                    PasillosDisponibles.Add(pasillo);

                foreach (var estanteria in rangos.Estanterias ?? new List<int>())
                    EstanteriasDisponibles.Add(estanteria);

                foreach (var altura in rangos.Alturas ?? new List<int>())
                    AlturasDisponibles.Add(altura);

                foreach (var posicion in rangos.Posiciones ?? new List<int>())
                    PosicionesDisponibles.Add(posicion);

                // NO establecer valores por defecto - los filtros son opcionales
                // El usuario puede seleccionar solo los filtros que necesite
                // Si no selecciona nada, se hace conteo de todo el almacén
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando rangos disponibles: {ex.Message}");
            }
        }

        private async Task CargarUbicacionesDisponiblesAsync()
        {
            if (AlmacenSeleccionado == null) return;

            try
            {
                var ubicaciones = await _ubicacionesService.ObtenerUbicacionesAsync(
                    AlmacenSeleccionado.CodigoAlmacen,
                    SessionManager.EmpresaSeleccionada!.Value,
                    soloConStock: false);

                UbicacionesDisponibles.Clear();

                // Agregar opción "SIN UBICAR" al principio
                UbicacionesDisponibles.Add("SIN UBICAR");

                // Agregar todas las ubicaciones ordenadas (filtrar vacías)
                foreach (var ubicacion in ubicaciones
                    .Where(u => !string.IsNullOrWhiteSpace(u.Ubicacion))
                    .OrderBy(u => u.Ubicacion))
                {
                    UbicacionesDisponibles.Add(ubicacion.Ubicacion);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando ubicaciones disponibles: {ex.Message}");
                // En caso de error, mantener solo "SIN UBICAR"
                UbicacionesDisponibles.Clear();
                UbicacionesDisponibles.Add("SIN UBICAR");
            }
        }
        #endregion

        #region Métodos de búsqueda de artículos
        private async Task BuscarArticuloAsync(string codigoArticulo)
        {
            try
            {
                var stockDisponible = await _stockService.ObtenerStockDisponibleAsync(codigoArticulo, null);
                ArticulosEncontrados.Clear();

                // Convertir StockDisponibleDto a ArticuloResumenDto
                foreach (var stock in stockDisponible)
                {
                    var articulo = new ArticuloResumenDto
                    {
                        CodigoArticulo = stock.CodigoArticulo,
                        DescripcionArticulo = stock.DescripcionArticulo
                    };
                    ArticulosEncontrados.Add(articulo);
                }

                // Si hay exactamente un resultado, seleccionarlo automáticamente
                if (ArticulosEncontrados.Count == 1)
                {
                    ArticuloSeleccionado = ArticulosEncontrados.First();
                    CodigoArticulo = ArticuloSeleccionado.CodigoArticulo;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error buscando artículo: {ex.Message}");
            }
        }
        #endregion

        #region Métodos de validación
        private void ActualizarEstadoValidacion()
        {
            bool esValido = !string.IsNullOrWhiteSpace(Titulo) &&
                           OperarioSeleccionado != null &&
                           FechaPlan.HasValue;

            if (EsConteoUbicacion)
            {
                esValido = esValido && AlmacenSeleccionado != null;
            }
            else
            {
                esValido = esValido && !string.IsNullOrWhiteSpace(CodigoArticulo);
            }

            PuedeActualizarOrden = esValido;
        }
        #endregion

        #region Generación de filtros JSON
        private string GenerarFiltrosJson()
        {
            var filtros = new Dictionary<string, object>();

            // Solo agregar almacén si es conteo por ubicación
            if (EsConteoUbicacion && AlmacenSeleccionado != null)
            {
                filtros["almacen"] = AlmacenSeleccionado.CodigoAlmacen;
            }

            if (EsConteoUbicacion)
            {
                // FLUJO 1: Conteo por ubicación
                if (UsarUbicacionDirecta)
                {
                    if (UbicacionDirecta == "SIN UBICAR")
                    {
                        // Para "SIN UBICAR", enviar ubicación vacía explícitamente
                        filtros["ubicacion"] = "";
                    }
                    else if (!string.IsNullOrWhiteSpace(UbicacionDirecta))
                    {
                        // Modo ubicación directa: usar solo la ubicación específica
                        filtros["ubicacion"] = UbicacionDirecta.Trim();
                    }
                }
                else
                {
                    // Filtros por componentes de ubicación (opcionales)
                    // Si no se especifica nada, se hace conteo de todo el almacén
                    if (Pasillo is int pasilloValor)
                        filtros["pasillo"] = pasilloValor.ToString();
                    if (Estanteria is int estanteriaValor)
                        filtros["estanteria"] = estanteriaValor.ToString();
                    if (Altura is int alturaValor)
                        filtros["altura"] = alturaValor.ToString();
                    if (Posicion is int posicionValor)
                        filtros["posicion"] = posicionValor.ToString();
                }
            }
            else
            {
                // FLUJO 2: Conteo por artículo
                if (!string.IsNullOrWhiteSpace(CodigoArticulo))
                    filtros["articulo"] = CodigoArticulo.Trim();
            }

            return JsonSerializer.Serialize(filtros);
        }
        #endregion

        #region Comandos
        [RelayCommand]
        private async Task BuscarArticulo()
        {
            if (string.IsNullOrWhiteSpace(ArticuloBuscado)) return;

            try
            {
                IsCargando = true;
                MensajeEstado = "Buscando artículo...";

                await BuscarArticuloAsync(ArticuloBuscado);
                ActualizarEstadoValidacion();

                MensajeEstado = ArticulosEncontrados.Count > 0 
                    ? $"Encontrados {ArticulosEncontrados.Count} artículos" 
                    : "No se encontraron artículos";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error al buscar artículo: {ex.Message}";
            }
            finally
            {
                IsCargando = false;
            }
        }


        #endregion

        #region Eventos de cambio de propiedades
        partial void OnTituloChanged(string value)
        {
            ActualizarEstadoValidacion();
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            ActualizarEstadoValidacion();
        }

        partial void OnFechaPlanChanged(DateTime? value)
        {
            ActualizarEstadoValidacion();
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? value)
        {
            ActualizarEstadoValidacion();
            if (value != null && EsConteoUbicacion)
            {
                _ = CargarRangosDisponiblesAsync();
                _ = CargarUbicacionesDisponiblesAsync();
            }
        }

        partial void OnEsConteoUbicacionChanged(bool value)
        {
            // Limpiar filtros cuando cambia el tipo de conteo
            if (value)
            {
                // Cambió a conteo por ubicación, limpiar campos de artículo
                CodigoArticulo = string.Empty;
                ArticuloBuscado = string.Empty;
                ArticulosEncontrados.Clear();
                ArticuloSeleccionado = null;
            }
            else
            {
                // Cambió a conteo por artículo, limpiar campos de ubicación
                Pasillo = null;
                Estanteria = null;
                Altura = null;
                Posicion = null;
                UbicacionDirecta = "SIN UBICAR";
            }

            ActualizarEstadoValidacion();
        }

        partial void OnArticuloSeleccionadoChanged(ArticuloResumenDto? value)
        {
            if (value != null)
            {
                CodigoArticulo = value.CodigoArticulo;
            }
            ActualizarEstadoValidacion();
        }

        partial void OnCodigoArticuloChanged(string value)
        {
            ActualizarEstadoValidacion();
        }

        partial void OnUsarUbicacionDirectaChanged(bool value)
        {
            if (value)
            {
                // Si se activa ubicación directa, limpiar filtros secuenciales
                Pasillo = null;
                Estanteria = null;
                Altura = null;
                Posicion = null;
            }
            else
            {
                // Si se desactiva ubicación directa, establecer "SIN UBICAR" por defecto
                UbicacionDirecta = "SIN UBICAR";
            }
            
            // Notificar cambio en la visibilidad
            OnPropertyChanged(nameof(MostrarFiltrosSecuenciales));
        }
        #endregion

        #region Clases auxiliares
        public class OpcionTodos
        {
            public string Texto { get; set; } = string.Empty;
            public override string ToString() => Texto;
        }

        public class PrioridadItem
        {
            public byte Valor { get; set; }
            public string Texto { get; set; } = string.Empty;
        }

        public class VisibilidadItem
        {
            public string Valor { get; set; } = string.Empty;
            public string Texto { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
        }
        #endregion
    }
}
