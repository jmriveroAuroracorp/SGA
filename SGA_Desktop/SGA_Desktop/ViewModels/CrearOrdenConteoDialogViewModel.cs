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
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class CrearOrdenConteoDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly ConteosService _conteosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;
        #endregion

        #region Constructor
        public CrearOrdenConteoDialogViewModel(ConteosService conteosService, StockService stockService, LoginService loginService)
        {
            _conteosService = conteosService;
            _stockService = stockService;
            _loginService = loginService;
            
            // Inicializar colecciones
            PrioridadesDisponibles = new ObservableCollection<PrioridadItem>
            {
                new() { Valor = 1, Texto = "1 - Muy Baja" },
                new() { Valor = 2, Texto = "2 - Baja" },
                new() { Valor = 3, Texto = "3 - Normal" },
                new() { Valor = 4, Texto = "4 - Alta" },
                new() { Valor = 5, Texto = "5 - Muy Alta" }
            };

            VisibilidadesDisponibles = new ObservableCollection<VisibilidadItem>
            {
                new() { Valor = "VISIBLE", Texto = "Conteo Visible", Descripcion = "El operario puede ver las cantidades en stock" },
                new() { Valor = "CIEGO", Texto = "Conteo Ciego", Descripcion = "El operario NO puede ver las cantidades en stock" }
            };

            AlmacenesDisponibles = new ObservableCollection<AlmacenDto>();
            OperariosDisponibles = new ObservableCollection<OperariosAccesoDto>();

            // Valores por defecto
            EsConteoUbicacion = true; // Por defecto, conteo por ubicación
            PrioridadSeleccionada = PrioridadesDisponibles.FirstOrDefault(p => p.Valor == 3);
            VisibilidadSeleccionada = VisibilidadesDisponibles.FirstOrDefault(v => v.Valor == "VISIBLE");
            FechaPlan = DateTime.Today.AddDays(1);
            
            // Establecer operario actual como seleccionado por defecto
            CodigoOperario = SessionManager.UsuarioActual?.operario.ToString() ?? "";

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public CrearOrdenConteoDialogViewModel() : this(new ConteosService(), new StockService(), new LoginService()) { }
        #endregion

        #region Observable Properties
        public ObservableCollection<PrioridadItem> PrioridadesDisponibles { get; }
        public ObservableCollection<VisibilidadItem> VisibilidadesDisponibles { get; }
        public ObservableCollection<AlmacenDto> AlmacenesDisponibles { get; }
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; }

        [ObservableProperty]
        private string titulo = string.Empty;

        // Propiedades para separar los dos flujos
        [ObservableProperty]
        private bool esConteoUbicacion = true; // true = conteo por ubicación, false = conteo por artículo

        [ObservableProperty]
        private PrioridadItem? prioridadSeleccionada;

        [ObservableProperty]
        private VisibilidadItem? visibilidadSeleccionada;

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionado;

        [ObservableProperty]
        private OperariosAccesoDto? operarioSeleccionado;

        [ObservableProperty]
        private string codigoOperario = string.Empty;

        [ObservableProperty]
        private DateTime? fechaPlan;

        [ObservableProperty]
        private string comentario = string.Empty;

        // Filtros para conteos por ubicación
        [ObservableProperty]
        private string pasillo = string.Empty;

        [ObservableProperty]
        private string estanteria = string.Empty;

        [ObservableProperty]
        private string altura = string.Empty;

        [ObservableProperty]
        private string posicion = string.Empty;

        [ObservableProperty]
        private string ubicacionDirecta = string.Empty; // Para ubicaciones específicas

        // Filtros para conteos por artículo
        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        // Propiedades para búsqueda de artículos
        [ObservableProperty]
        private string articuloBuscado = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ArticuloResumenDto> articulosEncontrados = new();

        [ObservableProperty]
        private ArticuloResumenDto? articuloSeleccionado;

        // Estados
        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        // Referencia al diálogo para cerrarlo
        public Window? DialogResult { get; set; }
        #endregion

        #region Computed Properties
        public bool PuedeCrearOrden => !IsCargando && !string.IsNullOrWhiteSpace(Titulo) && OperarioSeleccionado != null;

        // Visibilidad para conteos por ubicación
        public bool MostrarConteoUbicacion => EsConteoUbicacion;
        public bool MostrarConteoArticulo => !EsConteoUbicacion;
        // Propiedad computada para el radio button
        public bool EsConteoArticulo => !EsConteoUbicacion;
        // Visibilidad para búsqueda de artículos
        public bool MostrarListaArticulos => ArticulosEncontrados.Count > 1;
        public bool MostrarInfoArticulo => ArticuloSeleccionado != null;
        #endregion

        #region Commands
        [RelayCommand]
        private async Task CrearOrden()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Creando orden de conteo...";

                // Validaciones
                if (string.IsNullOrWhiteSpace(Titulo))
                {
                    var warningDialog = new WarningDialog("Error de validación", "El título es obligatorio");
                    warningDialog.ShowDialog();
                    return;
                }

                // Crear el DTO
                var dto = new CrearOrdenConteoDto
                {
                    CodigoEmpresa = SessionManager.EmpresaSeleccionada ?? 1,
                    Titulo = Titulo.Trim(),
                    Visibilidad = VisibilidadSeleccionada?.Valor ?? "VISIBLE",
                    Estado = "ASIGNADO",
                    ModoGeneracion = "AUTOMATICO",
                    Alcance = EsConteoUbicacion ? "ALMACEN" : "ARTICULO", // Determinar alcance según flujo
                    FiltrosJson = GenerarFiltrosJson(),
                    FechaPlan = FechaPlan,
                    CreadoPorCodigo = SessionManager.UsuarioActual?.operario.ToString() ?? "ADMIN",
                    Prioridad = (byte)(PrioridadSeleccionada?.Valor ?? 3),
                    CodigoOperario = OperarioSeleccionado?.Operario == 0 ? null : OperarioSeleccionado?.Operario.ToString(),
                    CodigoAlmacen = EsConteoUbicacion ? AlmacenSeleccionado?.CodigoAlmacen : null, // Solo para conteos por ubicación
                    Comentario = string.IsNullOrWhiteSpace(Comentario) ? null : Comentario.Trim()
                };


                // Si el alcance es ARTICULO, agregar el código del artículo
                if (!EsConteoUbicacion && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    dto.CodigoArticulo = CodigoArticulo.Trim();
                }

                // Crear la orden
                var ordenCreada = await _conteosService.CrearOrdenAsync(dto);

                // Mostrar mensaje de éxito
                var successDialog = new WarningDialog(
                    "Orden Creada", 
                    $"La orden #{ordenCreada.GuidID} '{ordenCreada.Titulo}' ha sido creada exitosamente.");
                successDialog.ShowDialog();

                // Cerrar el diálogo
                DialogResult?.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al crear orden: {ex.Message}");
                var errorDialog = new WarningDialog(
                    "Error al crear orden", 
                    $"No se pudo crear la orden de conteo: {ex.Message}");
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
                MensajeEstado = string.Empty;
            }
        }

        [RelayCommand]
        private async Task BuscarArticulo()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ArticuloBuscado))
                {
                    var warningDialog = new WarningDialog("Buscar artículo", "Introduce un código o descripción para buscar.");
                    warningDialog.ShowDialog();
                    return;
                }

                // En modo "Por Artículo" no requerimos almacén específico
                if (EsConteoUbicacion && AlmacenSeleccionado == null)
                {
                    var warningDialog = new WarningDialog("Buscar artículo", "Primero selecciona un almacén.");
                    warningDialog.ShowDialog();
                    return;
                }

                ArticulosEncontrados.Clear();
                ArticuloSeleccionado = null;

                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var terminoBusqueda = ArticuloBuscado.Trim();
                
                List<StockDto> resultados = new();
                string tipoBusqueda = "";

                // Intentar buscar por código primero (si parece un código)
                if (terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" "))
                {
                    tipoBusqueda = "código";
                    resultados = await _stockService.ObtenerPorArticuloAsync(
                        empresa,
                        codigoArticulo: terminoBusqueda,
                        codigoAlmacen: EsConteoUbicacion ? AlmacenSeleccionado?.CodigoAlmacen : null
                    );
                }

                // Si no encuentra por código o el término parece una descripción, buscar por descripción
                if (!resultados.Any())
                {
                    tipoBusqueda = terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" ") ? 
                        "código (sin resultados), luego descripción" : "descripción";
                    
                    resultados = await _stockService.ObtenerPorArticuloAsync(
                        empresa,
                        codigoArticulo: null,
                        codigoAlmacen: EsConteoUbicacion ? AlmacenSeleccionado?.CodigoAlmacen : null,
                        descripcion: terminoBusqueda
                    );
                }

                // Agrupar por artículo
                var grupos = resultados
                    .GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
                    .Select(g => new ArticuloResumenDto
                    {
                        CodigoArticulo = g.Key.CodigoArticulo,
                        DescripcionArticulo = g.Key.DescripcionArticulo ?? ""
                    })
                    .OrderBy(a => a.CodigoArticulo)
                    .ToList();

                foreach (var articulo in grupos)
                {
                    ArticulosEncontrados.Add(articulo);
                }

                // Mostrar mensaje apropiado según los resultados
                if (ArticulosEncontrados.Count == 1)
                {
                    ArticuloSeleccionado = ArticulosEncontrados.First();
                    CodigoArticulo = ArticuloSeleccionado.CodigoArticulo;
                    var mensaje = $"✓ Encontrado por {tipoBusqueda}:\n{ArticuloSeleccionado.CodigoArticulo} - {ArticuloSeleccionado.DescripcionArticulo}";
                    var successDialog = new WarningDialog("Artículo encontrado", mensaje);
                    successDialog.ShowDialog();
                }
                else if (ArticulosEncontrados.Count > 1)
                {
                    var mensaje = $"Se encontraron {ArticulosEncontrados.Count} artículos por {tipoBusqueda}.\nSelecciona uno de la lista desplegable.";
                    var infoDialog = new WarningDialog("Múltiples resultados", mensaje);
                    infoDialog.ShowDialog();
                }
                else
                {
                    var mensaje = $"No se encontraron artículos buscando '{terminoBusqueda}' por {tipoBusqueda}";
                    if (EsConteoUbicacion && AlmacenSeleccionado != null)
                    {
                        mensaje += $" en el almacén {AlmacenSeleccionado.CodigoAlmacen}";
                    }
                    else
                    {
                        mensaje += " en ningún almacén";
                    }
                    mensaje += ".\n\n";
                    
                    mensaje += "💡 Consejos:\n";
                    mensaje += "• Para buscar por código: introduce el código exacto (ej: 10000)\n";
                    mensaje += "• Para buscar por descripción: introduce parte de la descripción (ej: azúcar)\n";
                    if (EsConteoUbicacion)
                    {
                        mensaje += "• Verifica que el artículo tiene stock en este almacén";
                    }
                    else
                    {
                        mensaje += "• Verifica que el artículo tiene stock en algún almacén";
                    }
                    
                    var warningDialog = new WarningDialog("Sin resultados", mensaje);
                    warningDialog.ShowDialog();
                }

                // Notificar cambios en visibilidad
                OnPropertyChanged(nameof(MostrarListaArticulos));
                OnPropertyChanged(nameof(MostrarInfoArticulo));
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al buscar artículo: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            DialogResult?.Close();
        }
        #endregion

        #region Private Methods
        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenes();
                await CargarOperarios();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
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

                AlmacenSeleccionado = AlmacenesDisponibles.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
                // En caso de error, agregar almacén por defecto
                AlmacenesDisponibles.Clear();
                var almacenDefecto = new AlmacenDto
                {
                    CodigoAlmacen = "01",
                    NombreAlmacen = "Almacén Principal",
                    CodigoEmpresa = (short)(SessionManager.EmpresaSeleccionada ?? 1)
                };
                AlmacenesDisponibles.Add(almacenDefecto);
                AlmacenSeleccionado = almacenDefecto;
            }
        }

        private async Task CargarOperarios()
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

                // Seleccionar el operario actual si está en la lista
                var operarioActual = SessionManager.UsuarioActual?.operario;
                if (operarioActual.HasValue)
                {
                    OperarioSeleccionado = OperariosDisponibles.FirstOrDefault(o => o.Operario == operarioActual.Value);
                }
                
                // Si no se encontró, no seleccionar ninguno (forzar selección manual)
                if (OperarioSeleccionado == null && OperariosDisponibles.Count > 0)
                {
                    // No seleccionar automáticamente - el usuario debe elegir
                    OperarioSeleccionado = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // En caso de error, dejar la lista vacía
                OperariosDisponibles.Clear();
                OperarioSeleccionado = null;
            }
        }

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
                    if (!string.IsNullOrWhiteSpace(UbicacionDirecta))
                    {
                        filtros["ubicacion"] = UbicacionDirecta.Trim();
                    }
                    else
                    {
                        // Filtros por componentes de ubicación
                        if (!string.IsNullOrWhiteSpace(Pasillo))
                            filtros["pasillo"] = Pasillo.Trim();
                        if (!string.IsNullOrWhiteSpace(Estanteria))
                            filtros["estanteria"] = Estanteria.Trim();
                        if (!string.IsNullOrWhiteSpace(Altura))
                            filtros["altura"] = Altura.Trim();
                        if (!string.IsNullOrWhiteSpace(Posicion))
                            filtros["posicion"] = Posicion.Trim();
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
        // Métodos de cambio de propiedades
        partial void OnEsConteoUbicacionChanged(bool value)
        {
            OnPropertyChanged(nameof(MostrarConteoUbicacion));
            OnPropertyChanged(nameof(MostrarConteoArticulo));
            
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
                Pasillo = string.Empty;
                Estanteria = string.Empty;
                Altura = string.Empty;
                Posicion = string.Empty;
                UbicacionDirecta = string.Empty;
                
                // Asegurar que hay un almacén seleccionado para conteos por artículo
                if (AlmacenSeleccionado == null && AlmacenesDisponibles.Any())
                {
                    AlmacenSeleccionado = AlmacenesDisponibles.FirstOrDefault();
                }
            }
            
            OnPropertyChanged(nameof(MostrarListaArticulos));
            OnPropertyChanged(nameof(MostrarInfoArticulo));
        }

        partial void OnTituloChanged(string value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(PuedeCrearOrden));
        }

        partial void OnArticuloSeleccionadoChanged(ArticuloResumenDto? value)
        {
            if (value != null)
            {
                CodigoArticulo = value.CodigoArticulo;
            }
            OnPropertyChanged(nameof(MostrarInfoArticulo));
        }

        partial void OnArticulosEncontradosChanged(ObservableCollection<ArticuloResumenDto> value)
        {
            OnPropertyChanged(nameof(MostrarListaArticulos));
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? value)
        {
            // Limpiar búsqueda de artículos cuando cambia el almacén
            ArticuloBuscado = string.Empty;
            ArticulosEncontrados.Clear();
            ArticuloSeleccionado = null;
            CodigoArticulo = string.Empty;
            OnPropertyChanged(nameof(MostrarListaArticulos));
            OnPropertyChanged(nameof(MostrarInfoArticulo));
        }
        #endregion
    }

    // Clase auxiliar para prioridades
    public class PrioridadItem
    {
        public byte Valor { get; set; }
        public string Texto { get; set; } = string.Empty;
    }

    // Clase auxiliar para visibilidades
    public class VisibilidadItem
    {
        public string Valor { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
    }
} 