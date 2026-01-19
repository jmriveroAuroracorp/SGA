using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using System.Windows.Data;

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

        // Propiedades para filtrado de operarios
        [ObservableProperty]
        private string filtroOperarios = string.Empty;
        
        [ObservableProperty]
        private bool isDropDownOpenOperarios = false;

        [ObservableProperty]
        private DateTime? fechaPlan;

        [ObservableProperty]
        private DateTime? fechaProximaRenovacion;

        [ObservableProperty]
        private int? frecuenciaDias;

        [ObservableProperty]
        private string comentario = string.Empty;
        
        // Propiedad calculada para saber si es un conteo periódico
        public bool EsPeriodico { get; private set; } = false;
        
        // Propiedad para saber si es un conteo periódico activo (solo se pueden editar campos limitados)
        public bool EsPeriodicoActivo { get; private set; } = false;
        
        // Propiedades calculadas para habilitar/deshabilitar campos
        public bool CamposEditables => !EsPeriodicoActivo; // Título, prioridad, almacén, etc.
        public bool CamposPeriodicosEditables => true; // Operario, comentario, fecha renovación siempre editables
        
        // Fecha mínima para la próxima renovación (mañana)
        public DateTime FechaMinimaRenovacion => DateTime.Now.Date.AddDays(1);
        #endregion

        #region Propiedades de filtros de ubicaci?n
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

        // Propiedades para controlar el estado de los ComboBox
        [ObservableProperty]
        private bool estanteriaHabilitada = true;

        [ObservableProperty]
        private bool alturaHabilitada = true;

        [ObservableProperty]
        private bool posicionHabilitada = true;

        // Propiedad calculada para mostrar/ocultar filtros secuenciales
        public bool MostrarFiltrosSecuenciales => !UsarUbicacionDirecta;
        #endregion

        #region Propiedades de filtros de art?culo
        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        [ObservableProperty]
        private string articuloBuscado = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ArticuloResumenDto> articulosEncontrados = new();

        [ObservableProperty]
        private ArticuloResumenDto? articuloSeleccionado;

        [ObservableProperty]
        private bool articuloTieneStockVirtual = true;
        #endregion

        #region Propiedades de estado
        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private bool puedeActualizarOrden = false;

        // Referencia al di?logo para cerrarlo
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

        // Vista filtrada para operarios
        public ICollectionView OperariosView { get; private set; } = null!;

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
        public bool EsConteoArticulo => !EsConteoUbicacion;
        public bool MostrarListaArticulos => ArticulosEncontrados.Count > 1;
        public bool MostrarInfoArticulo => ArticuloSeleccionado != null;
        public bool MostrarAdvertenciaSinStock => MostrarInfoArticulo && !ArticuloTieneStockVirtual;
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
            
            // Inicializar ICollectionView para filtrado de operarios
            OperariosView = CollectionViewSource.GetDefaultView(OperariosDisponibles);
            OperariosView.Filter = FiltraOperario;
            
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
                
                // Validar fecha de próxima renovación si es periódico
                if (EsPeriodico && !FechaProximaRenovacion.HasValue)
                {
                    var errorDialog = new WarningDialog(
                        "Fecha requerida",
                        "Debe especificar una fecha de próxima renovación para activar el conteo periódico.");
                    errorDialog.ShowDialog();
                    return;
                }
                
                var fechaMinima = DateTime.Now.Date.AddDays(1); // Mañana como mínimo
                if (EsPeriodico && FechaProximaRenovacion.HasValue && FechaProximaRenovacion.Value.Date < fechaMinima)
                {
                    var errorDialog = new WarningDialog(
                        "Fecha inválida",
                        "La fecha de próxima renovación debe ser del día siguiente en adelante. No se puede seleccionar hoy ni fechas pasadas.");
                    errorDialog.ShowDialog();
                    return;
                }

                CrearOrdenConteoDto dto;
                
                // Si es un conteo periódico activo, solo actualizar campos permitidos
                if (EsPeriodicoActivo)
                {
                    // Solo actualizar: Operario, Comentario, FechaProximaRenovacion, FrecuenciaDias, Prioridad
                    // Usar valores actuales de la orden para el resto
                    dto = new CrearOrdenConteoDto
                    {
                        CodigoEmpresa = ordenActual.CodigoEmpresa,
                        Titulo = ordenActual.Titulo, // Mantener original
                        Visibilidad = ordenActual.Visibilidad, // Mantener original
                        Estado = ordenActual.Estado, // Mantener original
                        ModoGeneracion = ordenActual.ModoGeneracion, // Mantener original
                        Alcance = ordenActual.Alcance, // Mantener original
                        FiltrosJson = ordenActual.FiltrosJson, // Mantener original
                        FechaPlan = ordenActual.FechaPlan, // Mantener original
                        CreadoPorCodigo = ordenActual.CreadoPorCodigo, // Mantener original
                        Prioridad = (byte)(PrioridadSeleccionada?.Valor ?? ordenActual.Prioridad), // EDITABLE
                        CodigoOperario = OperarioSeleccionado?.Operario == 0 ? null : OperarioSeleccionado?.Operario.ToString(), // EDITABLE
                        CodigoAlmacen = ordenActual.CodigoAlmacen, // Mantener original
                        Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim(), // EDITABLE
                        EsPeriodico = true,
                        FechaProximaRenovacion = FechaProximaRenovacion, // EDITABLE
                        FrecuenciaDias = FrecuenciaDias // EDITABLE
                    };
                    
                    // Mantener artículo si existe
                    if (!string.IsNullOrEmpty(ordenActual.CodigoArticulo))
                    {
                        dto.CodigoArticulo = ordenActual.CodigoArticulo;
                    }
                    
                    Debug.WriteLine($"Actualizando conteo periódico ACTIVO - Solo campos permitidos:");
                    Debug.WriteLine($"  - Prioridad: {dto.Prioridad}");
                    Debug.WriteLine($"  - CodigoOperario: {dto.CodigoOperario}");
                    Debug.WriteLine($"  - Comentario: {dto.Comentario}");
                    Debug.WriteLine($"  - FechaProximaRenovacion: {dto.FechaProximaRenovacion}");
                    Debug.WriteLine($"  - FrecuenciaDias: {dto.FrecuenciaDias}");
                }
                else
                {
                    // Edición normal: todos los campos son editables
                    dto = new CrearOrdenConteoDto
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
                        Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim(),
                        EsPeriodico = EsPeriodico,
                        FechaProximaRenovacion = EsPeriodico ? FechaProximaRenovacion : null,
                        FrecuenciaDias = EsPeriodico ? FrecuenciaDias : null
                    };

                    // Si el alcance es ARTICULO, agregar el código del artículo
                    if (!EsConteoUbicacion && !string.IsNullOrWhiteSpace(CodigoArticulo))
                    {
                        dto.CodigoArticulo = CodigoArticulo.Trim();
                    }
                    
                    Debug.WriteLine($"Actualizando orden normal - Todos los campos:");
                    Debug.WriteLine($"  - Titulo: {dto.Titulo}");
                    Debug.WriteLine($"  - Prioridad: {dto.Prioridad}");
                    Debug.WriteLine($"  - CodigoOperario: {dto.CodigoOperario}");
                    Debug.WriteLine($"  - Comentario: {dto.Comentario}");
                    Debug.WriteLine($"  - FechaProximaRenovacion: {dto.FechaProximaRenovacion}");
                }

                // Actualizar la orden
                var ordenActualizada = await _conteosService.ActualizarOrdenAsync(_ordenGuid, dto);

                // Mostrar mensaje de ?xito
                var successDialog = new WarningDialog(
                    "Orden Actualizada", 
                    $"La orden '{ordenActualizada.Titulo}' ha sido actualizada exitosamente.");
                successDialog.ShowDialog();

                // Cerrar el diálogo con resultado true - buscar la ventana padre y cerrarla
                var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
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

        #region M?todos de inicializaci?n
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

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

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
                
                // Refrescar la vista filtrada
                OperariosView?.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // En caso de error, dejar la lista vac?a
                OperariosDisponibles.Clear();
                OperariosView?.Refresh();
            }
        }
        #endregion

        #region M?todos p?blicos para cargar datos de la orden
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
                Comentario = orden.Comentario ?? string.Empty;
                
                // Detectar si es periódico y si está activo
                EsPeriodico = orden.EsPeriodico;
                EsPeriodicoActivo = orden.EsPeriodico && orden.Activo;
                
                // Para conteos periódicos activos, mostrar la fecha de hoy
                // (se renuevan automáticamente según FechaProximaRenovacion, pero es útil mostrar fecha actual)
                // Para otros casos, cargar la fecha planificada normalmente
                if (EsPeriodicoActivo)
                {
                    FechaPlan = DateTime.Now.Date; // Mostrar fecha de hoy para conteos periódicos activos
                }
                else
                {
                    FechaPlan = orden.FechaPlan;
                }
                
                OnPropertyChanged(nameof(EsPeriodico));
                OnPropertyChanged(nameof(EsPeriodicoActivo));
                OnPropertyChanged(nameof(CamposEditables));
                OnPropertyChanged(nameof(CamposPeriodicosEditables));
                
                // Cargar fecha de próxima renovación
                if (orden.EsPeriodico && orden.Activo)
                {
                    // Si está activo, cargar la fecha actual o usar la fecha original si existe
                    FechaProximaRenovacion = orden.FechaProximaRenovacion;
                }
                else if (orden.EsPeriodico && !orden.Activo)
                {
                    // Si está desactivado, usar el día actual como valor por defecto
                    FechaProximaRenovacion = DateTime.Now.Date;
                }
                
                // Cargar frecuencia de días
                FrecuenciaDias = orden.FrecuenciaDias;

                // Cargar prioridad
                PrioridadSeleccionada = PrioridadesDisponibles.FirstOrDefault(p => p.Valor == orden.Prioridad);

                // Cargar visibilidad
                VisibilidadSeleccionada = VisibilidadesDisponibles.FirstOrDefault(v => v.Valor == orden.Visibilidad);

                // IMPORTANTE: Cargar las listas PRIMERO antes de seleccionar valores
                await CargarAlmacenesAsync();
                await CargarOperariosAsync();

                // AHORA S? cargar almac?n (despu?s de que se hayan cargado los almacenes)
                if (!string.IsNullOrEmpty(orden.CodigoAlmacen))
                {
                    Debug.WriteLine($"Buscando almac?n con c?digo: '{orden.CodigoAlmacen}'");
                    Debug.WriteLine($"Almacenes disponibles: {AlmacenesDisponibles.Count}");
                    foreach (var almacen in AlmacenesDisponibles)
                    {
                        Debug.WriteLine($"  - {almacen.CodigoAlmacen}: {almacen.NombreAlmacen}");
                    }
                    
                    // Intentar diferentes formas de comparaci?n
                    AlmacenSeleccionado = AlmacenesDisponibles.FirstOrDefault(a => 
                        a.CodigoAlmacen == orden.CodigoAlmacen ||
                        a.CodigoAlmacen == orden.CodigoAlmacen?.Trim());
                    Debug.WriteLine($"Almac?n seleccionado: {(AlmacenSeleccionado != null ? $"{AlmacenSeleccionado.CodigoAlmacen} - {AlmacenSeleccionado.NombreAlmacen}" : "NO ENCONTRADO")}");
                }

                // AHORA S? cargar operario (despu?s de que se hayan cargado los operarios)
                if (!string.IsNullOrEmpty(orden.CodigoOperario))
                {
                    Debug.WriteLine($"Buscando operario con c?digo: '{orden.CodigoOperario}'");
                    Debug.WriteLine($"Operarios disponibles: {OperariosDisponibles.Count}");
                    foreach (var operario in OperariosDisponibles)
                    {
                        Debug.WriteLine($"  - {operario.Operario}: {operario.NombreOperario}");
                    }
                    
                    // Intentar diferentes formas de comparaci?n
                    OperarioSeleccionado = OperariosDisponibles.FirstOrDefault(o => 
                        o.Operario.ToString() == orden.CodigoOperario ||
                        o.Operario.ToString() == orden.CodigoOperario?.Trim() ||
                        o.Operario == int.Parse(orden.CodigoOperario ?? "0"));
                    Debug.WriteLine($"Operario seleccionado: {(OperarioSeleccionado != null ? $"{OperarioSeleccionado.Operario} - {OperarioSeleccionado.NombreOperario}" : "NO ENCONTRADO")}");
                }

                // Cargar filtros
                await CargarFiltrosDeOrdenAsync(orden);

                // Cargar rangos si es conteo por ubicaci?n
                if (EsConteoUbicacion && AlmacenSeleccionado != null)
                {
                    await CargarRangosDisponiblesAsync();
                    await CargarUbicacionesDisponiblesAsync();
                }

                // Cargar art?culo si es conteo por art?culo
                if (!EsConteoUbicacion && !string.IsNullOrEmpty(orden.CodigoArticulo))
                {
                    CodigoArticulo = orden.CodigoArticulo;
                    await BuscarArticuloAsync(orden.CodigoArticulo);
                    
                    // Asegurar que se seleccione el art?culo correcto si hay m?ltiples resultados
                    if (ArticulosEncontrados.Count > 1)
                    {
                        ArticuloSeleccionado = ArticulosEncontrados.FirstOrDefault(a => 
                            a.CodigoArticulo == orden.CodigoArticulo) ?? ArticulosEncontrados.First();
                    }
                    // Si no se encontr? pero hay c?digo, crear el DTO manualmente
                    else if (ArticulosEncontrados.Count == 0 && !string.IsNullOrEmpty(orden.CodigoArticulo))
                    {
                        ArticuloSeleccionado = new ArticuloResumenDto
                        {
                            CodigoArticulo = orden.CodigoArticulo,
                            DescripcionArticulo = orden.DescripcionArticulo ?? "Art?culo sin stock virtual registrado"
                        };
                        CodigoArticulo = orden.CodigoArticulo;
                    }
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

                // Cargar filtros de ubicaci?n
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

                // Actualizar el estado de habilitaci?n despu?s de cargar los filtros
                ActualizarEstadoFiltros();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando filtros: {ex.Message}");
            }
        }
        #endregion

        #region M?todos de carga de rangos y ubicaciones
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

                // Agregar opci?n "Todos" al principio de cada lista
                PasillosDisponibles.Add(new OpcionTodos { Texto = "Todos los pasillos" });
                EstanteriasDisponibles.Add(new OpcionTodos { Texto = "Todas las estanter?as" });
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

                // Actualizar el estado de habilitaci?n despu?s de cargar los rangos
                ActualizarEstadoFiltros();

                // NO establecer valores por defecto - los filtros son opcionales
                // El usuario puede seleccionar solo los filtros que necesite
                // Si no selecciona nada, se hace conteo de todo el almac?n
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

                // Agregar opci?n "SIN UBICAR" al principio
                UbicacionesDisponibles.Add("SIN UBICAR");

                // Agregar todas las ubicaciones ordenadas (filtrar vac?as)
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

        #region M?todos de b?squeda de art?culos
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

                // Si hay exactamente un resultado, seleccionarlo autom?ticamente
                if (ArticulosEncontrados.Count == 1)
                {
                    ArticuloSeleccionado = ArticulosEncontrados.First();
                    CodigoArticulo = ArticuloSeleccionado.CodigoArticulo;
                    ArticuloTieneStockVirtual = true; // Tiene stock porque se encontr? en la b?squeda
                }
                else if (ArticulosEncontrados.Count > 0)
                {
                    ArticuloTieneStockVirtual = true; // Tiene stock porque se encontraron resultados
                }
                else
                {
                    // Si no se encontraron resultados pero se proporcion? un c?digo, permitir crear sin stock virtual
                    if (!string.IsNullOrWhiteSpace(codigoArticulo))
                    {
                        ArticuloSeleccionado = new ArticuloResumenDto
                        {
                            CodigoArticulo = codigoArticulo,
                            DescripcionArticulo = "Art?culo sin stock virtual registrado"
                        };
                        CodigoArticulo = codigoArticulo;
                        ArticuloTieneStockVirtual = false; // NO tiene stock virtual
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error buscando art?culo: {ex.Message}");
            }
        }
        #endregion

        #region M?todos de validaci?n
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

        #region Generaci?n de filtros JSON
        private string GenerarFiltrosJson()
        {
            var filtros = new Dictionary<string, object>();

            // Solo agregar almac?n si es conteo por ubicaci?n
            if (EsConteoUbicacion && AlmacenSeleccionado != null)
            {
                filtros["almacen"] = AlmacenSeleccionado.CodigoAlmacen;
            }

            if (EsConteoUbicacion)
            {
                // FLUJO 1: Conteo por ubicaci?n
                if (UsarUbicacionDirecta)
                {
                    if (UbicacionDirecta == "SIN UBICAR")
                    {
                        // Para "SIN UBICAR", enviar ubicaci?n vac?a expl?citamente
                        filtros["ubicacion"] = "";
                    }
                    else if (!string.IsNullOrWhiteSpace(UbicacionDirecta))
                    {
                        // Modo ubicaci?n directa: usar solo la ubicaci?n espec?fica
                        filtros["ubicacion"] = UbicacionDirecta.Trim();
                    }
                }
                else
                {
                    // Filtros por componentes de ubicaci?n (opcionales)
                    // Si no se especifica nada, se hace conteo de todo el almac?n
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
                // FLUJO 2: Conteo por art?culo
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
                MensajeEstado = "Buscando art?culo...";

                await BuscarArticuloAsync(ArticuloBuscado);
                ActualizarEstadoValidacion();

                MensajeEstado = ArticulosEncontrados.Count > 0 
                    ? $"Encontrados {ArticulosEncontrados.Count} art?culos" 
                    : "No se encontraron art?culos";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error al buscar art?culo: {ex.Message}";
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
                // Limpiar filtros cuando cambia el almac?n
                Pasillo = null;
                Estanteria = null;
                Altura = null;
                Posicion = null;
                EstanteriaHabilitada = true;
                AlturaHabilitada = true;
                PosicionHabilitada = true;
                
                _ = CargarRangosDisponiblesAsync();
                _ = CargarUbicacionesDisponiblesAsync();
            }
        }

        partial void OnEsConteoUbicacionChanged(bool value)
        {
            // Limpiar filtros cuando cambia el tipo de conteo
            if (value)
            {
                // Cambi? a conteo por ubicaci?n, limpiar campos de art?culo
                CodigoArticulo = string.Empty;
                ArticuloBuscado = string.Empty;
                ArticulosEncontrados.Clear();
                ArticuloSeleccionado = null;
                // Restablecer estado de habilitaci?n
                EstanteriaHabilitada = true;
                AlturaHabilitada = true;
                PosicionHabilitada = true;
            }
            else
            {
                // Cambi? a conteo por art?culo, limpiar campos de ubicaci?n
                Pasillo = null;
                Estanteria = null;
                Altura = null;
                Posicion = null;
                UbicacionDirecta = "SIN UBICAR";
                // Restablecer estado de habilitaci?n
                EstanteriaHabilitada = true;
                AlturaHabilitada = true;
                PosicionHabilitada = true;
            }

            ActualizarEstadoValidacion();
        }

        partial void OnArticuloSeleccionadoChanged(ArticuloResumenDto? value)
        {
            if (value != null)
            {
                CodigoArticulo = value.CodigoArticulo;
                // Si se selecciona de la lista de encontrados, tiene stock virtual
                // Si la descripci?n indica que no tiene stock, mantenerlo en false
                if (value.DescripcionArticulo != "Art?culo sin stock virtual registrado")
                {
                    ArticuloTieneStockVirtual = true;
                }
            }
            OnPropertyChanged(nameof(MostrarInfoArticulo));
            OnPropertyChanged(nameof(MostrarAdvertenciaSinStock));
            ActualizarEstadoValidacion();
        }

        partial void OnArticuloTieneStockVirtualChanged(bool value)
        {
            OnPropertyChanged(nameof(MostrarAdvertenciaSinStock));
        }

        partial void OnCodigoArticuloChanged(string value)
        {
            ActualizarEstadoValidacion();
        }

        partial void OnUsarUbicacionDirectaChanged(bool value)
        {
            if (value)
            {
                // Si se activa ubicaci?n directa, limpiar filtros secuenciales
                Pasillo = null;
                Estanteria = null;
                Altura = null;
                Posicion = null;
            }
            else
            {
                // Si se desactiva ubicaci?n directa, establecer "SIN UBICAR" por defecto
                UbicacionDirecta = "SIN UBICAR";
                // Restablecer estado de habilitaci?n
                EstanteriaHabilitada = true;
                AlturaHabilitada = true;
                PosicionHabilitada = true;
            }
            
            // Notificar cambio en la visibilidad
            OnPropertyChanged(nameof(MostrarFiltrosSecuenciales));
        }

        partial void OnPasilloChanged(object? value)
        {
            // Si se selecciona "Todos los pasillos", bloquear y limpiar los filtros m?s espec?ficos
            if (value is OpcionTodos)
            {
                EstanteriaHabilitada = false;
                AlturaHabilitada = false;
                PosicionHabilitada = false;
                Estanteria = null;
                Altura = null;
                Posicion = null;
            }
            else
            {
                // Si se selecciona un pasillo espec?fico, habilitar estanter?a
                EstanteriaHabilitada = true;
                // Re-evaluar el estado de altura y posici?n basado en estanter?a
                ActualizarEstadoFiltros();
            }
        }

        partial void OnEstanteriaChanged(object? value)
        {
            // Si se selecciona "Todas las estanter?as", bloquear y limpiar los filtros m?s espec?ficos
            if (value is OpcionTodos)
            {
                AlturaHabilitada = false;
                PosicionHabilitada = false;
                Altura = null;
                Posicion = null;
            }
            else
            {
                // Si se selecciona una estanter?a espec?fica, habilitar altura
                AlturaHabilitada = true;
                // Re-evaluar el estado de posici?n basado en altura
                ActualizarEstadoFiltros();
            }
        }

        partial void OnAlturaChanged(object? value)
        {
            // Si se selecciona "Todas las alturas", bloquear y limpiar el filtro m?s espec?fico
            if (value is OpcionTodos)
            {
                PosicionHabilitada = false;
                Posicion = null;
            }
            else
            {
                // Si se selecciona una altura espec?fica, habilitar posici?n
                PosicionHabilitada = true;
            }
        }

        private void ActualizarEstadoFiltros()
        {
            // Re-evaluar el estado de altura basado en estanter?a
            if (Estanteria is OpcionTodos)
            {
                AlturaHabilitada = false;
                PosicionHabilitada = false;
            }
            else if (Estanteria != null)
            {
                AlturaHabilitada = true;
                // Re-evaluar posici?n basado en altura
                if (Altura is OpcionTodos)
                {
                    PosicionHabilitada = false;
                }
                else if (Altura != null)
                {
                    PosicionHabilitada = true;
                }
            }
        }
        #endregion

        #region M?todos de filtrado de operarios
        partial void OnFiltroOperariosChanged(string value)
        {
            OperariosView?.Refresh(); // Actualiza el filtrado al teclear
        }

        private bool FiltraOperario(object obj)
        {
            if (string.IsNullOrWhiteSpace(FiltroOperarios)) return true;
            if (obj is not OperariosAccesoDto operario) return false;

            // B?squeda acento-insensible, sin may?sc/min?sc, en cualquier parte del texto
            var compare = CultureInfo.CurrentCulture.CompareInfo;
            var options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

            bool contiene(string s) =>
                !string.IsNullOrEmpty(s) &&
                compare.IndexOf(s, FiltroOperarios, options) >= 0;

            return contiene(operario.NombreOperario) || contiene(operario.NombreCompleto);
        }

        [RelayCommand]
        private void AbrirDropDownOperarios()
        {
            FiltroOperarios = ""; // Limpiar el filtro para permitir escribir desde cero
            IsDropDownOpenOperarios = true;
        }

        [RelayCommand]
        private void CerrarDropDownOperarios()
        {
            IsDropDownOpenOperarios = false;
        }

        [RelayCommand]
        private void LimpiarSeleccionOperarios()
        {
            OperarioSeleccionado = null;
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
