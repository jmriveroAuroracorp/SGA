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
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
    // Clase para representar la opción "Todos" en los ComboBox
    public class OpcionTodos
    {
        public string Texto { get; set; } = "Todos";
        public int? Valor { get; set; } = null;
        
        public override string ToString() => Texto;
    }

    public partial class CrearOrdenConteoDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly ConteosService _conteosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;
        private readonly InventarioService _inventarioService;
        private readonly UbicacionesService _ubicacionesService;
        #endregion

        #region Constructor
        public CrearOrdenConteoDialogViewModel(ConteosService conteosService, StockService stockService, LoginService loginService, InventarioService inventarioService, UbicacionesService ubicacionesService)
        {
            _conteosService = conteosService;
            _stockService = stockService;
            _loginService = loginService;
            _inventarioService = inventarioService;
            _ubicacionesService = ubicacionesService;
            
            // Inicializar colecciones
            PrioridadesDisponibles = new ObservableCollection<PrioridadItem>
            {
                new() { Valor = 1, Texto = "1 - Muy Baja", Nombre = "Muy Baja" },
                new() { Valor = 2, Texto = "2 - Baja", Nombre = "Baja" },
                new() { Valor = 3, Texto = "3 - Normal", Nombre = "Normal" },
                new() { Valor = 4, Texto = "4 - Alta", Nombre = "Alta" },
                new() { Valor = 5, Texto = "5 - Muy Alta", Nombre = "Muy Alta" }
            };

            VisibilidadesDisponibles = new ObservableCollection<VisibilidadItem>
            {
                new() { Valor = "VISIBLE", Texto = "Conteo Visible", Descripcion = "El operario puede ver las cantidades en stock" },
                new() { Valor = "CIEGO", Texto = "Conteo Ciego", Descripcion = "El operario NO puede ver las cantidades en stock" }
            };

            AlmacenesDisponibles = new ObservableCollection<AlmacenDto>();
            OperariosDisponibles = new ObservableCollection<OperariosAccesoDto>();

            // Inicializar ICollectionView para filtrado de operarios (aunque la colección esté vacía)
            OperariosView = CollectionViewSource.GetDefaultView(OperariosDisponibles);
            OperariosView.Filter = FiltraOperario;
            
            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesView = CollectionViewSource.GetDefaultView(AlmacenesDisponibles);
            AlmacenesView.Filter = FiltraAlmacen;

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

        public CrearOrdenConteoDialogViewModel() : this(new ConteosService(), new StockService(), new LoginService(), new InventarioService(), new UbicacionesService()) { }
        #endregion

        #region Observable Properties
        public ObservableCollection<PrioridadItem> PrioridadesDisponibles { get; }
        public ObservableCollection<VisibilidadItem> VisibilidadesDisponibles { get; }
        public ObservableCollection<AlmacenDto> AlmacenesDisponibles { get; }
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; }
        
        // Vista filtrada para operarios
        public ICollectionView OperariosView { get; private set; } = null!;
        
        // Vista filtrada para almacenes (excluye "TODOS" en modo ubicación)
        public ICollectionView AlmacenesView { get; private set; } = null!;

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
        
        // Propiedades para filtrado de operarios
        [ObservableProperty]
        private string filtroOperarios = string.Empty;
        
        [ObservableProperty]
        private bool isDropDownOpenOperarios = false;

        [ObservableProperty]
        private string comentario = string.Empty;

        // Filtros para conteos por ubicación
        [ObservableProperty]
        private object? pasillo;

        [ObservableProperty]
        private object? estanteria;

        [ObservableProperty]
        private object? altura;

        [ObservableProperty]
        private object? posicion;

        [ObservableProperty]
        private string ubicacionDirecta = string.Empty; // Para ubicaciones específicas

        // Ubicaciones disponibles para el ComboBox
        [ObservableProperty]
        private ObservableCollection<string> ubicacionesDisponibles = new();

        // Propiedades para controlar el estado de los ComboBox
        [ObservableProperty]
        private bool estanteriaHabilitada = true;

        [ObservableProperty]
        private bool alturaHabilitada = true;

        [ObservableProperty]
        private bool posicionHabilitada = true;

        // Propiedades para controlar el modo de selección
        [ObservableProperty]
        private bool usarUbicacionDirecta = false;

        // Propiedad calculada para mostrar/ocultar filtros secuenciales
        public bool MostrarFiltrosSecuenciales => !UsarUbicacionDirecta;

        // Rangos disponibles (para los combos automáticos)
        [ObservableProperty]
        private ObservableCollection<object> pasillosDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> estanteriasDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> alturasDisponibles = new();

        [ObservableProperty]
        private ObservableCollection<object> posicionesDisponibles = new();

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

        [ObservableProperty]
        private bool articuloTieneStockVirtual = true; // Indica si el artículo tiene stock virtual registrado

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
        public bool MostrarAdvertenciaSinStock => MostrarInfoArticulo && !ArticuloTieneStockVirtual;
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

                // Solo requerir almacén en conteos por ubicación (y que no sea "TODOS")
                if (EsConteoUbicacion && (AlmacenSeleccionado == null || AlmacenSeleccionado.CodigoAlmacen == "TODOS"))
                {
                    var warningDialog = new WarningDialog("Error de validación", "Selecciona un almacén específico antes de crear la orden.");
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
                    CodigoAlmacen = AlmacenSeleccionado?.CodigoAlmacen == "TODOS" ? null : AlmacenSeleccionado?.CodigoAlmacen,
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
                if (EsConteoUbicacion && (AlmacenSeleccionado == null || AlmacenSeleccionado.CodigoAlmacen == "TODOS"))
                {
                    var warningDialog = new WarningDialog("Buscar artículo", "Primero selecciona un almacén específico.");
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
                        codigoAlmacen: AlmacenSeleccionado?.CodigoAlmacen == "TODOS" ? null : AlmacenSeleccionado?.CodigoAlmacen
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
                        codigoAlmacen: AlmacenSeleccionado?.CodigoAlmacen == "TODOS" ? null : AlmacenSeleccionado?.CodigoAlmacen,
                        descripcion: terminoBusqueda
                    );
                }

                // Si no hay almacén seleccionado o es "TODOS", filtrar por almacenes permitidos del usuario
                if (AlmacenSeleccionado == null || AlmacenSeleccionado.CodigoAlmacen == "TODOS")
                {
                    var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                    resultados = resultados.Where(r => almacenesPermitidos.Contains(r.CodigoAlmacen)).ToList();
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
                    ArticuloTieneStockVirtual = true; // Tiene stock porque se encontró en la búsqueda
                    var mensaje = $"✓ Encontrado por {tipoBusqueda}:\n{ArticuloSeleccionado.CodigoArticulo} - {ArticuloSeleccionado.DescripcionArticulo}";
                    if (AlmacenSeleccionado == null || AlmacenSeleccionado.CodigoAlmacen == "TODOS")
                    {
                        mensaje += "\n\n(Se buscará en todos los almacenes permitidos)";
                    }
                    var successDialog = new WarningDialog("Artículo encontrado", mensaje);
                    successDialog.ShowDialog();
                }
                else if (ArticulosEncontrados.Count > 1)
                {
                    ArticuloTieneStockVirtual = true; // Tiene stock porque se encontraron resultados
                    var mensaje = $"Se encontraron {ArticulosEncontrados.Count} artículos por {tipoBusqueda}.\nSelecciona uno de la lista desplegable.";
                    if (AlmacenSeleccionado == null || AlmacenSeleccionado.CodigoAlmacen == "TODOS")
                    {
                        mensaje += "\n\n(Resultados de todos los almacenes permitidos)";
                    }
                    var infoDialog = new WarningDialog("Múltiples resultados", mensaje);
                    infoDialog.ShowDialog();
                }
                else
                {
                    // Si no encuentra resultados pero parece un código de artículo, permitir usarlo directamente
                    // (puede haber stock físico aunque no esté registrado virtualmente)
                    bool pareceCodigo = terminoBusqueda.Length <= 20 && !terminoBusqueda.Contains(" ");
                    
                    if (pareceCodigo)
                    {
                        // Permitir usar el código directamente aunque no haya stock virtual
                        ArticuloSeleccionado = new ArticuloResumenDto
                        {
                            CodigoArticulo = terminoBusqueda,
                            DescripcionArticulo = "Artículo sin stock virtual registrado"
                        };
                        CodigoArticulo = terminoBusqueda;
                        ArticuloTieneStockVirtual = false; // NO tiene stock virtual
                        
                        var mensaje = $"⚠️ No se encontró stock virtual para el código '{terminoBusqueda}'";
                        if (AlmacenSeleccionado != null && AlmacenSeleccionado.CodigoAlmacen != "TODOS")
                        {
                            var nombreAlmacen = !string.IsNullOrWhiteSpace(AlmacenSeleccionado.NombreAlmacen) 
                                ? $" ({AlmacenSeleccionado.NombreAlmacen})" 
                                : "";
                            mensaje += $" en el almacén {AlmacenSeleccionado.CodigoAlmacen}{nombreAlmacen}";
                        }
                        else
                        {
                            mensaje += " en los almacenes permitidos";
                        }
                        mensaje += ".\n\n";
                        mensaje += "✅ Se permitirá crear el conteo igualmente, ya que puede haber stock físico aunque no esté registrado virtualmente.\n\n";
                        mensaje += "💡 El conteo servirá para verificar y ajustar el stock real.";
                        
                        var infoDialog = new WarningDialog("Artículo sin stock virtual", mensaje);
                        infoDialog.ShowDialog();
                    }
                    else
                    {
                        ArticuloTieneStockVirtual = true; // Resetear si no es un código válido
                        // Si parece una descripción, mostrar mensaje informativo
                        var mensaje = $"No se encontraron artículos buscando '{terminoBusqueda}' por descripción";
                        if (AlmacenSeleccionado != null && AlmacenSeleccionado.CodigoAlmacen != "TODOS")
                        {
                            var nombreAlmacen = !string.IsNullOrWhiteSpace(AlmacenSeleccionado.NombreAlmacen) 
                                ? $" ({AlmacenSeleccionado.NombreAlmacen})" 
                                : "";
                            mensaje += $" en el almacén {AlmacenSeleccionado.CodigoAlmacen}{nombreAlmacen}";
                        }
                        else
                        {
                            mensaje += " en los almacenes permitidos";
                        }
                        mensaje += ".\n\n";
                        mensaje += "💡 Sugerencias:\n";
                        mensaje += "• Intenta buscar por código exacto del artículo\n";
                        mensaje += "• Verifica que has escrito correctamente la descripción\n";
                        mensaje += "• Puedes escribir el código del artículo directamente en el campo de búsqueda";
                        
                        var warningDialog = new WarningDialog("Sin resultados", mensaje);
                        warningDialog.ShowDialog();
                    }
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

                // Agregar opción "TODOS" al inicio
                var opcionTodos = new AlmacenDto
                {
                    CodigoAlmacen = "TODOS",
                    NombreAlmacen = "Todos los almacenes",
                    CodigoEmpresa = (short)empresa
                };
                AlmacenesDisponibles.Add(opcionTodos);

                // Agregar el resto de almacenes
                foreach (var a in resultado)
                    AlmacenesDisponibles.Add(a);

                // Refrescar la vista filtrada
                AlmacenesView?.Refresh();

                // Establecer selección por defecto según el modo
                if (EsConteoUbicacion)
                {
                    // En modo ubicación, seleccionar el primer almacén específico (no "TODOS")
                    var almacenEspecifico = AlmacenesDisponibles.FirstOrDefault(a => a.CodigoAlmacen != "TODOS");
                    AlmacenSeleccionado = almacenEspecifico ?? opcionTodos;
                }
                else
                {
                    // En modo artículo, seleccionar "TODOS" por defecto
                    AlmacenSeleccionado = opcionTodos;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
                // En caso de error, agregar al menos la opción TODOS
                AlmacenesDisponibles.Clear();
                var opcionTodos = new AlmacenDto
                {
                    CodigoAlmacen = "TODOS",
                    NombreAlmacen = "Todos los almacenes",
                    CodigoEmpresa = (short)(SessionManager.EmpresaSeleccionada ?? 1)
                };
                AlmacenesDisponibles.Add(opcionTodos);
                AlmacenSeleccionado = opcionTodos;
            }
        }

        private async Task<List<string>> ObtenerAlmacenesPermitidosAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                
                if (!permisos.Any())
                {
                    permisos = await _stockService.ObtenerAlmacenesAsync(centro);
                }

                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
                
                // Retornar solo los códigos de almacén permitidos
                return almacenesAutorizados.Select(a => a.CodigoAlmacen).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error obteniendo almacenes permitidos: {ex.Message}");
                // En caso de error, retornar lista vacía para máxima seguridad
                return new List<string>();
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

                // La vista ya está inicializada en el constructor, solo refrescar
                OperariosView?.Refresh();

                // Dejar el combo en blanco - el usuario debe seleccionar manualmente
                OperarioSeleccionado = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // En caso de error, dejar la lista vacía
                OperariosDisponibles.Clear();
                OperarioSeleccionado = null;
            }
        }

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
                // En caso de error, limpiar las colecciones
                PasillosDisponibles.Clear();
                EstanteriasDisponibles.Clear();
                AlturasDisponibles.Clear();
                PosicionesDisponibles.Clear();
            }
        }

        private async Task CargarUbicacionesDisponiblesAsync()
        {
            try
            {
                if (AlmacenSeleccionado == null) return;

                var ubicaciones = await _ubicacionesService.ObtenerUbicacionesAsync(
                    AlmacenSeleccionado.CodigoAlmacen,
                    SessionManager.EmpresaSeleccionada!.Value,
                    soloConStock: false // Cargar todas las ubicaciones, no solo las que tienen stock
                );

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

       private string GenerarFiltrosJson()
            {
                var filtros = new Dictionary<string, object>();

                // Incluir el almacén solo si se ha seleccionado y no es "TODOS"
                // En conteos por ubicación es obligatorio, en conteos por artículo es opcional
                if (AlmacenSeleccionado != null && AlmacenSeleccionado.CodigoAlmacen != "TODOS")
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
                            // Para "Sin ubicar", enviar ubicación vacía explícitamente
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
        // Métodos de cambio de propiedades
        partial void OnEsConteoUbicacionChanged(bool value)
        {
            OnPropertyChanged(nameof(MostrarConteoUbicacion));
            OnPropertyChanged(nameof(MostrarConteoArticulo));
            
            // Actualizar el filtro de almacenes cuando cambia el tipo de conteo
            AlmacenesView?.Refresh();
            
            // Limpiar filtros cuando cambia el tipo de conteo
            if (value)
            {
                // Cambió a conteo por ubicación, limpiar campos de artículo
                CodigoArticulo = string.Empty;
                ArticuloBuscado = string.Empty;
                ArticulosEncontrados.Clear();
                ArticuloSeleccionado = null;
                
                // Asegurar que hay un almacén seleccionado (no "TODOS") para conteos por ubicación
                if (AlmacenSeleccionado == null || AlmacenSeleccionado.CodigoAlmacen == "TODOS")
                {
                    var almacenEspecifico = AlmacenesDisponibles.FirstOrDefault(a => a.CodigoAlmacen != "TODOS");
                    if (almacenEspecifico != null)
                    {
                        AlmacenSeleccionado = almacenEspecifico;
                    }
                }
            }
            else
            {
                // Cambió a conteo por artículo, limpiar campos de ubicación
                Pasillo = null;
                Estanteria = null;
                Altura = null;
                Posicion = null;
                UbicacionDirecta = "SIN UBICAR";
                
                // Resetear estado de habilitación
                EstanteriaHabilitada = true;
                AlturaHabilitada = true;
                PosicionHabilitada = true;
                
                // En conteos por artículo, establecer "TODOS" por defecto
                var opcionTodos = AlmacenesDisponibles.FirstOrDefault(a => a.CodigoAlmacen == "TODOS");
                if (opcionTodos != null)
                {
                    AlmacenSeleccionado = opcionTodos;
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
                // Si se selecciona de la lista de encontrados, tiene stock virtual
                // Si la descripción indica que no tiene stock, mantenerlo en false
                if (value.DescripcionArticulo != "Artículo sin stock virtual registrado")
                {
                    ArticuloTieneStockVirtual = true;
                }
            }
            OnPropertyChanged(nameof(MostrarInfoArticulo));
            OnPropertyChanged(nameof(MostrarAdvertenciaSinStock));
        }

        partial void OnArticulosEncontradosChanged(ObservableCollection<ArticuloResumenDto> value)
        {
            OnPropertyChanged(nameof(MostrarListaArticulos));
        }

        partial void OnArticuloTieneStockVirtualChanged(bool value)
        {
            OnPropertyChanged(nameof(MostrarAdvertenciaSinStock));
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

            // Cargar rangos disponibles y ubicaciones solo cuando se selecciona un almacén específico (no "TODOS")
            if (value != null && value.CodigoAlmacen != "TODOS")
            {
                _ = CargarRangosDisponiblesAsync();
                _ = CargarUbicacionesDisponiblesAsync();
            }
        }

        partial void OnPasilloChanged(object? value)
        {
            // Si se selecciona "Todos los pasillos", bloquear y limpiar los filtros más específicos
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
                // Si se selecciona un pasillo específico, habilitar estantería
                EstanteriaHabilitada = true;
                // Re-evaluar el estado de altura y posición basado en estantería
                ActualizarEstadoFiltros();
            }
        }

        partial void OnEstanteriaChanged(object? value)
        {
            // Si se selecciona "Todas las estanterías", bloquear y limpiar los filtros más específicos
            if (value is OpcionTodos)
            {
                AlturaHabilitada = false;
                PosicionHabilitada = false;
                Altura = null;
                Posicion = null;
            }
            else
            {
                // Si se selecciona una estantería específica, habilitar altura
                AlturaHabilitada = true;
                // Re-evaluar el estado de posición basado en altura
                ActualizarEstadoFiltros();
            }
        }

        partial void OnAlturaChanged(object? value)
        {
            // Si se selecciona "Todas las alturas", bloquear y limpiar el filtro más específico
            if (value is OpcionTodos)
            {
                PosicionHabilitada = false;
                Posicion = null;
            }
            else
            {
                // Si se selecciona una altura específica, habilitar posición
                PosicionHabilitada = true;
            }
        }

        partial void OnPosicionChanged(object? value)
        {
            // No hay filtros más específicos que la posición
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
                // Si se desactiva ubicación directa, establecer "Sin ubicar" por defecto
                UbicacionDirecta = "SIN UBICAR";
            }
            
            // Notificar cambio en la visibilidad
            OnPropertyChanged(nameof(MostrarFiltrosSecuenciales));
        }

        private void ActualizarEstadoFiltros()
        {
            // Re-evaluar el estado de altura basado en estantería
            if (Estanteria is OpcionTodos)
            {
                AlturaHabilitada = false;
                PosicionHabilitada = false;
            }
            else if (Estanteria != null)
            {
                AlturaHabilitada = true;
                // Re-evaluar posición basado en altura
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

        // Métodos para filtrado de operarios
        partial void OnFiltroOperariosChanged(string value)
        {
            OperariosView?.Refresh(); // Actualiza el filtrado al teclear
        }

        private bool FiltraOperario(object obj)
        {
            if (string.IsNullOrWhiteSpace(FiltroOperarios)) return true;
            if (obj is not OperariosAccesoDto operario) return false;

            // Búsqueda acento-insensible, sin mayúsc/minúsc, en cualquier parte del texto
            var compare = CultureInfo.CurrentCulture.CompareInfo;
            var options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

            bool contiene(string s) =>
                !string.IsNullOrEmpty(s) &&
                compare.IndexOf(s, FiltroOperarios, options) >= 0;

            return contiene(operario.NombreOperario) || contiene(operario.NombreCompleto);
        }

        private bool FiltraAlmacen(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            
            // En modo conteo por ubicación, excluir "TODOS"
            if (EsConteoUbicacion && almacen.CodigoAlmacen == "TODOS")
            {
                return false;
            }
            
            return true;
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
    }

    // Clase auxiliar para prioridades
    public class PrioridadItem
    {
        public byte Valor { get; set; }
        public string Texto { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty; // Solo el nombre sin el número
    }

    // Clase auxiliar para visibilidades
    public class VisibilidadItem
    {
        public string Valor { get; set; } = string.Empty;
        public string Texto { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
    }
} 