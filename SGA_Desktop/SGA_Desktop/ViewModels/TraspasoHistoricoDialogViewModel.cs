using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Globalization;

namespace SGA_Desktop.ViewModels
{
    public partial class TraspasoHistoricoDialogViewModel : ObservableObject
    {
        private readonly TraspasosService _traspasosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;

        // Propiedades para filtros
        [ObservableProperty] private DateTime? fechaDesde;
        [ObservableProperty] private DateTime? fechaHasta;
        [ObservableProperty] private string codigoArticulo = "";
        [ObservableProperty] private string codigoLote = "";
        [ObservableProperty] private string codigoPalet = "";
        [ObservableProperty] private string filtroObservaciones = "";
        [ObservableProperty] private AlmacenDto? almacenOrigenSeleccionado;
        [ObservableProperty] private AlmacenDto? almacenDestinoSeleccionado;
        [ObservableProperty] private EstadoTraspasoDto? estadoSeleccionado;
        [ObservableProperty] private OperariosAccesoDto? operarioSeleccionado;
        [ObservableProperty] private bool estaCargando = false;
        [ObservableProperty] private bool verTodasLasEmpresas = false;
        [ObservableProperty] private EmpresaDto? empresaFiltroSeleccionada;
        
        // Filtros específicos de ajustes
        [ObservableProperty] private OrigenAjusteDto? origenSeleccionado;
        public ObservableCollection<OrigenAjusteDto> Origenes { get; } = new();

        // Propiedad para mostrar el checkbox solo a admins
        public bool PuedeVerTodasLasEmpresas
        {
            get
            {
                var esAdmin = SessionManager.EsAdmin;
                System.Diagnostics.Debug.WriteLine($"[TraspasoHistoricoDialog] PuedeVerTodasLasEmpresas: {esAdmin}, IdRol: {SessionManager.UsuarioActual?.idRol}, Operario: {SessionManager.UsuarioActual?.operario}");
                return esAdmin;
            }
        }

        // Colección de empresas disponibles (las del usuario)
        public ObservableCollection<EmpresaDto> Empresas { get; } = new();

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> AlmacenesOrigen { get; } = new();
        public ObservableCollection<AlmacenDto> AlmacenesDestino { get; } = new();
        public ObservableCollection<EstadoTraspasoDto> Estados { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; } = new();
        
        // Propiedades para filtrado inteligente de almacenes
        [ObservableProperty] private string filtroAlmacenesOrigen = "";
        [ObservableProperty] private string filtroAlmacenesDestino = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenesOrigen = false;
        [ObservableProperty] private bool isDropDownOpenAlmacenesDestino = false;
        public ICollectionView AlmacenesOrigenView { get; private set; }
        public ICollectionView AlmacenesDestinoView { get; private set; }
        
        // Propiedades para autocompletado de operarios
        [ObservableProperty] private string filtroOperarios = "";
        [ObservableProperty] private bool isDropDownOpenOperarios = false;
        public ICollectionView OperariosView { get; private set; }

        // Propiedad para habilitar el campo de lote solo cuando hay artículo
        public bool PuedeFiltrarPorLote => !string.IsNullOrWhiteSpace(CodigoArticulo);

        // Propiedades calculadas para indicador de filtros activos
        public bool TieneFiltrosActivos
        {
            get
            {
                // Verificar si hay filtros activos (no son los valores por defecto)
                var almacenOrigenActivo = AlmacenOrigenSeleccionado != null;
                var almacenDestinoActivo = AlmacenDestinoSeleccionado != null;
                var estadoActivo = EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.CodigoEstado);
                var operarioActivo = OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0;
                var articuloActivo = !string.IsNullOrWhiteSpace(CodigoArticulo);
                var loteActivo = !string.IsNullOrWhiteSpace(CodigoLote);
                var fechaDesdeActiva = FechaDesde.HasValue && FechaDesde.Value != DateTime.Today;
                var fechaHastaActiva = FechaHasta.HasValue && FechaHasta.Value != DateTime.Today;

                return almacenOrigenActivo || almacenDestinoActivo || estadoActivo || operarioActivo || 
                       articuloActivo || loteActivo || fechaDesdeActiva || fechaHastaActiva;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

                if (AlmacenOrigenSeleccionado != null)
                {
                    filtros.Add($"Origen: {AlmacenOrigenSeleccionado.CodigoAlmacen}");
                }

                if (AlmacenDestinoSeleccionado != null)
                {
                    filtros.Add($"Destino: {AlmacenDestinoSeleccionado.CodigoAlmacen}");
                }

                if (EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.CodigoEstado))
                {
                    filtros.Add($"Estado: {EstadoSeleccionado.Descripcion}");
                }

                if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
                {
                    filtros.Add($"Operario: {OperarioSeleccionado.NombreOperario}");
                }

                if (!string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    filtros.Add($"Artículo: {CodigoArticulo}");
                }

                if (!string.IsNullOrWhiteSpace(CodigoLote))
                {
                    filtros.Add($"Lote: {CodigoLote}");
                }

                if (FechaDesde.HasValue && FechaHasta.HasValue)
                {
                    if (FechaDesde.Value != DateTime.Today || FechaHasta.Value != DateTime.Today)
                    {
                        filtros.Add($"Fechas: {FechaDesde.Value:dd/MM/yyyy} - {FechaHasta.Value:dd/MM/yyyy}");
                    }
                }

                return filtros.Count > 0 ? string.Join(" | ", filtros) : "Sin filtros";
            }
        }

        // Datos principales
        public ObservableCollection<TraspasoDto> Traspasos { get; } = new();
        [ObservableProperty] private TraspasoDto? traspasoSeleccionado;
        
        // Lista privada para almacenar todos los traspasos cargados (cuando VerTodasLasEmpresas está marcado)
        private List<TraspasoDto> _todosLosTraspasosCargados = new();

        // Comandos
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand CerrarCommand { get; }
        public IRelayCommand VerDetallesCommand { get; }
        
        // Comandos para manejo del dropdown de almacenes origen
        public IRelayCommand AbrirDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand LimpiarSeleccionAlmacenesOrigenCommand { get; }
        
        // Comandos para manejo del dropdown de almacenes destino
        public IRelayCommand AbrirDropDownAlmacenesDestinoCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesDestinoCommand { get; }
        public IRelayCommand LimpiarSeleccionAlmacenesDestinoCommand { get; }

        // Eventos
        public event Action<bool> RequestClose;

        public TraspasoHistoricoDialogViewModel(TraspasosService traspasosService)
        {
            _traspasosService = traspasosService;
            _stockService = new StockService();
            _loginService = new LoginService();

            // Inicializar ICollectionView para filtrado de operarios
            OperariosView = CollectionViewSource.GetDefaultView(OperariosDisponibles);
            OperariosView.Filter = FiltraOperario;
            
            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesOrigenView = CollectionViewSource.GetDefaultView(AlmacenesOrigen);
            AlmacenesOrigenView.Filter = FiltraAlmacenesOrigen;
            
            AlmacenesDestinoView = CollectionViewSource.GetDefaultView(AlmacenesDestino);
            AlmacenesDestinoView.Filter = FiltraAlmacenesDestino;

            // Cargar empresas disponibles (las del usuario)
            // Agregar opción "Todas" al inicio
            Empresas.Add(new EmpresaDto { Codigo = 0, Nombre = "Todas las empresas" });
            
            if (SessionManager.UsuarioActual?.empresas != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TraspasoHistoricoDialog] Cargando {SessionManager.UsuarioActual.empresas.Count} empresas del usuario");
                foreach (var empresa in SessionManager.UsuarioActual.empresas)
                {
                    Empresas.Add(empresa);
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistoricoDialog] Agregada empresa: {empresa.Codigo} - {empresa.Nombre}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[TraspasoHistoricoDialog] SessionManager.UsuarioActual?.empresas es null o vacío");
            }
            
            System.Diagnostics.Debug.WriteLine($"[TraspasoHistoricoDialog] Total empresas en combo: {Empresas.Count}");

            // Inicializar comandos
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
            CerrarCommand = new RelayCommand(Cerrar);
            VerDetallesCommand = new RelayCommand(VerDetalles, PuedeVerDetalles);
            
            // Inicializar comandos para dropdown de almacenes origen
            AbrirDropDownAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesOrigen = "";
                IsDropDownOpenAlmacenesOrigen = true;
            });
            
            CerrarDropDownAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenesOrigen = false;
            });
            
            LimpiarSeleccionAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                // No necesitamos limpiar selección aquí, solo actualizar el filtro
            });
            
            // Inicializar comandos para dropdown de almacenes destino
            AbrirDropDownAlmacenesDestinoCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesDestino = "";
                IsDropDownOpenAlmacenesDestino = true;
            });
            
            CerrarDropDownAlmacenesDestinoCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenesDestino = false;
            });
            
            LimpiarSeleccionAlmacenesDestinoCommand = new RelayCommand(() =>
            {
                // No necesitamos limpiar selección aquí, solo actualizar el filtro
            });

            // Inicialización
            _ = InitializeAsync();
            
            // Notificar cambio de PuedeVerTodasLasEmpresas después de la inicialización
            OnPropertyChanged(nameof(PuedeVerTodasLasEmpresas));
        }

        public TraspasoHistoricoDialogViewModel() : this(new TraspasosService()) { }

        // Validaciones de fechas sin carga automática
        partial void OnFechaDesdeChanged(DateTime? oldValue, DateTime? newValue)
        {
            if (newValue.HasValue && FechaHasta.HasValue && FechaHasta.Value < newValue.Value)
            {
                FechaHasta = newValue.Value;
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnFechaHastaChanged(DateTime? oldValue, DateTime? newValue)
        {
            if (newValue.HasValue && FechaDesde.HasValue && newValue.Value < FechaDesde.Value)
            {
                FechaHasta = FechaDesde.Value;
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        // Los cambios en filtros no cargan automáticamente - el usuario debe presionar "Aplicar filtros"
        
        partial void OnCodigoArticuloChanged(string value)
        {
            // Notificar cambio en PuedeFiltrarPorLote
            OnPropertyChanged(nameof(PuedeFiltrarPorLote));
            
            // Si se limpia el artículo, limpiar también el lote
            if (string.IsNullOrWhiteSpace(value))
            {
                CodigoLote = "";
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnCodigoLoteChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenOrigenSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnEstadoSeleccionadoChanged(EstadoTraspasoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }
        
        partial void OnFiltroOperariosChanged(string value)
        {
            OperariosView.Refresh(); // Actualiza el filtrado al teclear
        }
        
        // Métodos para manejar cambios en los filtros de almacenes
        partial void OnFiltroAlmacenesOrigenChanged(string value)
        {
            AlmacenesOrigenView?.Refresh();
        }
        
        partial void OnFiltroAlmacenesDestinoChanged(string value)
        {
            AlmacenesDestinoView?.Refresh();
        }

        // Cuando se desmarca "Ver todas las empresas", limpiar la selección del combo
        partial void OnVerTodasLasEmpresasChanged(bool oldValue, bool newValue)
        {
            if (!newValue)
            {
                // Si se desmarca, limpiar la selección del combo
                EmpresaFiltroSeleccionada = null;
                _todosLosTraspasosCargados.Clear();
            }
        }

        // Cuando cambia la empresa seleccionada en el combo, filtrar la lista en memoria
        partial void OnEmpresaFiltroSeleccionadaChanged(EmpresaDto? oldValue, EmpresaDto? newValue)
        {
            // Solo filtrar si "Ver todas las empresas" está marcado y hay datos cargados
            if (VerTodasLasEmpresas && _todosLosTraspasosCargados.Any())
            {
                FiltrarTraspasosPorEmpresa();
            }
        }

        // Método para filtrar los traspasos en memoria por empresa
        private void FiltrarTraspasosPorEmpresa()
        {
            try
            {
                Traspasos.Clear();
                
                // Si no hay empresa seleccionada o es "Todas" (codigo 0), mostrar todos
                if (EmpresaFiltroSeleccionada == null || EmpresaFiltroSeleccionada.Codigo == 0)
                {
                    foreach (var traspaso in _todosLosTraspasosCargados.OrderByDescending(t => t.FechaInicio))
                    {
                        Traspasos.Add(traspaso);
                    }
                    System.Diagnostics.Debug.WriteLine($"[Filtro empresa] Mostrando todos los traspasos: {Traspasos.Count}");
                }
                else
                {
                    // Filtrar por la empresa seleccionada
                    var traspasosFiltrados = _todosLosTraspasosCargados
                        .Where(t => t.CodigoEmpresa == EmpresaFiltroSeleccionada.Codigo)
                        .OrderByDescending(t => t.FechaInicio);
                    
                    foreach (var traspaso in traspasosFiltrados)
                    {
                        Traspasos.Add(traspaso);
                    }
                    System.Diagnostics.Debug.WriteLine($"[Filtro empresa] Filtrando por empresa {EmpresaFiltroSeleccionada.Codigo}: {Traspasos.Count} traspasos");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtrando traspasos por empresa: {ex.Message}");
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Establecer fechas por defecto (últimos días para ver traspasos recientes)
                FechaDesde = DateTime.Today; // Fecha de hoy
                FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00

                // Cargar almacenes
                await CargarAlmacenesAsync();

                // Cargar operarios
                await CargarOperariosAsync();

                // Cargar estados
                await CargarEstadosAsync();
                
                // Establecer "-- Todos los estados --" como selección por defecto
                EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado));

                // Cargar orígenes de ajustes
                await CargarOrigenesAsync();
                OrigenSeleccionado = Origenes.FirstOrDefault(o => string.IsNullOrEmpty(o.CodigoOrigen));

                // No establecer ninguna empresa por defecto - el usuario debe seleccionar si quiere filtrar por una específica
                // Si no hay empresa seleccionada y VerTodasLasEmpresas está marcado, se verán todas las empresas

                // No cargar traspasos automáticamente - el usuario debe presionar "Aplicar filtros"
                
                // Notificar cambio de PuedeVerTodasLasEmpresas después de cargar datos
                OnPropertyChanged(nameof(PuedeVerTodasLasEmpresas));
            }
            catch (Exception ex)
            {
                // Manejar error de inicialización
                System.Diagnostics.Debug.WriteLine($"Error en inicialización: {ex.Message}");
            }
        }

        private async Task CargarAlmacenesAsync()
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

                var operarioId = SessionManager.Operario;
                var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos, operarioId);

                AlmacenesOrigen.Clear();
                AlmacenesDestino.Clear();

                foreach (var almacen in almacenes)
                {
                    AlmacenesOrigen.Add(almacen);
                    AlmacenesDestino.Add(almacen);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
            }
        }

        private async Task CargarEstadosAsync()
        {
            try
            {
                Estados.Clear();
                
                // Opción para mostrar todos los estados
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "", Descripcion = "-- Todos los estados --" });
                
                // Estados reales del sistema de traspasos
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "PENDIENTE", Descripcion = "Pendiente" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "PENDIENTE_ERP", Descripcion = "Pendiente ERP" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "ERROR_ERP", Descripcion = "Error ERP" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "COMPLETADO", Descripcion = "Completado" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando estados: {ex.Message}");
            }
        }

        private async Task CargarOrigenesAsync()
        {
            try
            {
                Origenes.Clear();

                Origenes.Add(new OrigenAjusteDto { CodigoOrigen = "", Descripcion = "-- Todos los orígenes --" });
                Origenes.Add(new OrigenAjusteDto { CodigoOrigen = "TRASPASO", Descripcion = "Traspaso" });
                Origenes.Add(new OrigenAjusteDto { CodigoOrigen = "INVENTARIO", Descripcion = "Inventario" });
                Origenes.Add(new OrigenAjusteDto { CodigoOrigen = "CONTEO", Descripcion = "Conteo" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando orígenes: {ex.Message}");
            }
        }

        private async Task CargarTraspasosAsync()
        {
            try
            {
                EstaCargando = true;
                
                // Si "Ver todas las empresas" está marcado, usar la empresa seleccionada en el combo (si hay), o null para ver todas
                short? empresa;
                if (VerTodasLasEmpresas)
                {
                    // Si hay una empresa seleccionada y no es "Todas" (codigo 0), filtrar por esa; si no, ver todas (null)
                    if (EmpresaFiltroSeleccionada != null && EmpresaFiltroSeleccionada.Codigo != 0)
                    {
                        empresa = EmpresaFiltroSeleccionada.Codigo;
                    }
                    else
                    {
                        empresa = null; // Ver todas las empresas
                    }
                }
                else
                {
                    // Si no está marcado "Ver todas", usar la empresa actual
                    empresa = SessionManager.EmpresaSeleccionada;
                }
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] VerTodasLasEmpresas: {VerTodasLasEmpresas}, EmpresaFiltroSeleccionada: {EmpresaFiltroSeleccionada?.Codigo}, empresa pasada a API: {(empresa.HasValue ? empresa.Value.ToString() : "NULL (todas las empresas)")}");
                
                // Asegurar que las fechas estén bien configuradas (misma lógica que InventarioViewModel)
                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;
                
                System.Diagnostics.Debug.WriteLine($"Cargando traspasos desde: {fechaDesde:yyyy-MM-dd} hasta: {fechaHasta:yyyy-MM-dd}");
                
                // Determinar el estado para el filtro (si es vacío, no filtrar por estado)
                var estadoFiltro = EstadoSeleccionado?.CodigoEstado;
                if (string.IsNullOrEmpty(estadoFiltro))
                {
                    estadoFiltro = null; // No filtrar por estado
                }
                
                // Determinar el usuarioId para el filtro (si hay operario seleccionado)
                int? usuarioIdFiltro = null;
                if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
                {
                    usuarioIdFiltro = OperarioSeleccionado.Operario;
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Filtrando por operario en API: {usuarioIdFiltro}");
                }
                
                var traspasos = await _traspasosService.ObtenerTraspasosFiltradosAsync(
                    estado: estadoFiltro,
                    codigoPalet: null, // No filtramos por palet en este caso
                    almacenOrigen: AlmacenOrigenSeleccionado?.CodigoAlmacen,
                    almacenDestino: AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    fechaInicioDesde: fechaDesde.Date, // Solo la fecha, hora 00:00:00
                    fechaInicioHasta: fechaHasta.Date, // Solo la fecha, la API se encarga de incluir todo el día
                    usuarioId: usuarioIdFiltro,
                    codigoEmpresa: empresa // Filtrar por empresa seleccionada
                );

                System.Diagnostics.Debug.WriteLine($"API devolvió {traspasos.Count} traspasos");

                Traspasos.Clear();
                
                // 🔒 FILTRO DE SEGURIDAD: Aplicar filtro automático por almacenes permitidos del usuario
                // Si "Ver todas las empresas" está marcado, no filtrar por almacenes (ver todos)
                List<TraspasoDto> traspasosFiltrados;
                if (VerTodasLasEmpresas)
                {
                    // Admin viendo todas las empresas: no filtrar por almacenes
                    traspasosFiltrados = traspasos.ToList();
                    // Guardar todos los traspasos cargados para poder filtrar después en memoria
                    _todosLosTraspasosCargados = traspasosFiltrados.ToList();
                    System.Diagnostics.Debug.WriteLine($"[Admin] Viendo todas las empresas: {traspasosFiltrados.Count} traspasos sin filtrar por almacenes");
                }
                else
                {
                    // Usuario normal: filtrar por almacenes permitidos de su empresa
                    var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                    traspasosFiltrados = traspasos.Where(t => 
                        almacenesPermitidos.Contains(t.AlmacenOrigen) || 
                        almacenesPermitidos.Contains(t.AlmacenDestino)
                    ).ToList();
                    // Limpiar la lista de todos los traspasos ya que no estamos en modo "todas las empresas"
                    _todosLosTraspasosCargados.Clear();
                    System.Diagnostics.Debug.WriteLine($"Después del filtro de almacenes permitidos: {traspasosFiltrados.Count} traspasos");
                }
                
                // Si "Ver todas las empresas" está marcado y hay una empresa seleccionada en el combo, filtrar por empresa
                if (VerTodasLasEmpresas && EmpresaFiltroSeleccionada != null)
                {
                    traspasosFiltrados = traspasosFiltrados.Where(t => t.CodigoEmpresa == EmpresaFiltroSeleccionada.Codigo).ToList();
                    System.Diagnostics.Debug.WriteLine($"[Filtro empresa] Filtrando por empresa {EmpresaFiltroSeleccionada.Codigo}: {traspasosFiltrados.Count} traspasos");
                }
                
                // Aplicar filtros adicionales (artículo, lote y operario)
                var traspasosFiltradosFinal = traspasosFiltrados;
                
                // Filtro por código de artículo
                if (!string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t => 
                        !string.IsNullOrWhiteSpace(t.CodigoArticulo) && 
                        t.CodigoArticulo.Contains(CodigoArticulo, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"Después del filtro de artículo: {traspasosFiltradosFinal.Count} traspasos");
                }
                
                // Filtro por lote
                if (!string.IsNullOrWhiteSpace(CodigoLote))
                {
                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t => 
                        (!string.IsNullOrWhiteSpace(t.Partida) && 
                        t.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)) ||
                        (t.LineasPalet != null && t.LineasPalet.Any(l => 
                            !string.IsNullOrWhiteSpace(l.Lote) && 
                            l.Lote.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"Después del filtro de lote: {traspasosFiltradosFinal.Count} traspasos");
                }
                
                // El filtro de operario ya se aplica en la API mediante el parámetro usuarioId
                // Mantenemos este código como respaldo por si la API no filtra correctamente
                if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
                {
                    // Verificar que el filtro de la API funcionó correctamente
                    var traspasosSinFiltroOperario = traspasosFiltradosFinal.Where(t => 
                        t.UsuarioInicioId == OperarioSeleccionado.Operario || 
                        (t.UsuarioFinalizacionId.HasValue && t.UsuarioFinalizacionId == OperarioSeleccionado.Operario)
                    ).ToList();
                    
                    if (traspasosSinFiltroOperario.Count != traspasosFiltradosFinal.Count)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Advertencia: La API no filtró correctamente por operario. Aplicando filtro en memoria.");
                        traspasosFiltradosFinal = traspasosSinFiltroOperario;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Traspasos después del filtro de operario: {traspasosFiltradosFinal.Count}");
                }

                // Resolver nombres de operarios
                var operariosDict = OperariosDisponibles.ToDictionary(o => o.Operario.ToString(), o => ExtraerSoloNombre(o.NombreCompleto ?? "Sin nombre"));

                foreach (var traspaso in traspasosFiltradosFinal.OrderByDescending(t => t.FechaInicio))
                {
                    // Resolver nombre del operario de inicio
                    if (traspaso.UsuarioInicioId > 0 && string.IsNullOrEmpty(traspaso.UsuarioInicioNombre))
                    {
                        traspaso.UsuarioInicioNombre = operariosDict.GetValueOrDefault(traspaso.UsuarioInicioId.ToString(), $"ID: {traspaso.UsuarioInicioId}");
                    }

                    // Resolver nombre del operario de finalización si existe
                    if (traspaso.UsuarioFinalizacionId.HasValue && traspaso.UsuarioFinalizacionId > 0 && string.IsNullOrEmpty(traspaso.UsuarioFinalizacionNombre))
                    {
                        traspaso.UsuarioFinalizacionNombre = operariosDict.GetValueOrDefault(traspaso.UsuarioFinalizacionId.ToString(), $"ID: {traspaso.UsuarioFinalizacionId}");
                    }

                    Traspasos.Add(traspaso);
                    System.Diagnostics.Debug.WriteLine($"Traspaso: {traspaso.CodigoArticulo} - {traspaso.FechaInicio:yyyy-MM-dd HH:mm}");
                }
                
                System.Diagnostics.Debug.WriteLine($"Total final: {Traspasos.Count} traspasos");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando traspasos: {ex.Message}");
                // Aquí podrías mostrar un mensaje de error al usuario
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task AplicarFiltrosAsync()
        {
            await CargarTraspasosAsync();
            // Notificar cambios en propiedades calculadas después de aplicar filtros
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today; // Fecha de hoy
            FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00
            CodigoArticulo = "";
            CodigoLote = "";
            OperarioSeleccionado = null;
            AlmacenOrigenSeleccionado = null;
            AlmacenDestinoSeleccionado = null;
            EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado)); // "-- Todos los estados --"
            
            // Desmarcar "Ver todas las empresas" y reiniciar el combo
            VerTodasLasEmpresas = false;
            EmpresaFiltroSeleccionada = Empresas.FirstOrDefault(e => e.Codigo == 0); // "Todas las empresas"
            
            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            
            // Limpiar la lista de traspasos
            Traspasos.Clear();
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                // Intentar permiso específico para traspasos (permiso 12)
                var operarios = await _loginService.ObtenerOperariosConAccesoTraspasosAsync();

                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Operarios con permiso 12: {operarios.Count}");

                OperariosDisponibles.Clear();

                // Si no hay operarios con permiso 12, usar fallback automáticamente
                if (operarios.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[TraspasoHistorico] No hay operarios con permiso 12, usando fallback a permiso 13");
                    var operariosFallback = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                    operarios = operariosFallback;
                }

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }

                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Total operarios cargados: {OperariosDisponibles.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // Fallback final: usar operarios de conteos
                try
                {
                    var operariosFallback = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                    OperariosDisponibles.Clear();
                    foreach (var operario in operariosFallback.OrderBy(o => o.NombreOperario))
                    {
                        OperariosDisponibles.Add(operario);
                    }
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Fallback: {OperariosDisponibles.Count} operarios cargados");
                }
                catch
                {
                    OperariosDisponibles.Clear();
                    System.Diagnostics.Debug.WriteLine("[TraspasoHistorico] Error total: No se pudieron cargar operarios");
                }
            }
        }

        private void Cerrar()
        {
            RequestClose?.Invoke(false);
        }

        private bool PuedeVerDetalles()
        {
            return TraspasoSeleccionado != null;
        }

        private void VerDetalles()
        {
            if (TraspasoSeleccionado == null) return;

            // Aquí puedes implementar la lógica para mostrar detalles del traspaso
            // Por ejemplo, abrir otro dialog con información detallada
            System.Diagnostics.Debug.WriteLine($"Ver detalles del traspaso: {TraspasoSeleccionado.Id}");
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

        // Comandos para controlar el dropdown de operarios
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

        // Método de filtrado para operarios (búsqueda en cualquier parte del texto)
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
        
        // Método de filtrado para almacenes origen
        private bool FiltraAlmacenesOrigen(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesOrigen)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesOrigen, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        // Método de filtrado para almacenes destino
        private bool FiltraAlmacenesDestino(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesDestino)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesDestino, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // 🔒 MÉTODO DE SEGURIDAD: Obtener almacenes permitidos del usuario
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

                var operarioId = SessionManager.Operario;
                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos, operarioId);
                
                // Retornar solo los códigos de almacén permitidos
                return almacenesAutorizados.Select(a => a.CodigoAlmacen).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo almacenes permitidos: {ex.Message}");
                // En caso de error, retornar lista vacía para máxima seguridad
                return new List<string>();
            }
        }
    }
} 