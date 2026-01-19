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
using System.Windows;
using SGA_Desktop.Dialog;

namespace SGA_Desktop.ViewModels
{
    public partial class TraspasoHistoricoViewModel : ObservableObject
    {
        private readonly TraspasosService _traspasosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;
        private readonly InventarioService _inventarioService;

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
        [ObservableProperty] private bool debeHacerScrollAlInicio = false;
        [ObservableProperty] private bool verTodasLasEmpresas = false;
        [ObservableProperty] private EmpresaDto? empresaFiltroSeleccionada;

        // Propiedad para mostrar el checkbox solo a admins
        public bool PuedeVerTodasLasEmpresas => SessionManager.EsAdmin;
        
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
                var paletActivo = !string.IsNullOrWhiteSpace(CodigoPalet);
                var observacionesActivo = !string.IsNullOrWhiteSpace(FiltroObservaciones);
                var fechaDesdeActiva = FechaDesde.HasValue && FechaDesde.Value != DateTime.Today;
                var fechaHastaActiva = FechaHasta.HasValue && FechaHasta.Value != DateTime.Today;
                var origenActivo = OrigenSeleccionado != null && !string.IsNullOrEmpty(OrigenSeleccionado.CodigoOrigen);
                var verTodasLasEmpresasActivo = VerTodasLasEmpresas;

                return almacenOrigenActivo || almacenDestinoActivo || estadoActivo || operarioActivo || 
                       articuloActivo || loteActivo || paletActivo || observacionesActivo || fechaDesdeActiva || fechaHastaActiva || origenActivo || verTodasLasEmpresasActivo;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

                // 🔷 SIEMPRE mostrar las fechas (incluso si son las por defecto)
                if (FechaDesde.HasValue && FechaHasta.HasValue)
                {
                    filtros.Add($"Fechas: {FechaDesde.Value:dd/MM/yyyy} - {FechaHasta.Value:dd/MM/yyyy}");
                }
                else if (FechaDesde.HasValue)
                {
                    filtros.Add($"Desde: {FechaDesde.Value:dd/MM/yyyy}");
                }
                else if (FechaHasta.HasValue)
                {
                    filtros.Add($"Hasta: {FechaHasta.Value:dd/MM/yyyy}");
                }

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

                if (!string.IsNullOrWhiteSpace(CodigoPalet))
                {
                    filtros.Add($"Palet: {CodigoPalet}");
                }

                if (!string.IsNullOrWhiteSpace(FiltroObservaciones))
                {
                    filtros.Add($"Observaciones: {FiltroObservaciones}");
                }

                if (OrigenSeleccionado != null && !string.IsNullOrEmpty(OrigenSeleccionado.CodigoOrigen))
                {
                    filtros.Add($"Origen: {OrigenSeleccionado.Descripcion}");
                }

                // Agregar información sobre el filtro de empresas
                if (VerTodasLasEmpresas)
                {
                    if (EmpresaFiltroSeleccionada != null && EmpresaFiltroSeleccionada.Codigo != 0)
                    {
                        filtros.Add($"Empresa: {EmpresaFiltroSeleccionada.Nombre}");
                    }
                    else
                    {
                        filtros.Add("Todas las empresas");
                    }
                }

                return filtros.Count > 0 ? string.Join(" | ", filtros) : "Sin filtros";
            }
        }

        // Datos principales (colecciones públicas filtradas)
        public ObservableCollection<TraspasoDto> Traspasos { get; } = new();
        [ObservableProperty] private TraspasoDto? traspasoSeleccionado;
        
        // Pestañas
        [ObservableProperty] private bool mostrandoAuroraSga = true;
        [ObservableProperty] private bool mostrandoStorageControl = false;
        
        // Datos para StorageControl
        public ObservableCollection<TraspasoStorageControlDto> TraspasosStorageControl { get; } = new();
        [ObservableProperty] private TraspasoStorageControlDto? traspasoStorageControlSeleccionado;
        
        // Datos para Ajustes (colección pública filtrada)
        public ObservableCollection<AjusteDto> Ajustes { get; } = new();
        [ObservableProperty] private AjusteDto? ajusteSeleccionado;
        
        // Datos combinados (SGA Actual + Ajustes)
        public ObservableCollection<object> TraspasosYAjustes { get; } = new();
        [ObservableProperty] private object? traspasoOAjusteSeleccionado;
        
        // Colecciones privadas para almacenar TODOS los datos sin filtrar (para filtrado en memoria)
        private List<TraspasoDto> _todosLosTraspasos = new();
        private List<AjusteDto> _todosLosAjustes = new();
        private DateTime? _ultimaFechaDesdeCargada;
        private DateTime? _ultimaFechaHastaCargada;
        
        // Cache para almacenes permitidos (evitar llamadas duplicadas)
        private List<string>? _almacenesPermitidosCache = null;
        private DateTime? _ultimaActualizacionAlmacenes = null;
        
        // Protección para evitar ejecuciones simultáneas de AplicarFiltrosEnMemoria
        private bool _aplicandoFiltros = false;
        
        // Estados finales que no necesitan actualizarse (ya son definitivos)
        // ERROR_ERP NO es final porque se pueden hacer reintentos y cambiar de estado
        private static readonly HashSet<string> EstadosFinalesTraspasos = new(StringComparer.OrdinalIgnoreCase)
        {
            "COMPLETADO",
            "CANCELADO"
        };
        
        private static readonly HashSet<string> EstadosFinalesAjustes = new(StringComparer.OrdinalIgnoreCase)
        {
            "COMPLETADO",
            "CANCELADO"
        };
        
        private bool EsEstadoFinalTraspaso(string? codigoEstado)
        {
            return !string.IsNullOrWhiteSpace(codigoEstado) && 
                   EstadosFinalesTraspasos.Contains(codigoEstado);
        }
        
        private bool EsEstadoFinalAjuste(string? estado)
        {
            return !string.IsNullOrWhiteSpace(estado) && 
                   EstadosFinalesAjustes.Contains(estado);
        }
        
        // Filtros específicos de ajustes
        [ObservableProperty] private OrigenAjusteDto? origenSeleccionado;
        public ObservableCollection<OrigenAjusteDto> Origenes { get; } = new();

        // Comandos
        public IRelayCommand AbrirFiltrosCommand { get; }
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand VerDetallesCommand { get; }
        public IRelayCommand CambiarAAuroraSgaCommand { get; }
        public IRelayCommand CambiarAStorageControlCommand { get; }
        public IAsyncRelayCommand AplicarFiltrosStorageControlCommand { get; }
        
        // Comandos para manejo del dropdown de almacenes origen
        public IRelayCommand AbrirDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand LimpiarSeleccionAlmacenesOrigenCommand { get; }
        
        // Comandos para manejo del dropdown de almacenes destino
        public IRelayCommand AbrirDropDownAlmacenesDestinoCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesDestinoCommand { get; }
        public IRelayCommand LimpiarSeleccionAlmacenesDestinoCommand { get; }

        public TraspasoHistoricoViewModel(TraspasosService traspasosService)
        {
            _traspasosService = traspasosService;
            _stockService = new StockService();
            _loginService = new LoginService();
            _inventarioService = new InventarioService();

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
                foreach (var empresa in SessionManager.UsuarioActual.empresas)
                {
                    Empresas.Add(empresa);
                }
            }

            // Inicializar comandos
            AbrirFiltrosCommand = new RelayCommand(AbrirFiltros);
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
            VerDetallesCommand = new RelayCommand(VerDetalles, PuedeVerDetalles);
            CambiarAAuroraSgaCommand = new RelayCommand(CambiarAAuroraSga);
            CambiarAStorageControlCommand = new RelayCommand(CambiarAStorageControl);
            AplicarFiltrosStorageControlCommand = new AsyncRelayCommand(AplicarFiltrosStorageControlAsync);
            
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
        }

        public TraspasoHistoricoViewModel() : this(new TraspasosService()) { }

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
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnCodigoLoteChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnCodigoPaletChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnFiltroObservacionesChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados (solo si tiene al menos 5 caracteres o está vacío)
            if ((_todosLosTraspasos.Any() || _todosLosAjustes.Any()) && 
                (string.IsNullOrWhiteSpace(value) || value.Trim().Length >= 5))
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnAlmacenOrigenSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnEstadoSeleccionadoChanged(EstadoTraspasoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
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

        partial void OnVerTodasLasEmpresasChanged(bool oldValue, bool newValue)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnEmpresaFiltroSeleccionadaChanged(EmpresaDto? oldValue, EmpresaDto? newValue)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados y "Ver todas las empresas" está marcado
            if (VerTodasLasEmpresas && (_todosLosTraspasos.Any() || _todosLosAjustes.Any()))
            {
                AplicarFiltrosEnMemoria();
            }
        }

        partial void OnOrigenSeleccionadoChanged(OrigenAjusteDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            // Aplicar filtros en memoria si ya hay datos cargados
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
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
                
                // Asegurar que el filtro de operarios esté vacío para mostrar todos
                FiltroOperarios = "";
                
                // Establecer "Todos" como selección por defecto en operarios
                // Usar el objeto exacto de la colección para asegurar que el binding funcione
                var todosOperario = OperariosDisponibles.FirstOrDefault(o => o.Operario == 0);
                if (todosOperario != null)
                {
                    OperarioSeleccionado = todosOperario;
                }

                // Cargar estados
                await CargarEstadosAsync();
                
                // Establecer "-- Todos los estados --" como selección por defecto
                EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado));

                // Cargar orígenes de ajustes
                await CargarOrigenesAsync();
                OrigenSeleccionado = Origenes.FirstOrDefault(o => string.IsNullOrEmpty(o.CodigoOrigen));

                // Cargar automáticamente los traspasos y ajustes del día actual
                await CargarTraspasosAsync();
                await CargarAjustesAsync();
                
                // Aplicar filtros en memoria para mostrar los datos en la UI
                AplicarFiltrosEnMemoria();
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
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] VerTodasLasEmpresas: {VerTodasLasEmpresas}, empresa pasada a API: {(empresa.HasValue ? empresa.Value.ToString() : "NULL (todas las empresas)")}");
                
                // Asegurar que las fechas estén bien configuradas (misma lógica que InventarioViewModel)
                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;
                
                System.Diagnostics.Debug.WriteLine($"Cargando traspasos desde: {fechaDesde:yyyy-MM-dd} hasta: {fechaHasta:yyyy-MM-dd}");
                
                // Si ya hay datos cargados (refresh), cargar sin filtros adicionales para obtener todos los registros
                // y luego aplicar filtros en memoria. Si es la primera carga, aplicar filtros en el API.
                var esRefresh = _todosLosTraspasos.Any();
                
                string? estadoFiltro = null;
                string? codigoPaletFiltro = null;
                string? almacenOrigenFiltro = null;
                string? almacenDestinoFiltro = null;
                
                if (!esRefresh)
                {
                    // Primera carga: aplicar filtros en el API para optimizar
                    estadoFiltro = EstadoSeleccionado?.CodigoEstado;
                if (string.IsNullOrEmpty(estadoFiltro))
                {
                    estadoFiltro = null; // No filtrar por estado
                }
                
                    codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                    ? CodigoPalet.Trim()
                    : null;
                    
                    almacenOrigenFiltro = AlmacenOrigenSeleccionado?.CodigoAlmacen;
                    almacenDestinoFiltro = AlmacenDestinoSeleccionado?.CodigoAlmacen;
                }
                // Si es refresh, todos los filtros son null, se cargarán todos los registros del rango de fechas

                var traspasos = await _traspasosService.ObtenerTraspasosFiltradosAsync(
                    estado: estadoFiltro,
                    codigoPalet: codigoPaletFiltro,
                    almacenOrigen: almacenOrigenFiltro,
                    almacenDestino: almacenDestinoFiltro,
                    fechaInicioDesde: fechaDesde.Date, // Solo la fecha, hora 00:00:00
                    fechaInicioHasta: fechaHasta.Date, // Solo la fecha, la API se encarga de incluir todo el día
                    codigoEmpresa: empresa // Filtrar por empresa seleccionada
                );

                System.Diagnostics.Debug.WriteLine($"API devolvió {traspasos.Count} traspasos");
                
                // 🔒 FILTRO DE SEGURIDAD: Aplicar filtro automático por almacenes permitidos del usuario
                // Si "Ver todas las empresas" está marcado, no filtrar por almacenes (ver todos)
                List<TraspasoDto> traspasosFiltrados;
                if (VerTodasLasEmpresas)
                {
                    // Admin viendo todas las empresas: no filtrar por almacenes
                    traspasosFiltrados = traspasos.ToList();
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
                System.Diagnostics.Debug.WriteLine($"Después del filtro de almacenes permitidos: {traspasosFiltrados.Count} traspasos");
                }
                
                // Si ya hay datos cargados, actualizar existentes y añadir nuevos (refresh)
                if (_todosLosTraspasos.Any())
                {
                    var traspasosDict = _todosLosTraspasos.ToDictionary(t => t.Id);
                    var idsDelApi = new HashSet<Guid>(traspasosFiltrados.Select(t => t.Id));
                    var traspasosActualizados = 0;
                    var traspasosNuevos = 0;
                    var traspasosEliminados = 0;
                    
                    // Actualizar existentes y añadir nuevos
                    foreach (var traspaso in traspasosFiltrados)
                    {
                        if (traspasosDict.TryGetValue(traspaso.Id, out var traspasoExistente))
                        {
                            // Solo actualizar si NO está en estado final (los completados no cambian)
                            if (!EsEstadoFinalTraspaso(traspasoExistente.CodigoEstado))
                            {
                                // Actualizar registro existente (copiar propiedades)
                                // Optimización: usar FindIndex con ID en lugar de IndexOf (más eficiente)
                                var index = _todosLosTraspasos.FindIndex(t => t.Id == traspaso.Id);
                                if (index >= 0)
                                {
                                    _todosLosTraspasos[index] = traspaso;
                                    traspasosActualizados++;
                                }
                            }
                            // Si está en estado final, no actualizar (ya es definitivo)
                        }
                        else
                        {
                            // Añadir nuevo registro (siempre añadir nuevos, incluso si están completados)
                            _todosLosTraspasos.Add(traspaso);
                            traspasosNuevos++;
                        }
                    }
                    
                    // Eliminar registros que ya no están en el rango de fechas (no vinieron del API)
                    var traspasosAEliminar = _todosLosTraspasos
                        .Where(t => !idsDelApi.Contains(t.Id) && 
                                    (t.FechaInicio.Date < fechaDesde.Date || t.FechaInicio.Date > fechaHasta.Date))
                        .ToList();
                    
                    foreach (var traspaso in traspasosAEliminar)
                    {
                        _todosLosTraspasos.Remove(traspaso);
                        traspasosEliminados++;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Refresh: {traspasosActualizados} traspasos actualizados, {traspasosNuevos} nuevos añadidos, {traspasosEliminados} eliminados (fuera de rango)");
                }
                else
                {
                    // Primera carga: guardar todos los traspasos
                    _todosLosTraspasos = traspasosFiltrados.ToList();
                    System.Diagnostics.Debug.WriteLine($"Carga inicial: {_todosLosTraspasos.Count} traspasos");
                }
                
                // NO aplicar filtros aquí - se aplicarán desde AplicarFiltrosAsync() para evitar duplicados
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
            if (!ValidarFiltroObservacionesMinimo())
                return;

            var fechaDesde = FechaDesde ?? DateTime.Today;
            var fechaHasta = FechaHasta ?? DateTime.Today;
            
            // Cuando se pulsa "Recargar" (AplicarFiltrosCommand), SIEMPRE hacer refresh del API
            // para obtener los datos más recientes (actualizar existentes y añadir nuevos)
            var fechasCambiaron = _ultimaFechaDesdeCargada != fechaDesde || _ultimaFechaHastaCargada != fechaHasta;
            
            // Si las fechas cambiaron, limpiar las colecciones para recargar todo desde cero
            if (fechasCambiaron)
            {
                _todosLosTraspasos.Clear();
                _todosLosAjustes.Clear();
            }
            
            // Recargar traspasos (siempre hacer refresh cuando se pulsa el botón)
            await CargarTraspasosAsync();
            _ultimaFechaDesdeCargada = fechaDesde;
            _ultimaFechaHastaCargada = fechaHasta;
            
            // Si la pestaña StorageControl está visible o tiene datos, también aplicar filtros ahí
            if (MostrandoStorageControl || TraspasosStorageControl.Any())
            {
                await CargarTraspasosStorageControlAsync();
            }
            
            // Recargar ajustes (siempre hacer refresh cuando se pulsa el botón)
            if (MostrandoAuroraSga || TraspasosYAjustes.Any())
            {
                await CargarAjustesAsync();
            }
            
            // Aplicar filtros en memoria una sola vez después de cargar todo
            AplicarFiltrosEnMemoria();

            // Notificar cambios en propiedades calculadas después de aplicar filtros
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            
            // Notificar que se debe hacer scroll al inicio después de recargar
            DebeHacerScrollAlInicio = true;
            // Resetear inmediatamente para que se pueda detectar el cambio la próxima vez
            DebeHacerScrollAlInicio = false;
        }

        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today; // Fecha de hoy
            FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00
            CodigoArticulo = "";
            CodigoLote = "";
            CodigoPalet = "";
            FiltroObservaciones = "";
            OperarioSeleccionado = OperariosDisponibles.FirstOrDefault(o => o.Operario == 0); // "Todos"
            AlmacenOrigenSeleccionado = null;
            AlmacenDestinoSeleccionado = null;
            EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado)); // "-- Todos los estados --"
            OrigenSeleccionado = Origenes.FirstOrDefault(o => string.IsNullOrEmpty(o.CodigoOrigen)); // "-- Todos los orígenes --"
            
            // Desmarcar "Ver todas las empresas" y reiniciar el combo
            VerTodasLasEmpresas = false;
            EmpresaFiltroSeleccionada = Empresas.FirstOrDefault(e => e.Codigo == 0); // "Todas las empresas"
            
            // Limpiar colecciones completas para forzar recarga
            _todosLosTraspasos.Clear();
            _todosLosAjustes.Clear();
            _ultimaFechaDesdeCargada = null;
            _ultimaFechaHastaCargada = null;
            
            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            
            // Limpiar las listas
            Traspasos.Clear();
            Ajustes.Clear();
            TraspasosYAjustes.Clear();
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                // Cargar operarios con CUALQUIERA de los permisos: 12 (traspasos), 13 (conteos) o 14 (inventarios)
                var operariosTraspasos = await _loginService.ObtenerOperariosConAccesoTraspasosAsync();
                var operariosConteos = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                var operariosInventarios = await _loginService.ObtenerOperariosConAccesoInventariosAsync();

                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Operarios con permiso 12 (traspasos): {operariosTraspasos.Count}");
                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Operarios con permiso 13 (conteos): {operariosConteos.Count}");
                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Operarios con permiso 14 (inventarios): {operariosInventarios.Count}");

                OperariosDisponibles.Clear();

                // Agregar opción "Todos" como primera opción
                OperariosDisponibles.Add(new OperariosAccesoDto 
                { 
                    Operario = 0, 
                    NombreOperario = "Todos",
                    MRH_CodigoAplicacion = 0
                });

                // Combinar todas las listas y eliminar duplicados (OR: si tiene cualquiera de los permisos, aparece)
                var todosOperarios = operariosTraspasos
                    .Concat(operariosConteos)
                    .Concat(operariosInventarios)
                    .GroupBy(o => o.Operario)
                    .Select(g => g.First())
                    .OrderBy(o => o.NombreOperario)
                    .ToList();

                foreach (var operario in todosOperarios)
                {
                    OperariosDisponibles.Add(operario);
                }

                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Total operarios cargados (permisos 12, 13 o 14): {OperariosDisponibles.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // Fallback: intentar solo con traspasos
                try
                {
                    var operariosFallback = await _loginService.ObtenerOperariosConAccesoTraspasosAsync();
                    OperariosDisponibles.Clear();
                    // Agregar opción "Todos" como primera opción
                    OperariosDisponibles.Add(new OperariosAccesoDto 
                    { 
                        Operario = 0, 
                        NombreOperario = "Todos",
                        MRH_CodigoAplicacion = 0
                    });
                    foreach (var operario in operariosFallback.OrderBy(o => o.NombreOperario))
                    {
                        OperariosDisponibles.Add(operario);
                    }
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Fallback: {OperariosDisponibles.Count} operarios cargados");
                }
                catch
                {
                    OperariosDisponibles.Clear();
                    // Aún así, agregar la opción "Todos"
                    OperariosDisponibles.Add(new OperariosAccesoDto 
                    { 
                        Operario = 0, 
                        NombreOperario = "Todos",
                        MRH_CodigoAplicacion = 0
                    });
                    System.Diagnostics.Debug.WriteLine("[TraspasoHistorico] Error total: No se pudieron cargar operarios");
                }
            }
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

        private void AbrirFiltros()
        {
            // Asegurar que "Todos" esté seleccionado por defecto en operarios al abrir el diálogo
            if (OperarioSeleccionado == null || OperarioSeleccionado.Operario != 0)
            {
                var todosOperario = OperariosDisponibles.FirstOrDefault(o => o.Operario == 0);
                if (todosOperario != null)
                {
                    OperarioSeleccionado = todosOperario;
                    // Establecer el texto del ComboBox para que muestre "Todos" (el ComboBox es editable)
                    FiltroOperarios = todosOperario.NombreOperario ?? "Todos";
                }
            }
            else if (OperarioSeleccionado.Operario == 0)
            {
                // Si ya está seleccionado "Todos", asegurar que el texto también muestre "Todos"
                FiltroOperarios = OperarioSeleccionado.NombreOperario ?? "Todos";
            }
            
            // Usar el ViewModel principal directamente como DataContext para que los cambios se reflejen automáticamente
            var dlg = new HistorialTraspasosFiltrosDialog
            {
                DataContext = this
            };
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow;
            if (owner != null && owner != dlg)
                dlg.Owner = owner;
            dlg.ShowDialog();
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
            // Limpiar el filtro cada vez que se despliega el combo para mostrar todos los operarios
            FiltroOperarios = "";
            IsDropDownOpenOperarios = true;
        }

        [RelayCommand]
        private void CerrarDropDownOperarios()
        {
            IsDropDownOpenOperarios = false;
            
            // Cuando se cierra el dropdown, si hay un operario seleccionado, mostrar su nombre en el texto
            if (OperarioSeleccionado != null)
            {
                FiltroOperarios = OperarioSeleccionado.NombreOperario ?? "";
            }
        }

        // Método de filtrado para operarios (búsqueda en cualquier parte del texto)
        private bool FiltraOperario(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            
            // "Todos" siempre se muestra (Operario = 0)
            if (operario.Operario == 0) return true;
            
            if (string.IsNullOrWhiteSpace(FiltroOperarios)) return true;

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
        private async Task<List<string>> ObtenerAlmacenesPermitidosAsync(bool forzarRefresh = false)
        {
            // Si hay cache válido y no se fuerza refresh, devolver cache
            if (!forzarRefresh && _almacenesPermitidosCache != null && 
                _ultimaActualizacionAlmacenes.HasValue &&
                (DateTime.Now - _ultimaActualizacionAlmacenes.Value).TotalMinutes < 5)
            {
                System.Diagnostics.Debug.WriteLine("[Cache] Devolviendo almacenes permitidos desde cache");
                return _almacenesPermitidosCache;
            }
            
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
                
                // Guardar en cache
                _almacenesPermitidosCache = almacenesAutorizados.Select(a => a.CodigoAlmacen).ToList();
                _ultimaActualizacionAlmacenes = DateTime.Now;
                
                System.Diagnostics.Debug.WriteLine($"[Cache] Almacenes permitidos actualizados: {_almacenesPermitidosCache.Count} almacenes");
                
                return _almacenesPermitidosCache;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo almacenes permitidos: {ex.Message}");
                // Si hay error pero tenemos cache, devolver cache
                return _almacenesPermitidosCache ?? new List<string>();
            }
        }

        // Comandos para cambiar de pestaña
        private void CambiarAAuroraSga()
        {
            MostrandoAuroraSga = true;
            MostrandoStorageControl = false;
            
            // Si ya hay datos cargados, aplicar filtros en memoria
            if (_todosLosTraspasos.Any() || _todosLosAjustes.Any())
            {
                AplicarFiltrosEnMemoria();
            }
            else
            {
                // Cargar datos si no hay nada cargado
                _ = CargarTraspasosAsync();
                _ = CargarAjustesAsync();
            }
        }

        private void CambiarAStorageControl()
        {
            MostrandoAuroraSga = false;
            MostrandoStorageControl = true;
            
            // Cargar traspasos de StorageControl si la colección está vacía
            if (!TraspasosStorageControl.Any())
            {
                _ = CargarTraspasosStorageControlAsync();
            }
        }
        
        /// <summary>
        /// Método público para activar la pestaña de Aurora SGA desde fuera (por ejemplo, desde navegación)
        /// </summary>
        public void ActivarPestañaAjustes()
        {
            CambiarAAuroraSga();
        }
        
        /// <summary>
        /// Aplica todos los filtros en memoria sobre las colecciones completas cargadas
        /// </summary>
        private void AplicarFiltrosEnMemoria()
        {
            // Protección: evitar ejecuciones simultáneas (si ya se está aplicando, salir)
            if (_aplicandoFiltros)
            {
                System.Diagnostics.Debug.WriteLine("[Filtros] AplicarFiltrosEnMemoria ya en ejecución, ignorando llamada duplicada");
                return;
            }
            
            _aplicandoFiltros = true;
            try
            {
                // ========== FILTRAR TRASPASOS ==========
                var traspasosFiltrados = _todosLosTraspasos.AsEnumerable();
            
            // Filtro por empresa (si "Ver todas las empresas" está marcado y hay una empresa seleccionada que no sea "Todas")
            if (VerTodasLasEmpresas && EmpresaFiltroSeleccionada != null && EmpresaFiltroSeleccionada.Codigo != 0)
            {
                traspasosFiltrados = traspasosFiltrados.Where(t => t.CodigoEmpresa == EmpresaFiltroSeleccionada.Codigo);
            }
            
            // Filtro por estado
            var estadoFiltro = EstadoSeleccionado?.CodigoEstado;
            if (!string.IsNullOrEmpty(estadoFiltro))
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    !string.IsNullOrWhiteSpace(t.CodigoEstado) &&
                    t.CodigoEstado.Equals(estadoFiltro, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por código de artículo
            if (!string.IsNullOrWhiteSpace(CodigoArticulo))
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    !string.IsNullOrWhiteSpace(t.CodigoArticulo) &&
                    t.CodigoArticulo.Contains(CodigoArticulo, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por lote (solo si hay artículo)
            if (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    (!string.IsNullOrWhiteSpace(t.Partida) &&
                    t.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)) ||
                    (t.LineasPalet != null && t.LineasPalet.Any(l =>
                        !string.IsNullOrWhiteSpace(l.Lote) &&
                        l.Lote.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)))
                );
            }
            
            // Filtro por palet
            var codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                ? CodigoPalet.Trim()
                : null;
            if (codigoPaletFiltro != null)
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    !string.IsNullOrWhiteSpace(t.CodigoPalet) &&
                    t.CodigoPalet.Contains(codigoPaletFiltro, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por observaciones
            var observacionesFiltro = !string.IsNullOrWhiteSpace(FiltroObservaciones) && FiltroObservaciones.Trim().Length >= 5
                ? FiltroObservaciones.Trim()
                : null;
            if (observacionesFiltro != null)
            {
                var observacionesFiltroNormalizado = NormalizarIdentificadorOrden(observacionesFiltro);
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                {
                    bool coincideComentario = !string.IsNullOrWhiteSpace(t.Comentarios) &&
                        (t.Comentarios.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                         NormalizarIdentificadorOrden(t.Comentarios).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase));
                    bool coincideOrdenTrabajo = !string.IsNullOrWhiteSpace(t.OrdenTrabajoId) &&
                        (t.OrdenTrabajoId.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                         NormalizarIdentificadorOrden(t.OrdenTrabajoId).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase));
                    return coincideComentario || coincideOrdenTrabajo;
                });
            }
            
            // Filtro por almacén origen
            if (AlmacenOrigenSeleccionado != null && !string.IsNullOrWhiteSpace(AlmacenOrigenSeleccionado.CodigoAlmacen))
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    !string.IsNullOrWhiteSpace(t.AlmacenOrigen) &&
                    t.AlmacenOrigen.Equals(AlmacenOrigenSeleccionado.CodigoAlmacen, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por almacén destino
            if (AlmacenDestinoSeleccionado != null && !string.IsNullOrWhiteSpace(AlmacenDestinoSeleccionado.CodigoAlmacen))
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    !string.IsNullOrWhiteSpace(t.AlmacenDestino) &&
                    t.AlmacenDestino.Equals(AlmacenDestinoSeleccionado.CodigoAlmacen, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por operario
            if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
            {
                traspasosFiltrados = traspasosFiltrados.Where(t =>
                    t.UsuarioInicioId == OperarioSeleccionado.Operario
                );
            }
            
            // Resolver nombres de operarios y actualizar colección de traspasos
            var operariosDict = OperariosDisponibles.ToDictionary(o => o.Operario.ToString(), o => ExtraerSoloNombre(o.NombreCompleto ?? "Sin nombre"));
            Traspasos.Clear();
            foreach (var traspaso in traspasosFiltrados.OrderByDescending(t => t.FechaInicio))
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
            }
            
            // ========== FILTRAR AJUSTES ==========
            var ajustesFiltrados = _todosLosAjustes.AsEnumerable();
            
            // Filtro por empresa (si "Ver todas las empresas" está marcado y hay una empresa seleccionada que no sea "Todas")
            if (VerTodasLasEmpresas && EmpresaFiltroSeleccionada != null && EmpresaFiltroSeleccionada.Codigo != 0)
            {
                ajustesFiltrados = ajustesFiltrados.Where(a => a.CodigoEmpresa == EmpresaFiltroSeleccionada.Codigo);
            }
            
            // Filtro por estado
            if (!string.IsNullOrEmpty(estadoFiltro))
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    !string.IsNullOrWhiteSpace(a.Estado) &&
                    a.Estado.Equals(estadoFiltro, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por código de artículo
            if (!string.IsNullOrWhiteSpace(CodigoArticulo))
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    !string.IsNullOrWhiteSpace(a.CodigoArticulo) &&
                    a.CodigoArticulo.Contains(CodigoArticulo, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por lote (solo si hay artículo)
            if (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    !string.IsNullOrWhiteSpace(a.Partida) &&
                    a.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por palet
            if (codigoPaletFiltro != null)
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    !string.IsNullOrWhiteSpace(a.CodigoPalet) &&
                    a.CodigoPalet.Contains(codigoPaletFiltro, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por observaciones (buscar en CodigoInventario y CodigoConteo)
            if (observacionesFiltro != null)
            {
                var observacionesFiltroNormalizado = NormalizarIdentificadorOrden(observacionesFiltro);
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                {
                    bool coincideInventario = !string.IsNullOrWhiteSpace(a.CodigoInventario) &&
                        (a.CodigoInventario.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                         NormalizarIdentificadorOrden(a.CodigoInventario).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase));
                    bool coincideConteo = !string.IsNullOrWhiteSpace(a.CodigoConteo) &&
                        (a.CodigoConteo.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                         NormalizarIdentificadorOrden(a.CodigoConteo).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase));
                    return coincideInventario || coincideConteo;
                });
            }
            
            // Filtro por almacén (origen o destino, para ajustes solo hay uno)
            var almacenFiltro = AlmacenOrigenSeleccionado?.CodigoAlmacen ?? AlmacenDestinoSeleccionado?.CodigoAlmacen;
            if (!string.IsNullOrWhiteSpace(almacenFiltro))
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    !string.IsNullOrWhiteSpace(a.CodigoAlmacen) &&
                    a.CodigoAlmacen.Equals(almacenFiltro, StringComparison.OrdinalIgnoreCase)
                );
            }
            
            // Filtro por operario
            if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    a.UsuarioId == OperarioSeleccionado.Operario
                );
            }
            
            // Filtro por origen (Inventario, Conteo)
            var origenFiltro = OrigenSeleccionado?.CodigoOrigen;
            if (!string.IsNullOrEmpty(origenFiltro) && origenFiltro != "TRASPASO")
            {
                ajustesFiltrados = ajustesFiltrados.Where(a =>
                    a.Origen == origenFiltro
                );
            }
            
            // Actualizar colección de ajustes
            Ajustes.Clear();
            foreach (var ajuste in ajustesFiltrados.OrderByDescending(a => a.Fecha))
            {
                Ajustes.Add(ajuste);
            }
            
            // Actualizar lista combinada
            ActualizarTraspasosYAjustes();
            }
            finally
            {
                _aplicandoFiltros = false;
            }
        }
        
        private void ActualizarTraspasosYAjustes()
        {
            TraspasosYAjustes.Clear();
            
            // Aplicar filtro de origen si está seleccionado
            var origenFiltro = OrigenSeleccionado?.CodigoOrigen;
            var mostrarTraspasos = string.IsNullOrEmpty(origenFiltro) || origenFiltro == "TRASPASO";
            var mostrarAjustes = string.IsNullOrEmpty(origenFiltro) || origenFiltro == "INVENTARIO" || origenFiltro == "CONTEO";
            
            // Agregar traspasos de SGA Actual (si corresponde)
            if (mostrarTraspasos)
            {
            foreach (var traspaso in Traspasos)
            {
                traspaso.Fuente = "SGA_Actual";
                    TraspasosYAjustes.Add(traspaso);
                }
            }
            
            // Agregar ajustes (si corresponde)
            // Los ajustes ya vienen filtrados por origen desde AplicarFiltrosEnMemoria()
            if (mostrarAjustes)
            {
                foreach (var ajuste in Ajustes)
                {
                    TraspasosYAjustes.Add(ajuste);
                }
            }
            
            // Ordenar por fecha descendente (más recientes primero) - optimizado
            var itemsOrdenados = TraspasosYAjustes
                .OrderByDescending(t => 
                {
                    if (t is TraspasoDto traspasoSga)
                        return traspasoSga.FechaInicio;
                    if (t is AjusteDto ajuste)
                        return ajuste.Fecha;
                    return DateTime.MinValue;
                })
                .ToList();
            
            // Reemplazar la colección de una vez en lugar de limpiar y añadir uno por uno
            TraspasosYAjustes.Clear();
            foreach (var item in itemsOrdenados)
            {
                TraspasosYAjustes.Add(item);
            }
        }

        private async Task CargarTraspasosStorageControlAsync()
        {
            try
            {
                EstaCargando = true;
                
                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;
                
                var codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                    ? CodigoPalet.Trim()
                    : null;
                var observacionesFiltro = !string.IsNullOrWhiteSpace(FiltroObservaciones) && FiltroObservaciones.Trim().Length >= 5
                    ? FiltroObservaciones.Trim()
                    : null;

                var partidaFiltro = (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
                    ? CodigoLote
                    : null;

                // Si "Ver todas las empresas" está marcado, pasar null para ver todas
                var empresa = VerTodasLasEmpresas ? null : SessionManager.EmpresaSeleccionada;
                var traspasos = await _traspasosService.ObtenerTraspasosStorageControlAsync(
                    fechaDesde: fechaDesde.Date,
                    fechaHasta: fechaHasta.Date,
                    almacenOrigen: AlmacenOrigenSeleccionado?.CodigoAlmacen,
                    almacenDestino: AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    codigoArticulo: string.IsNullOrWhiteSpace(CodigoArticulo) ? null : CodigoArticulo,
                    partida: partidaFiltro,
                    codigoEmpresa: empresa // Filtrar por empresa seleccionada
                );

                TraspasosStorageControl.Clear();
                
                // 🔒 FILTRO DE SEGURIDAD: Aplicar filtro automático por almacenes permitidos del usuario
                // Si "Ver todas las empresas" está marcado, no filtrar por almacenes (ver todos)
                List<TraspasoStorageControlDto> traspasosFiltrados;
                if (VerTodasLasEmpresas)
                {
                    // Admin viendo todas las empresas: no filtrar por almacenes
                    traspasosFiltrados = traspasos.ToList();
                    System.Diagnostics.Debug.WriteLine($"[StorageControl Admin] Viendo todas las empresas: {traspasosFiltrados.Count} traspasos sin filtrar por almacenes");
                }
                else
                {
                    // Usuario normal: filtrar por almacenes permitidos de su empresa
                var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Traspasos recibidos del API: {traspasos.Count}");
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Almacenes permitidos: {string.Join(", ", almacenesPermitidos)}");
                
                    traspasosFiltrados = traspasos.Where(t => 
                    (t.AlmacenOrigen != null && almacenesPermitidos.Contains(t.AlmacenOrigen)) || 
                    (t.AlmacenDestino != null && almacenesPermitidos.Contains(t.AlmacenDestino))
                ).ToList();
                
                var traspasosDescartados = traspasos.Count - traspasosFiltrados.Count;
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Traspasos después del filtro de almacenes: {traspasosFiltrados.Count}");
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Traspasos descartados por filtro de almacenes: {traspasosDescartados}");
                }

                if (codigoPaletFiltro != null)
                {
                    traspasosFiltrados = traspasosFiltrados.Where(t =>
                        !string.IsNullOrWhiteSpace(t.CodigoPalet) &&
                        t.CodigoPalet.Contains(codigoPaletFiltro, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Después del filtro de palet: {traspasosFiltrados.Count}");
                }
                else if (!string.IsNullOrWhiteSpace(CodigoPalet))
                {
                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Filtro de palet ignorado: se necesitan al menos 3 caracteres (actual: {CodigoPalet.Trim().Length})");
                }

                if (observacionesFiltro != null)
                {
                    var observacionesFiltroNormalizado = NormalizarIdentificadorOrden(observacionesFiltro);

                    traspasosFiltrados = traspasosFiltrados.Where(t =>
                        !string.IsNullOrWhiteSpace(t.Comentario) &&
                        (t.Comentario.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                         NormalizarIdentificadorOrden(t.Comentario).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Después del filtro de observaciones: {traspasosFiltrados.Count}");
                }
                else if (!string.IsNullOrWhiteSpace(FiltroObservaciones))
                {
                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Filtro de observaciones ignorado: se necesitan al menos 5 caracteres (actual: {FiltroObservaciones.Trim().Length})");
                }

                // Filtro por lote (solo si hay artículo escrito y el API no lo filtró)
                if (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    traspasosFiltrados = traspasosFiltrados.Where(t =>
                        !string.IsNullOrWhiteSpace(t.Partida) &&
                        t.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Después del filtro de lote: {traspasosFiltrados.Count}");
                }

                foreach (var traspaso in traspasosFiltrados.OrderByDescending(t => t.FechaRegistro ?? t.Fecha))
                {
                    TraspasosStorageControl.Add(traspaso);
                }
                
                // Si la pestaña Aurora SGA está visible, actualizar la lista combinada
                if (MostrandoAuroraSga)
                {
                    ActualizarTraspasosYAjustes();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando traspasos StorageControl: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task AplicarFiltrosStorageControlAsync()
        {
            if (!ValidarFiltroObservacionesMinimo())
                return;

            await CargarTraspasosStorageControlAsync();
        }

        private bool ValidarFiltroObservacionesMinimo()
        {
            if (!string.IsNullOrWhiteSpace(FiltroObservaciones) && FiltroObservaciones.Trim().Length < 5)
            {
                MostrarWarningDialog("Filtro de observaciones", "Introduce al menos 5 caracteres para filtrar por observaciones.");
                return false;
            }

            return true;
        }

        private void MostrarWarningDialog(string title, string message, string iconGlyph = "\uE7BA")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new WarningDialog(title, message, iconGlyph);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                {
                    dialog.Owner = owner;
                }
                dialog.ShowDialog();
            });
        }

        private static string NormalizarIdentificadorOrden(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return string.Empty;

            // Primero quitar acentos y tildes
            var textoNormalizado = valor.Normalize(System.Text.NormalizationForm.FormD);
            var sinAcentos = new System.Text.StringBuilder();
            
            foreach (char c in textoNormalizado)
            {
                var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                // Mantener solo caracteres que no sean marcas diacríticas (tildes, acentos)
                if (categoria != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sinAcentos.Append(c);
                }
            }

            // Luego quitar signos de puntuación y espacios, mantener solo letras y dígitos
            var builder = new System.Text.StringBuilder(sinAcentos.Length);
            foreach (var ch in sinAcentos.ToString())
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToUpperInvariant(ch));
            }
            return builder.ToString();
        }

        // Comandos para copiar datos al portapapeles
        [RelayCommand]
        private void CopiarCodigo(string codigo)
        {
            if (!string.IsNullOrWhiteSpace(codigo))
                Clipboard.SetText(codigo);
        }

        [RelayCommand]
        private void CopiarDescripcion(string descripcion)
        {
            if (!string.IsNullOrWhiteSpace(descripcion))
                Clipboard.SetText(descripcion);
        }

        [RelayCommand]
        private void CopiarLote(string lote)
        {
            if (!string.IsNullOrWhiteSpace(lote))
                Clipboard.SetText(lote);
        }

        [RelayCommand]
        private void CopiarFechaCaducidad(DateTime? fechaCaducidad)
        {
            if (fechaCaducidad.HasValue)
                Clipboard.SetText(fechaCaducidad.Value.ToString("dd/MM/yyyy"));
        }

        [RelayCommand]
        private void CopiarAlmacenOrigen(string almacen)
        {
            if (!string.IsNullOrWhiteSpace(almacen))
                Clipboard.SetText(almacen);
        }

        [RelayCommand]
        private void CopiarUbicacionOrigen(string ubicacion)
        {
            if (!string.IsNullOrWhiteSpace(ubicacion))
                Clipboard.SetText(ubicacion);
        }

        [RelayCommand]
        private void CopiarAlmacenDestino(string almacen)
        {
            if (!string.IsNullOrWhiteSpace(almacen))
                Clipboard.SetText(almacen);
        }

        [RelayCommand]
        private void CopiarUbicacionDestino(string ubicacion)
        {
            if (!string.IsNullOrWhiteSpace(ubicacion))
                Clipboard.SetText(ubicacion);
        }

        [RelayCommand]
        private void CopiarCodigoPalet(string codigoPalet)
        {
            if (!string.IsNullOrWhiteSpace(codigoPalet))
                Clipboard.SetText(codigoPalet);
        }

        [RelayCommand]
        private void CopiarCantidad(decimal? cantidad)
        {
            if (cantidad.HasValue)
                Clipboard.SetText(cantidad.Value.ToString("0.########"));
        }

        [RelayCommand]
        private void CopiarUsuario(string usuario)
        {
            if (!string.IsNullOrWhiteSpace(usuario))
                Clipboard.SetText(usuario);
        }

        [RelayCommand]
        private void CopiarComentarios(string comentarios)
        {
            if (!string.IsNullOrWhiteSpace(comentarios))
                Clipboard.SetText(comentarios);
        }

        [RelayCommand]
        private void CopiarTipoTraspaso(string tipoTraspaso)
        {
            if (!string.IsNullOrWhiteSpace(tipoTraspaso))
                Clipboard.SetText(tipoTraspaso);
        }

        [RelayCommand]
        private void CopiarEstado(string estado)
        {
            if (!string.IsNullOrWhiteSpace(estado))
                Clipboard.SetText(estado);
        }

        [RelayCommand]
        private void CopiarFecha(DateTime? fecha)
        {
            if (fecha.HasValue)
                Clipboard.SetText(fecha.Value.ToString("dd/MM/yyyy HH:mm"));
        }

        [RelayCommand]
        private void CopiarDiferencia(object diferencia)
        {
            if (diferencia is decimal dec)
                Clipboard.SetText(dec.ToString("0.######"));
            else if (diferencia != null && decimal.TryParse(diferencia.ToString(), out var parsed))
                Clipboard.SetText(parsed.ToString("0.######"));
        }

        [RelayCommand]
        private void CopiarAlmacen(string almacen)
        {
            if (!string.IsNullOrWhiteSpace(almacen))
                Clipboard.SetText(almacen);
        }

        [RelayCommand]
        private void CopiarUbicacion(string ubicacion)
        {
            if (!string.IsNullOrWhiteSpace(ubicacion))
                Clipboard.SetText(ubicacion);
        }

        [RelayCommand]
        private void CopiarCodigoInventario(string codigoInventario)
        {
            if (!string.IsNullOrWhiteSpace(codigoInventario))
                Clipboard.SetText(codigoInventario);
        }

        // Métodos para cargar ajustes
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

        private async Task CargarAjustesAsync()
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

                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;

                // Si ya hay datos cargados (refresh), cargar sin filtros adicionales para obtener todos los registros
                // y luego aplicar filtros en memoria. Si es la primera carga, aplicar filtros en el API.
                var esRefresh = _todosLosAjustes.Any();
                
                string? estadoFiltro = null;
                string? codigoArticuloFiltro = null;
                string? almacenFiltro = null;
                int? usuarioIdFiltro = null;
                string? partidaFiltro = null;
                string? codigoPaletFiltro = null;
                
                if (!esRefresh)
                {
                    // Primera carga: aplicar filtros en el API para optimizar
                    estadoFiltro = EstadoSeleccionado?.CodigoEstado;
                    if (string.IsNullOrEmpty(estadoFiltro))
                    {
                        estadoFiltro = null;
                    }

                    codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                        ? CodigoPalet.Trim()
                        : null;

                    // Usar AlmacenOrigenSeleccionado o AlmacenDestinoSeleccionado para ajustes (solo hay un almacén)
                    almacenFiltro = AlmacenOrigenSeleccionado?.CodigoAlmacen ?? AlmacenDestinoSeleccionado?.CodigoAlmacen;
                    codigoArticuloFiltro = string.IsNullOrWhiteSpace(CodigoArticulo) ? null : CodigoArticulo;
                    usuarioIdFiltro = OperarioSeleccionado?.Operario > 0 ? OperarioSeleccionado.Operario : null;
                    partidaFiltro = (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo)) ? CodigoLote : null;
                }
                // Si es refresh, todos los filtros son null, se cargarán todos los registros del rango de fechas

                var ajustes = await _inventarioService.ObtenerAjustesFiltradosAsync(
                    codigoEmpresa: empresa,
                    fechaDesde: fechaDesde.Date,
                    fechaHasta: fechaHasta.Date,
                    codigoArticulo: codigoArticuloFiltro,
                    codigoAlmacen: almacenFiltro,
                    estado: estadoFiltro,
                    usuarioId: usuarioIdFiltro,
                    partida: partidaFiltro,
                    codigoPalet: codigoPaletFiltro
                );

                // Filtrar por almacenes permitidos del usuario
                // Si "Ver todas las empresas" está marcado, no filtrar por almacenes (ver todos)
                List<AjusteDto> ajustesFiltradosPorSeguridad;
                if (VerTodasLasEmpresas)
                {
                    // Admin viendo todas las empresas: no filtrar por almacenes
                    ajustesFiltradosPorSeguridad = ajustes.ToList();
                    System.Diagnostics.Debug.WriteLine($"[Admin] Viendo todas las empresas: {ajustesFiltradosPorSeguridad.Count} ajustes sin filtrar por almacenes");
                }
                else
                {
                    // Usuario normal: filtrar por almacenes permitidos de su empresa
                var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                    ajustesFiltradosPorSeguridad = ajustes.Where(a =>
                    almacenesPermitidos.Contains(a.CodigoAlmacen)
                ).ToList();
                System.Diagnostics.Debug.WriteLine($"Después del filtro de almacenes permitidos: {ajustesFiltradosPorSeguridad.Count} ajustes");
                }
                
                // Si ya hay datos cargados, actualizar existentes y añadir nuevos (refresh)
                if (_todosLosAjustes.Any())
                {
                    var ajustesDict = _todosLosAjustes.ToDictionary(a => a.IdAjuste);
                    var idsDelApi = new HashSet<Guid>(ajustesFiltradosPorSeguridad.Select(a => a.IdAjuste));
                    var ajustesActualizados = 0;
                    var ajustesNuevos = 0;
                    var ajustesEliminados = 0;
                    
                    // Actualizar existentes y añadir nuevos
                    foreach (var ajuste in ajustesFiltradosPorSeguridad)
                    {
                        if (ajustesDict.TryGetValue(ajuste.IdAjuste, out var ajusteExistente))
                        {
                            // Solo actualizar si NO está en estado final (los completados no cambian)
                            if (!EsEstadoFinalAjuste(ajusteExistente.Estado))
                            {
                                // Actualizar registro existente (copiar propiedades)
                                // Optimización: usar FindIndex con ID en lugar de IndexOf (más eficiente)
                                var index = _todosLosAjustes.FindIndex(a => a.IdAjuste == ajuste.IdAjuste);
                                if (index >= 0)
                                {
                                    _todosLosAjustes[index] = ajuste;
                                    ajustesActualizados++;
                                }
                            }
                            // Si está en estado final, no actualizar (ya es definitivo)
                        }
                        else
                        {
                            // Añadir nuevo registro (siempre añadir nuevos, incluso si están completados)
                            _todosLosAjustes.Add(ajuste);
                            ajustesNuevos++;
                        }
                    }
                    
                    // Eliminar registros que ya no están en el rango de fechas (no vinieron del API)
                    var ajustesAEliminar = _todosLosAjustes
                        .Where(a => !idsDelApi.Contains(a.IdAjuste) && 
                                    (a.Fecha.Date < fechaDesde.Date || a.Fecha.Date > fechaHasta.Date))
                        .ToList();
                    
                    foreach (var ajuste in ajustesAEliminar)
                    {
                        _todosLosAjustes.Remove(ajuste);
                        ajustesEliminados++;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Refresh: {ajustesActualizados} ajustes actualizados, {ajustesNuevos} nuevos añadidos, {ajustesEliminados} eliminados (fuera de rango)");
                }
                else
                {
                    // Primera carga: guardar todos los ajustes
                    _todosLosAjustes = ajustesFiltradosPorSeguridad.ToList();
                    System.Diagnostics.Debug.WriteLine($"Carga inicial: {_todosLosAjustes.Count} ajustes");
                }
                
                // NO aplicar filtros aquí - se aplicarán desde AplicarFiltrosAsync() para evitar duplicados
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando ajustes: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
            }
        }
    }
}

