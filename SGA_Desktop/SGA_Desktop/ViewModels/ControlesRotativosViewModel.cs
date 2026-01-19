using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SGA_Desktop.ViewModels
{
    public partial class ControlesRotativosViewModel : ObservableObject
    {
        #region Constants
        private const string TODOS = "Todos";
        #endregion

        #region Fields & Services
        private readonly ConteosService _conteosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;
        #endregion

        #region Constructor
        public ControlesRotativosViewModel(ConteosService conteosService, StockService stockService, LoginService loginService)
        {
            _conteosService = conteosService;
            _stockService = stockService;
            _loginService = loginService;
            EmpresaActual = ObtenerNombreEmpresaActual();
            AlmacenesCombo = new ObservableCollection<AlmacenDto>();
            OperariosCombo = new ObservableCollection<OperariosAccesoDto>();
            OrdenesConteo = new ObservableCollection<OrdenConteoDto>();
            ResultadosSupervision = new ObservableCollection<ResultadoConteoDetalladoDto>();
            OperariosDisponibles = new ObservableCollection<OperariosAccesoDto>();
            ConteosPeriodicos = new ObservableCollection<ConteoPeriodicoDto>();

            OrdenesConteoView = CollectionViewSource.GetDefaultView(OrdenesConteo);
            OrdenesConteoView.Filter = new Predicate<object>(FiltroOrden);

            ResultadosView = CollectionViewSource.GetDefaultView(ResultadosSupervision);
            ResultadosView.Filter = new Predicate<object>(FiltroResultado);
            
            // Inicializar vistas para autocompletado de operarios
            OperariosComboView = CollectionViewSource.GetDefaultView(OperariosCombo);
            OperariosComboView.Filter = FiltraOperarioCombo;
            
            OperariosDisponiblesView = CollectionViewSource.GetDefaultView(OperariosDisponibles);
            OperariosDisponiblesView.Filter = FiltraOperarioDisponibles;
            
            // Inicializar vista para autocompletado de almacenes
            AlmacenesComboView = CollectionViewSource.GetDefaultView(AlmacenesCombo);
            AlmacenesComboView.Filter = FiltraAlmacenes;

            // Inicializar vistas para autocompletado de supervisión
            AlmacenesSupervisionComboView = CollectionViewSource.GetDefaultView(AlmacenesSupervisionCombo);
            AlmacenesSupervisionComboView.Filter = FiltraAlmacenesSupervisionCombo;
            
            UbicacionesSupervisionComboView = CollectionViewSource.GetDefaultView(UbicacionesSupervisionCombo);
            UbicacionesSupervisionComboView.Filter = FiltraUbicacionesSupervisionCombo;
            
            OperariosSupervisionComboView = CollectionViewSource.GetDefaultView(OperariosSupervisionCombo);
            OperariosSupervisionComboView.Filter = FiltraOperariosSupervisionCombo;

            EstadosCombo = new ObservableCollection<string>
            {
                "TODOS",
                "PLANIFICADO", 
                "ASIGNADO",
                "EN_PROCESO",
                "PENDIENTE_REVISION",
                "CERRADO",
                "CANCELADO"
            };

            EstadoFiltro = "TODOS";
            ModoVisualizacion = "ORDENES"; // Por defecto mostrar órdenes

            // Suscribirse a solicitudes de filtro
            ConteoFiltroStore.FiltroSolicitado += OnFiltroSolicitado;

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ControlesRotativosViewModel() : this(new ConteosService(), new StockService(), new LoginService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private string empresaActual;

        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
        public ObservableCollection<OperariosAccesoDto> OperariosCombo { get; }
        public ObservableCollection<OrdenConteoDto> OrdenesConteo { get; }
        public ObservableCollection<string> EstadosCombo { get; }
        public ObservableCollection<ResultadoConteoDetalladoDto> ResultadosSupervision { get; }
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; }
        public ObservableCollection<ConteoPeriodicoDto> ConteosPeriodicos { get; }
        public ICollectionView ResultadosView { get; }
        
        // Propiedades para autocompletado de operarios
        public ICollectionView OperariosComboView { get; private set; }
        public ICollectionView OperariosDisponiblesView { get; private set; }
        
        // Propiedades para autocompletado de almacenes
        public ICollectionView AlmacenesComboView { get; private set; }

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionadoCombo;

        partial void OnAlmacenSeleccionadoComboChanged(AlmacenDto? value)
        {
            // Recargar automáticamente cuando cambie el almacén seleccionado
            if (value != null && !IsCargando)
            {
                _ = CargarControles();
            }
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
            OnPropertyChanged(nameof(CanCargarControles));
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        [ObservableProperty]
        private OperariosAccesoDto? operarioSeleccionadoCombo;

        partial void OnOperarioSeleccionadoComboChanged(OperariosAccesoDto? value)
        {
            // Recargar automáticamente cuando cambie el operario seleccionado
            if (value != null && !IsCargando)
            {
                _ = CargarControles();
            }
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        [ObservableProperty]
        private OrdenConteoDto? ordenSeleccionada;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private DateTime fechaDesde = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime fechaHasta = DateTime.Today;

        [ObservableProperty]
        private string estadoFiltro = "TODOS";

        [ObservableProperty]
        private string idOrdenFiltro = string.Empty;

        [ObservableProperty]
        private bool verTodosLosConteos = false; // Por defecto, solo ver los propios
        
        [ObservableProperty]
        private OperariosAccesoDto? operarioCreadorSeleccionadoCombo;

        partial void OnIdOrdenFiltroChanged(string value)
        {
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnVerTodosLosConteosChanged(bool value)
        {
            // Cuando cambia VerTodosLosConteos, recargar los controles
            if (!IsCargando)
            {
                _ = CargarControles();
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnEstadoFiltroChanged(string value)
        {
            // Recargar automáticamente cuando cambie el estado del filtro
            if (!IsCargando)
            {
                _ = CargarControles();
            }
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        // Propiedades para supervisión
        [ObservableProperty]
        private string modoVisualizacion = "ORDENES"; // "ORDENES" o "SUPERVISION"

        [ObservableProperty]
        private ResultadoConteoDetalladoDto? resultadoSeleccionado;

        [ObservableProperty]
        private OperariosAccesoDto? operarioAprobadorSeleccionado;

        // Propiedades para filtros de supervisión
        [ObservableProperty]
        private bool verTodosLosResultadosSupervision = false; // Por defecto, solo ver los propios
        
        [ObservableProperty]
        private DateTime? fechaDesdeSupervision;
        
        [ObservableProperty]
        private DateTime? fechaHastaSupervision;
        
        [ObservableProperty]
        private decimal? diferenciaDesdeSupervision;
        
        [ObservableProperty]
        private decimal? diferenciaHastaSupervision;
        
        [ObservableProperty]
        private string? almacenSupervisionSeleccionado;
        
        [ObservableProperty]
        private string? ubicacionSupervisionSeleccionada;
        
        [ObservableProperty]
        private OperariosAccesoDto? operarioSupervisionSeleccionado;
        
        [ObservableProperty]
        private string filtroAlmacenesSupervisionCombo = "";
        
        [ObservableProperty]
        private string filtroUbicacionesSupervisionCombo = "";
        
        [ObservableProperty]
        private string filtroOperariosSupervisionCombo = "";
        
        [ObservableProperty]
        private bool isDropDownOpenAlmacenesSupervision = false;
        
        [ObservableProperty]
        private bool isDropDownOpenUbicacionesSupervision = false;
        
        [ObservableProperty]
        private bool isDropDownOpenOperariosSupervision = false;
        
        // Colecciones para autocompletado de supervisión
        public ObservableCollection<string> AlmacenesSupervisionCombo { get; } = new();
        public ObservableCollection<string> UbicacionesSupervisionCombo { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosSupervisionCombo { get; } = new();
        
        // Vistas filtrables para autocompletado
        public ICollectionView AlmacenesSupervisionComboView { get; private set; }
        public ICollectionView UbicacionesSupervisionComboView { get; private set; }
        public ICollectionView OperariosSupervisionComboView { get; private set; }
        
        // Filtros para autocompletado de operarios
        [ObservableProperty]
        private string filtroOperariosCombo = "";
        
        [ObservableProperty]
        private string filtroOperariosDisponibles = "";
        
        [ObservableProperty]
        private bool isDropDownOpenCombo = false;
        
        [ObservableProperty]
        private bool isDropDownOpenDisponibles = false;
        
        // Propiedades para filtrado de almacenes
        [ObservableProperty]
        private string filtroAlmacenesTexto = "";
        
        [ObservableProperty]
        private bool isDropDownOpenAlmacenes = false;

        public ICollectionView OrdenesConteoView { get; }
        
        // Propiedades para filtros de conteos periódicos
        [ObservableProperty]
        private AlmacenDto? almacenSeleccionadoPeriodicos;
        
        [ObservableProperty]
        private DateTime? fechaDesdePeriodicos;
        
        [ObservableProperty]
        private DateTime? fechaHastaPeriodicos;
        
        [ObservableProperty]
        private string estadoActivoFiltro = "TODOS"; // TODOS, ACTIVO, INACTIVO
        
        [ObservableProperty]
        private OperariosAccesoDto? operarioSeleccionadoPeriodicos;
        
        [ObservableProperty]
        private bool verTodosLosConteosPeriodicos = false; // Por defecto, solo ver los propios
        
        [ObservableProperty]
        private OperariosAccesoDto? operarioCreadorSeleccionadoPeriodicos;
        #endregion

        #region Computed Properties
        public bool CanEnableInputs => !IsCargando;
        public bool CanCargarControles => !IsCargando && AlmacenSeleccionadoCombo != null;
        
        // Propiedades calculadas para supervisión
        public bool MostrandoOrdenes => ModoVisualizacion == "ORDENES";
        public bool MostrandoSupervision => ModoVisualizacion == "SUPERVISION";
        public bool MostrandoPeriodicos => ModoVisualizacion == "PERIODICOS";
        public int TotalResultados => ResultadosSupervision?.Count ?? 0;
        public int ResultadosPendientes => ResultadosSupervision?.Count(r => r.RequiereAprobacion) ?? 0;
        public bool PuedeReasignar => ResultadoSeleccionado != null && 
                                   ResultadoSeleccionado.RequiereAprobacion && 
                                   OperarioAprobadorSeleccionado != null &&
                                   OperarioAprobadorSeleccionado.Operario != 0;

        public bool TieneFiltrosActivosSupervision
        {
            get
            {
                var verTodosActivo = VerTodosLosResultadosSupervision; // Si está en true, es un filtro activo
                var fechaDesdeActivo = FechaDesdeSupervision.HasValue;
                var fechaHastaActivo = FechaHastaSupervision.HasValue;
                var diferenciaDesdeActivo = DiferenciaDesdeSupervision.HasValue;
                var diferenciaHastaActivo = DiferenciaHastaSupervision.HasValue;
                var almacenActivo = !string.IsNullOrEmpty(AlmacenSupervisionSeleccionado);
                var ubicacionActiva = !string.IsNullOrEmpty(UbicacionSupervisionSeleccionada);
                var operarioActivo = OperarioSupervisionSeleccionado != null && 
                                     OperarioSupervisionSeleccionado.Operario > 0;

                return verTodosActivo || fechaDesdeActivo || fechaHastaActivo || 
                       diferenciaDesdeActivo || diferenciaHastaActivo || almacenActivo || 
                       ubicacionActiva || operarioActivo;
            }
        }

        public string ResumenFiltrosActivosSupervision
        {
            get
            {
                var filtros = new List<string>();

                if (VerTodosLosResultadosSupervision)
                {
                    filtros.Add("Ver todos");
                }

                if (FechaDesdeSupervision.HasValue || FechaHastaSupervision.HasValue)
                {
                    var fechaStr = FechaDesdeSupervision.HasValue && FechaHastaSupervision.HasValue
                        ? $"{FechaDesdeSupervision.Value:dd/MM/yyyy} - {FechaHastaSupervision.Value:dd/MM/yyyy}"
                        : FechaDesdeSupervision.HasValue
                            ? $"Desde {FechaDesdeSupervision.Value:dd/MM/yyyy}"
                            : $"Hasta {FechaHastaSupervision.Value:dd/MM/yyyy}";
                    filtros.Add($"Fecha: {fechaStr}");
                }

                if (DiferenciaDesdeSupervision.HasValue || DiferenciaHastaSupervision.HasValue)
                {
                    var diferenciaStr = DiferenciaDesdeSupervision.HasValue && DiferenciaHastaSupervision.HasValue
                        ? $"{DiferenciaDesdeSupervision.Value:N2} - {DiferenciaHastaSupervision.Value:N2}"
                        : DiferenciaDesdeSupervision.HasValue
                            ? $"Desde {DiferenciaDesdeSupervision.Value:N2}"
                            : $"Hasta {DiferenciaHastaSupervision.Value:N2}";
                    filtros.Add($"Diferencia: {diferenciaStr}");
                }

                if (!string.IsNullOrEmpty(AlmacenSupervisionSeleccionado))
                {
                    filtros.Add($"Almacén: {AlmacenSupervisionSeleccionado}");
                }

                if (!string.IsNullOrEmpty(UbicacionSupervisionSeleccionada))
                {
                    filtros.Add($"Ubicación: {UbicacionSupervisionSeleccionada}");
                }

                if (OperarioSupervisionSeleccionado != null && OperarioSupervisionSeleccionado.Operario > 0)
                {
                    filtros.Add($"Operario: {OperarioSupervisionSeleccionado.NombreOperario}");
                }

                return string.Join(" | ", filtros);
            }
        }

        public string TotalOrdenes
        {
            get
            {
                var total = OrdenesConteo?.Count ?? 0;
                return $"Total: {total} orden{(total != 1 ? "es" : "")} de conteo";
            }
        }

        public bool TieneFiltrosActivos
        {
            get
            {
                // Verificar si hay filtros activos
                var almacenActivo = AlmacenSeleccionadoCombo != null && 
                                    AlmacenSeleccionadoCombo.CodigoAlmacen != "Todas";
                var estadoActivo = !string.IsNullOrEmpty(EstadoFiltro) && EstadoFiltro != "TODOS";
                var operarioActivo = OperarioSeleccionadoCombo != null && 
                                     OperarioSeleccionadoCombo.Operario != 0;
                var idActivo = !string.IsNullOrWhiteSpace(IdOrdenFiltro);
                var verTodosActivo = VerTodosLosConteos;
                // Siempre mostrar fechas como filtro activo (incluso si son los valores por defecto)
                var fechasActivas = true; // Siempre hay un rango de fechas aplicado

                return almacenActivo || estadoActivo || operarioActivo || idActivo || verTodosActivo || fechasActivas;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

                // Siempre mostrar las fechas (incluso si son los valores por defecto)
                filtros.Add($"Fechas: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}");

                if (AlmacenSeleccionadoCombo != null && AlmacenSeleccionadoCombo.CodigoAlmacen != "Todas")
                {
                    filtros.Add($"Almacén: {AlmacenSeleccionadoCombo.CodigoAlmacen}");
                }

                if (!string.IsNullOrEmpty(EstadoFiltro) && EstadoFiltro != "TODOS")
                {
                    filtros.Add($"Estado: {EstadoFiltro}");
                }

                if (OperarioSeleccionadoCombo != null && OperarioSeleccionadoCombo.Operario != 0)
                {
                    filtros.Add($"Operario: {OperarioSeleccionadoCombo.NombreOperario}");
                }

                if (!string.IsNullOrWhiteSpace(IdOrdenFiltro))
                {
                    filtros.Add($"ID: {IdOrdenFiltro}");
                }

                if (VerTodosLosConteos)
                {
                    filtros.Add("Ver todos");
                    // Si hay un creador seleccionado, agregarlo al resumen
                    if (OperarioCreadorSeleccionadoCombo != null && OperarioCreadorSeleccionadoCombo.Operario > 0)
                    {
                        filtros.Add($"Creado por: {OperarioCreadorSeleccionadoCombo.NombreOperario}");
                    }
                }
                else
                {
                    filtros.Add("Solo propios");
                }

                return string.Join(" | ", filtros);
            }
        }
        
        public bool TieneFiltrosActivosPeriodicos
        {
            get
            {
                var almacenActivo = AlmacenSeleccionadoPeriodicos != null && 
                                    AlmacenSeleccionadoPeriodicos.CodigoAlmacen != "Todas";
                var estadoActivo = !string.IsNullOrEmpty(EstadoActivoFiltro) && EstadoActivoFiltro != "TODOS";
                var operarioActivo = OperarioSeleccionadoPeriodicos != null && 
                                     OperarioSeleccionadoPeriodicos.Operario != 0;
                var verTodosActivo = VerTodosLosConteosPeriodicos;
                var fechasActivas = FechaDesdePeriodicos.HasValue || FechaHastaPeriodicos.HasValue;

                return almacenActivo || estadoActivo || operarioActivo || verTodosActivo || fechasActivas;
            }
        }

        public string ResumenFiltrosActivosPeriodicos
        {
            get
            {
                var filtros = new List<string>();

                // Mostrar fechas solo si están establecidas
                if (FechaDesdePeriodicos.HasValue || FechaHastaPeriodicos.HasValue)
                {
                    var fechaDesdeStr = FechaDesdePeriodicos.HasValue 
                        ? FechaDesdePeriodicos.Value.ToString("dd/MM/yyyy") 
                        : "...";
                    var fechaHastaStr = FechaHastaPeriodicos.HasValue 
                        ? FechaHastaPeriodicos.Value.ToString("dd/MM/yyyy") 
                        : "...";
                    filtros.Add($"Fechas: {fechaDesdeStr} - {fechaHastaStr}");
                }

                if (AlmacenSeleccionadoPeriodicos != null && AlmacenSeleccionadoPeriodicos.CodigoAlmacen != "Todas")
                {
                    filtros.Add($"Almacén: {AlmacenSeleccionadoPeriodicos.CodigoAlmacen}");
                }

                if (!string.IsNullOrEmpty(EstadoActivoFiltro) && EstadoActivoFiltro != "TODOS")
                {
                    filtros.Add($"Estado: {EstadoActivoFiltro}");
                }

                if (OperarioSeleccionadoPeriodicos != null && OperarioSeleccionadoPeriodicos.Operario != 0)
                {
                    filtros.Add($"Operario: {OperarioSeleccionadoPeriodicos.NombreOperario}");
                }

                if (VerTodosLosConteosPeriodicos)
                {
                    filtros.Add("Ver todos");
                    // Si hay un creador seleccionado, agregarlo al resumen
                    if (OperarioCreadorSeleccionadoPeriodicos != null && OperarioCreadorSeleccionadoPeriodicos.Operario > 0)
                    {
                        filtros.Add($"Creado por: {OperarioCreadorSeleccionadoPeriodicos.NombreOperario}");
                    }
                }
                else
                {
                    filtros.Add("Solo propios");
                }

                return string.Join(" | ", filtros);
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task CrearControlRotativo()
        {
            try
            {
                // Crear el ViewModel del diálogo
                var dialogViewModel = new CrearOrdenConteoDialogViewModel(_conteosService, _stockService, new LoginService(), new InventarioService(), new UbicacionesService());
                
                // Crear y mostrar el diálogo
                var dialog = new CrearOrdenConteoDialog(dialogViewModel);
                
                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo
                dialog.ShowDialog();
                
                // Si se creó una orden, recargar la lista
                await CargarControles();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al abrir el diálogo de creación: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private async Task AbrirFiltros()
        {
            try
            {
                // Crear el ViewModel del diálogo con los valores actuales
                var dialogViewModel = new FiltrosOrdenesConteoDialogViewModel(
                    AlmacenSeleccionadoCombo,
                    FechaDesde,
                    FechaHasta,
                    EstadoFiltro,
                    OperarioSeleccionadoCombo,
                    IdOrdenFiltro,
                    VerTodosLosConteos,
                    OperarioCreadorSeleccionadoCombo
                );

                // Crear y mostrar el diálogo
                var dialog = new FiltrosOrdenesConteoDialog(dialogViewModel);

                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo y esperar resultado
                var result = dialog.ShowDialog();

                // Si el usuario hizo clic en "Recargar" (result == true), aplicar los filtros
                if (result == true)
                {
                    // Actualizar los filtros del ViewModel principal con los valores del diálogo
                    AlmacenSeleccionadoCombo = dialogViewModel.AlmacenSeleccionadoCombo;
                    FechaDesde = dialogViewModel.FechaDesde;
                    FechaHasta = dialogViewModel.FechaHasta;
                    EstadoFiltro = dialogViewModel.EstadoFiltro;
                    OperarioSeleccionadoCombo = dialogViewModel.OperarioSeleccionadoCombo;
                    IdOrdenFiltro = dialogViewModel.IdOrdenFiltro;
                    VerTodosLosConteos = dialogViewModel.VerTodosLosConteos;
                    OperarioCreadorSeleccionadoCombo = dialogViewModel.OperarioCreadorSeleccionadoCombo;

                    // Notificar cambios en propiedades calculadas
                    OnPropertyChanged(nameof(TieneFiltrosActivos));
                    OnPropertyChanged(nameof(ResumenFiltrosActivos));

                    // Recargar los controles con los nuevos filtros
                    await CargarControles();
                    
                    // Refrescar la vista para aplicar el filtro de ID
                    OrdenesConteoView?.Refresh();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al abrir el diálogo de filtros: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private void LimpiarFiltros()
        {
            EstadoFiltro = "TODOS";
            IdOrdenFiltro = string.Empty;
            VerTodosLosConteos = false; // Por defecto, solo ver los propios
            
            // Verificar que las colecciones no estén vacías antes de acceder a FirstOrDefault()
            if (OperariosCombo?.Any() == true)
            {
                OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                FiltroOperariosCombo = ""; // Limpiar el filtro de texto
            }
            
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
                FiltroAlmacenesTexto = ""; // Limpiar el filtro de texto
            }
            
            // Establecer las fechas: desde hace 7 días hasta hoy
            FechaDesde = DateTime.Today.AddDays(-7);
            FechaHasta = DateTime.Today;

            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            
            // Recargar controles y refrescar vista
            _ = CargarControles();
            OrdenesConteoView?.Refresh();
        }

        [RelayCommand]
        private async Task CargarControles()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando órdenes de conteo...";

                // Filtrar por estado si no es "TODOS"
                var estadoFiltro = EstadoFiltro == "TODOS" ? null : EstadoFiltro;
                
                // Filtrar por operario si no es "TODOS"
                var operarioFiltro = OperarioSeleccionadoCombo?.Operario == 0 ? null : OperarioSeleccionadoCombo?.Operario.ToString();

                Debug.WriteLine($"CargarControles - EstadoFiltro: '{EstadoFiltro}', OperarioFiltro: '{OperarioSeleccionadoCombo?.DescripcionCombo}', FechaDesde: {FechaDesde:yyyy-MM-dd}, FechaHasta: {FechaHasta:yyyy-MM-dd}");
                
                // Obtener código de operario del usuario actual para filtrar por almacenes autorizados
                // El filtro de almacenes autorizados siempre se aplica (independiente de VerTodosLosConteos)
                string? codigoOperarioSesion = SessionManager.UsuarioActual?.operario.ToString();
                
                // Obtener código del creador para filtrar por "solo propios" vs "ver todos"
                // Si VerTodosLosConteos es false, filtrar por CreadoPorCodigo (solo los que creó el usuario)
                // Si VerTodosLosConteos es true, usar el filtro de creador seleccionado (si hay uno)
                string? creadoPorCodigo;
                if (!VerTodosLosConteos)
                {
                    // Solo ver los propios
                    creadoPorCodigo = SessionManager.UsuarioActual?.operario.ToString();
                }
                else
                {
                    // Ver todos, pero filtrar por creador si hay uno seleccionado
                    creadoPorCodigo = OperarioCreadorSeleccionadoCombo?.Operario > 0 
                        ? OperarioCreadorSeleccionadoCombo.Operario.ToString() 
                        : null;
                }
                
                // Si hay un filtro especial activo (desde WelcomeView), pasar null como fechas
                // para que el backend no filtre por fecha y cargue todos los conteos
                DateTime? fechaDesdeParam = string.IsNullOrEmpty(_filtroEspecial) ? FechaDesde : null;
                DateTime? fechaHastaParam = string.IsNullOrEmpty(_filtroEspecial) ? FechaHasta : null;
                
                // Pasar las fechas al servicio para filtrar en el backend
                var ordenes = await _conteosService.ListarTodasLasOrdenesAsync(
                    estadoFiltro, 
                    operarioFiltro,
                    fechaDesdeParam,
                    fechaHastaParam,
                    codigoOperarioSesion,
                    creadoPorCodigo);
                
                Debug.WriteLine($"CargarControles - Se obtuvieron {ordenes?.Count ?? 0} órdenes");

                // Asegurar que tenemos los operarios cargados para el mapeo de nombres
                if (OperariosDisponibles.Count == 0)
                {
                    await CargarOperarios();
                }

                // Crear diccionario para mapear códigos a nombres de operarios
                var operariosDict = OperariosDisponibles
                    .Where(op => op.Operario > 0) // Excluir "Sin asignar"
                    .ToDictionary(op => op.Operario.ToString(), op => op.NombreOperario ?? "");

                OrdenesConteo.Clear();
                foreach (var orden in ordenes)
                {
                    // Mapear nombre del operario si existe
                    if (!string.IsNullOrEmpty(orden.CodigoOperario) && 
                        operariosDict.TryGetValue(orden.CodigoOperario, out var nombreOperario))
                    {
                        orden.NombreOperario = nombreOperario;
                    }
                    
                    // Mapear nombre del creador si existe
                    if (!string.IsNullOrEmpty(orden.CreadoPorCodigo) && 
                        operariosDict.TryGetValue(orden.CreadoPorCodigo, out var nombreCreador))
                    {
                        orden.NombreCreador = nombreCreador;
                    }
                    
                    OrdenesConteo.Add(orden);
                }

                OrdenesConteoView.Refresh();
                OnPropertyChanged(nameof(TotalOrdenes));
                OnPropertyChanged(nameof(TieneFiltrosActivos));
                OnPropertyChanged(nameof(ResumenFiltrosActivos));

                MensajeEstado = $"Se cargaron {OrdenesConteo.Count} órdenes de conteo";
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al cargar órdenes",
                    $"No se pudieron cargar las órdenes de conteo: {ex.Message}");
                ShowCenteredDialog(errorDialog);
                MensajeEstado = "Error al cargar órdenes";
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private void VerOrden(OrdenConteoDto orden)
        {
            if (orden == null) return;

            try
            {
                // Crear y mostrar el nuevo diálogo personalizado
                var dialog = new VerOrdenConteoDialog(orden);
                
                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al mostrar el diálogo: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private async Task CerrarOrden(OrdenConteoDto orden)
        {
            if (orden == null) return;

            try
            {
                // Confirmar antes de cerrar
                var confirm = new ConfirmationDialog(
                    "Cerrar Orden de Conteo",
                    $"¿Estás seguro de que deseas cerrar la orden '{orden.Titulo}'?\n\nEsta acción no se puede deshacer.",
                    "\uE11B" // ícono de pregunta
                );
                
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                if (mainWindow != null && mainWindow != confirm)
                    confirm.Owner = mainWindow;
                
                if (confirm.ShowDialog() != true)
                    return;

                // Verificar el estado actual desde la BD antes de cerrar
                var ordenActual = await _conteosService.ObtenerOrdenAsync(orden.GuidID);
                
                if (ordenActual == null)
                {
                    var dialog = new WarningDialog(
                        "Error", 
                        "No se pudo obtener la información actual de la orden.");
                    ShowCenteredDialog(dialog);
                    return;
                }
                
                if (ordenActual.Estado != "EN_PROCESO")
                {
                    var dialog = new WarningDialog(
                        "No se puede cerrar", 
                        $"La orden '{ordenActual.Titulo}' está en estado '{ordenActual.EstadoFormateado}' y no se puede cerrar.\n\nSolo se pueden cerrar órdenes en estado 'En Proceso'.");
                    ShowCenteredDialog(dialog);
                    
                    // Refrescar la lista para actualizar los estados
                    await CargarControles();
                    return;
                }

                // Cerrar la orden
                IsCargando = true;
                MensajeEstado = $"Cerrando orden '{orden.Titulo}'...";
                
                await _conteosService.CerrarOrdenAsync(orden.GuidID);
                
                // Mostrar mensaje de éxito
                var successDialog = new WarningDialog(
                    "Orden cerrada",
                    $"La orden '{orden.Titulo}' ha sido cerrada correctamente.");
                ShowCenteredDialog(successDialog);
                
                // Refrescar la lista
                await CargarControles();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al cerrar orden", 
                    $"No se pudo cerrar la orden: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task EditarOrden(OrdenConteoDto orden)
        {
            if (orden == null) return;

            try
            {
                // PRIMER CHECK: Consultar el estado actual de la orden desde la base de datos
                // (no usar los datos de la interfaz que pueden estar desactualizados)
                Debug.WriteLine($"PRIMER CHECK - Consultando estado actual de la orden desde BD...");
                var ordenActual = await _conteosService.ObtenerOrdenAsync(orden.GuidID);
                
                if (ordenActual == null)
                {
                    Debug.WriteLine($"PRIMER CHECK - ERROR: No se pudo obtener la orden desde BD");
                    var dialog = new WarningDialog(
                        "Error", 
                        "No se pudo obtener la información actual de la orden.");
                    ShowCenteredDialog(dialog);
                    return;
                }
                
                Debug.WriteLine($"PRIMER CHECK - Estado en interfaz: '{orden.Estado}'");
                Debug.WriteLine($"PRIMER CHECK - Estado actual en BD: '{ordenActual.Estado}'");
                Debug.WriteLine($"PRIMER CHECK - Estado formateado: '{ordenActual.EstadoFormateado}'");
                
                if (ordenActual.Estado != "PLANIFICADO" && ordenActual.Estado != "ASIGNADO")
                {
                    Debug.WriteLine($"PRIMER CHECK - BLOQUEANDO edición porque estado actual '{ordenActual.Estado}' no es editable");
                    var dialog = new WarningDialog(
                        "No se puede editar", 
                        $"La orden '{ordenActual.Titulo}' está en estado '{ordenActual.EstadoFormateado}' y no se puede editar.\n\nSolo se pueden editar órdenes en estado 'Asignado'.");
                    ShowCenteredDialog(dialog);
                    
                    // Refrescar la lista para actualizar los estados
                    Debug.WriteLine("PRIMER CHECK - Refrescando lista después del aviso...");
                    await CargarControles();
                    return;
                }
                
                Debug.WriteLine($"PRIMER CHECK - PERMITIENDO edición porque estado actual '{ordenActual.Estado}' es editable");

                // Crear el ViewModel del diálogo de edición
                var dialogViewModel = new EditarOrdenConteoDialogViewModel(_conteosService, _stockService, new LoginService(), new InventarioService(), new UbicacionesService());
                
                // Cargar los datos de la orden
                await dialogViewModel.CargarOrdenAsync(orden.GuidID);
                
                // Crear y mostrar el diálogo
                var editDialog = new EditarOrdenConteoDialog();
                editDialog.DataContext = dialogViewModel;
                
                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                editDialog.Owner = mainWindow;
                
                // Mostrar el diálogo
                var result = editDialog.ShowDialog();
                
                // Si se guardó correctamente, refrescar la lista
                if (result == true)
                {
                    await CargarControles();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al editar orden", 
                    $"No se pudo editar la orden: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }



        [RelayCommand]
        private async Task ExportarControles()
        {
            var dialog = new WarningDialog(
                "Exportar Órdenes",
                "Funcionalidad de exportación de órdenes de conteo en desarrollo.");
            dialog.ShowDialog();
        }

        // Comandos para supervisión
        [RelayCommand]
        private void CambiarAOrdenes()
        {
            ModoVisualizacion = "ORDENES";
            OnPropertyChanged(nameof(MostrandoOrdenes));
            OnPropertyChanged(nameof(MostrandoSupervision));
            OnPropertyChanged(nameof(MostrandoPeriodicos));
        }

        [RelayCommand]
        private async Task CambiarASupervision()
        {
            ModoVisualizacion = "SUPERVISION";
            OnPropertyChanged(nameof(MostrandoOrdenes));
            OnPropertyChanged(nameof(MostrandoSupervision));
            OnPropertyChanged(nameof(MostrandoPeriodicos));
            
            // Cargar datos de supervisión si es la primera vez
            if (ResultadosSupervision.Count == 0)
            {
                await CargarResultadosSupervision();
                await CargarOperarios();
            }
        }

        [RelayCommand]
        private async Task CambiarAPeriodicos()
        {
            ModoVisualizacion = "PERIODICOS";
            OnPropertyChanged(nameof(MostrandoOrdenes));
            OnPropertyChanged(nameof(MostrandoSupervision));
            OnPropertyChanged(nameof(MostrandoPeriodicos));
            
            // Inicializar filtros si es la primera vez
            if (AlmacenSeleccionadoPeriodicos == null && AlmacenesCombo.Any())
            {
                AlmacenSeleccionadoPeriodicos = AlmacenesCombo.FirstOrDefault();
            }
            
            if (OperarioSeleccionadoPeriodicos == null && OperariosCombo.Any())
            {
                OperarioSeleccionadoPeriodicos = OperariosCombo.FirstOrDefault();
            }
            
            // Cargar conteos periódicos si es la primera vez
            if (ConteosPeriodicos.Count == 0)
            {
                await CargarConteosPeriodicos();
            }
        }

        [RelayCommand]
        private async Task CargarConteosPeriodicos()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando conteos periódicos...";

                // Obtener código de operario del usuario actual para filtrar por almacenes autorizados
                var codigoOperarioSesion = SessionManager.UsuarioActual?.operario.ToString();
                
                // Obtener parámetros de filtro
                var codigoAlmacen = AlmacenSeleccionadoPeriodicos?.CodigoAlmacen != "Todas" 
                    ? AlmacenSeleccionadoPeriodicos?.CodigoAlmacen 
                    : null;
                
                // Solo aplicar filtro de fechas si están establecidas
                var fechaDesde = FechaDesdePeriodicos;
                var fechaHasta = FechaHastaPeriodicos;
                
                // Convertir estado a bool?
                bool? activo = EstadoActivoFiltro switch
                {
                    "ACTIVO" => true,
                    "INACTIVO" => false,
                    _ => null // TODOS
                };
                
                // Obtener código de operario asignado (filtro visual)
                var codigoOperario = OperarioSeleccionadoPeriodicos?.Operario > 0 
                    ? OperarioSeleccionadoPeriodicos.Operario.ToString() 
                    : null;
                
                // Obtener código del creador para filtrar por "solo propios" vs "ver todos"
                // Si VerTodosLosConteosPeriodicos es false, filtrar por CreadoPorCodigo (solo los que creó el usuario)
                // Si VerTodosLosConteosPeriodicos es true, usar el filtro de creador seleccionado (si hay uno)
                string? creadoPorCodigo;
                if (!VerTodosLosConteosPeriodicos)
                {
                    // Solo ver los propios
                    creadoPorCodigo = SessionManager.UsuarioActual?.operario.ToString();
                }
                else
                {
                    // Ver todos, pero filtrar por creador si hay uno seleccionado
                    creadoPorCodigo = OperarioCreadorSeleccionadoPeriodicos?.Operario > 0 
                        ? OperarioCreadorSeleccionadoPeriodicos.Operario.ToString() 
                        : null;
                }
                
                var conteos = await _conteosService.ListarConteosPeriodicosAsync(
                    codigoAlmacen,
                    fechaDesde,
                    fechaHasta,
                    activo,
                    codigoOperario,
                    codigoOperarioSesion,
                    creadoPorCodigo);

                // Asegurar que tenemos los operarios cargados para el mapeo de nombres
                if (OperariosDisponibles.Count == 0)
                {
                    await CargarOperarios();
                }

                // Crear diccionario para mapear códigos a nombres de operarios
                var operariosDict = OperariosDisponibles
                    .Where(op => op.Operario > 0) // Excluir "Sin asignar"
                    .ToDictionary(op => op.Operario.ToString(), op => op.NombreOperario ?? "");

                ConteosPeriodicos.Clear();
                foreach (var conteo in conteos.OrderByDescending(c => c.FechaCreacion))
                {
                    // Mapear nombre del operario si existe
                    if (!string.IsNullOrEmpty(conteo.CodigoOperario) && 
                        operariosDict.TryGetValue(conteo.CodigoOperario, out var nombreOperario))
                    {
                        conteo.NombreOperario = nombreOperario;
                    }
                    
                    // Mapear nombre del creador si existe
                    if (!string.IsNullOrEmpty(conteo.CreadoPorCodigo) && 
                        operariosDict.TryGetValue(conteo.CreadoPorCodigo, out var nombreCreador))
                    {
                        conteo.NombreCreador = nombreCreador;
                    }
                    
                    ConteosPeriodicos.Add(conteo);
                }

                OnPropertyChanged(nameof(TieneFiltrosActivosPeriodicos));
                OnPropertyChanged(nameof(ResumenFiltrosActivosPeriodicos));

                MensajeEstado = $"Se cargaron {ConteosPeriodicos.Count} conteos periódicos";
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al cargar conteos periódicos",
                    $"No se pudieron cargar los conteos periódicos: {ex.Message}");
                ShowCenteredDialog(errorDialog);
                MensajeEstado = "Error al cargar conteos periódicos";
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task ActivarPeriodicidad(ConteoPeriodicoDto conteo)
        {
            if (conteo == null) return;

            try
            {
                // Abrir el diálogo de edición en lugar de activar directamente
                var dialogViewModel = new EditarOrdenConteoDialogViewModel(_conteosService, _stockService, new LoginService(), new InventarioService(), new UbicacionesService());
                
                // Cargar los datos de la orden (todos excepto FechaProximaRenovacion si está desactivado)
                await dialogViewModel.CargarOrdenAsync(conteo.GuidID);
                
                // Crear y mostrar el diálogo
                var editDialog = new EditarOrdenConteoDialog();
                editDialog.DataContext = dialogViewModel;
                
                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                editDialog.Owner = mainWindow;
                
                // Mostrar el diálogo
                var result = editDialog.ShowDialog();
                
                // Si se guardó correctamente, recargar la lista
                if (result == true)
                {
                    await CargarConteosPeriodicos();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al abrir diálogo de edición",
                    $"No se pudo abrir el diálogo de edición: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private async Task DesactivarPeriodicidad(ConteoPeriodicoDto conteo)
        {
            if (conteo == null) return;

            try
            {
                await _conteosService.DesactivarPeriodicidadAsync(conteo.GuidID);
                
                // Recargar la lista para obtener datos actualizados
                await CargarConteosPeriodicos();
                
                var successDialog = new WarningDialog(
                    "Periodicidad desactivada",
                    $"El conteo periódico '{conteo.Titulo}' ha sido desactivado correctamente.");
                ShowCenteredDialog(successDialog);
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al desactivar periodicidad",
                    $"No se pudo desactivar la periodicidad: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private async Task AbrirFiltrosConteosPeriodicos()
        {
            try
            {
                // Crear el ViewModel del diálogo con los valores actuales
                var dialogViewModel = new FiltrosConteosPeriodicosDialogViewModel(
                    AlmacenSeleccionadoPeriodicos,
                    FechaDesdePeriodicos,
                    FechaHastaPeriodicos,
                    EstadoActivoFiltro,
                    OperarioSeleccionadoPeriodicos,
                    VerTodosLosConteosPeriodicos,
                    OperarioCreadorSeleccionadoPeriodicos
                );

                // Crear y mostrar el diálogo
                var dialog = new FiltrosConteosPeriodicosDialog(dialogViewModel);

                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo y esperar resultado
                var result = dialog.ShowDialog();

                // Si el usuario hizo clic en "Aplicar" (result == true), aplicar los filtros
                if (result == true)
                {
                    // Actualizar los filtros del ViewModel principal con los valores del diálogo
                    AlmacenSeleccionadoPeriodicos = dialogViewModel.AlmacenSeleccionadoCombo;
                    FechaDesdePeriodicos = dialogViewModel.FechaDesde;
                    FechaHastaPeriodicos = dialogViewModel.FechaHasta;
                    EstadoActivoFiltro = dialogViewModel.EstadoActivoFiltro;
                    OperarioSeleccionadoPeriodicos = dialogViewModel.OperarioSeleccionadoCombo;
                    VerTodosLosConteosPeriodicos = dialogViewModel.VerTodosLosConteos;
                    OperarioCreadorSeleccionadoPeriodicos = dialogViewModel.OperarioCreadorSeleccionadoCombo;

                    // Notificar cambios en propiedades calculadas
                    OnPropertyChanged(nameof(TieneFiltrosActivosPeriodicos));
                    OnPropertyChanged(nameof(ResumenFiltrosActivosPeriodicos));

                    // Recargar los conteos periódicos con los nuevos filtros
                    await CargarConteosPeriodicos();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al abrir el diálogo de filtros: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private void LimpiarFiltrosConteosPeriodicos()
        {
            EstadoActivoFiltro = "TODOS";
            VerTodosLosConteosPeriodicos = false; // Por defecto, solo ver los propios
            
            // Verificar que las colecciones no estén vacías antes de acceder a FirstOrDefault()
            if (OperariosCombo?.Any() == true)
            {
                OperarioSeleccionadoPeriodicos = OperariosCombo.FirstOrDefault();
                FiltroOperariosCombo = "";
            }
            
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenSeleccionadoPeriodicos = AlmacenesCombo.FirstOrDefault();
                FiltroAlmacenesTexto = "";
            }
            
            // Limpiar las fechas (sin filtro por defecto)
            FechaDesdePeriodicos = null;
            FechaHastaPeriodicos = null;

            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivosPeriodicos));
            OnPropertyChanged(nameof(ResumenFiltrosActivosPeriodicos));
            
            // Recargar conteos periódicos y refrescar vista
            _ = CargarConteosPeriodicos();
        }

        [RelayCommand]
        private async Task VerRenovaciones(ConteoPeriodicoDto conteo)
        {
            if (conteo == null) return;

            try
            {
                // Crear y mostrar el nuevo diálogo de historial
                var dialog = new HistorialRenovacionesDialog(conteo);
                
                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                
                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al obtener renovaciones",
                    $"No se pudieron obtener las renovaciones: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private async Task CargarResultadosSupervision()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando resultados de supervisión...";

                var resultados = await _conteosService.ObtenerResultadosSupervisionAsync();

                // Asegurar que tenemos los operarios cargados para el mapeo de nombres
                if (OperariosDisponibles.Count == 0)
                {
                    await CargarOperarios();
                }

                // Crear diccionario para mapear códigos a nombres de operarios
                var operariosDict = OperariosDisponibles
                    .Where(op => op.Operario > 0) // Excluir "Sin asignar"
                    .ToDictionary(op => op.Operario.ToString(), op => op.NombreOperario ?? "");

                ResultadosSupervision.Clear();
                foreach (var resultado in resultados.OrderByDescending(r => r.FechaEvaluacion))
                {
                    // Mapear nombre del operario que hizo el conteo
                    if (!string.IsNullOrEmpty(resultado.UsuarioCodigo) && 
                        operariosDict.TryGetValue(resultado.UsuarioCodigo, out var nombreOperario))
                    {
                        resultado.NombreOperario = nombreOperario;
                    }
                    
                    // Mapear nombre del creador de la orden
                    if (!string.IsNullOrEmpty(resultado.CreadoPorCodigo) && 
                        operariosDict.TryGetValue(resultado.CreadoPorCodigo, out var nombreCreador))
                    {
                        resultado.NombreCreador = nombreCreador;
                    }
                    
                    ResultadosSupervision.Add(resultado);
                }

                // Poblar ComboBoxes con valores únicos de almacenes, ubicaciones y operarios
                PoblarComboBoxesSupervision(resultados);

                ResultadosView.Refresh();
                OnPropertyChanged(nameof(TotalResultados));
                OnPropertyChanged(nameof(ResultadosPendientes));
                OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
                OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));

                MensajeEstado = $"Se cargaron {ResultadosSupervision.Count} resultados de supervisión";
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al cargar resultados",
                    $"No se pudieron cargar los resultados de supervisión: {ex.Message}");
                ShowCenteredDialog(errorDialog);
                MensajeEstado = "Error al cargar resultados";
            }
            finally
            {
                IsCargando = false;
            }
        }

        private void PoblarComboBoxesSupervision(IEnumerable<ResultadoConteoDetalladoDto> resultados)
        {
            // Poblar almacenes únicos
            var almacenesUnicos = resultados
                .Where(r => !string.IsNullOrEmpty(r.CodigoAlmacen))
                .Select(r => r.CodigoAlmacen!)
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            AlmacenesSupervisionCombo.Clear();
            foreach (var almacen in almacenesUnicos)
            {
                AlmacenesSupervisionCombo.Add(almacen);
            }

            // Poblar ubicaciones únicas
            var ubicacionesUnicas = resultados
                .Where(r => !string.IsNullOrEmpty(r.CodigoUbicacion))
                .Select(r => r.CodigoUbicacion!)
                .Distinct()
                .OrderBy(u => u)
                .ToList();

            UbicacionesSupervisionCombo.Clear();
            foreach (var ubicacion in ubicacionesUnicas)
            {
                UbicacionesSupervisionCombo.Add(ubicacion);
            }

            // Poblar operarios únicos (solo los que han hecho conteos)
            var operariosCodigos = resultados
                .Where(r => !string.IsNullOrEmpty(r.UsuarioCodigo))
                .Select(r => r.UsuarioCodigo!)
                .Distinct()
                .ToList();

            // Cargar operarios disponibles si no están cargados
            if (OperariosDisponibles.Count == 0)
            {
                _ = CargarOperarios();
            }

            // Filtrar operarios que han hecho conteos
            OperariosSupervisionCombo.Clear();
            
            // Agregar opción "Todos"
            OperariosSupervisionCombo.Add(new OperariosAccesoDto
            {
                Operario = 0,
                NombreOperario = "TODOS",
                Contraseña = "",
                MRH_CodigoAplicacion = 0
            });

            var operariosConConteos = OperariosDisponibles
                .Where(op => operariosCodigos.Contains(op.Operario.ToString()))
                .OrderBy(o => o.NombreOperario)
                .ToList();

            foreach (var operario in operariosConConteos)
            {
                OperariosSupervisionCombo.Add(operario);
            }
        }

        [RelayCommand]
        private async Task ReasignarLinea(ResultadoConteoDetalladoDto resultado)
        {
            if (resultado == null) return;

            // Establecer como seleccionado para mantener consistencia
            ResultadoSeleccionado = resultado;

            try
            {
                // Crear el diálogo
                var dialog = new ReasignarLineaDialog();
                
                // Asignar el DataContext correctamente
                var viewModel = new ReasignarLineaDialogViewModel();
                viewModel.ResultadoSeleccionado = resultado;
                
                // Cargar operarios
                await viewModel.CargarOperariosAsync();
                
                dialog.DataContext = viewModel;
                
                // Mostrar el diálogo
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                
                var result = dialog.ShowDialog();
                
                if (result == true)
                {
                    // Recargar los resultados de supervisión
                    _ = CargarResultadosSupervision();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al abrir diálogo de reasignación",
                    $"No se pudo abrir el diálogo: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }

        [RelayCommand]
        private void VerDetallesResultado(ResultadoConteoDetalladoDto resultado)
        {
            if (resultado == null) return;

            // Establecer como seleccionado para mantener consistencia
            ResultadoSeleccionado = resultado;

            var mensaje = $"DETALLES DEL RESULTADO DE CONTEO\n\n" +
                         $"INFORMACIÓN DE LA ORDEN\n" +
                         $"Título: {resultado.Titulo}\n" +
                         $"GUID Orden: {resultado.OrdenGuid}\n" +
                         $"Empresa: {resultado.CodigoEmpresa}\n" +
                         $"Tipo: {resultado.VisibilidadFormateada}\n\n" +
                         $"INFORMACIÓN DEL CONTEO\n" +
                         $"Almacén: {resultado.CodigoAlmacen}\n" +
                         $"Ubicación: {resultado.CodigoUbicacion ?? "N/A"}\n" +
                         $"Artículo: {resultado.CodigoArticulo ?? "N/A"}\n" +
                         $"Descripción: {resultado.DescripcionArticulo ?? "N/A"}\n" +
                         $"Lote/Partida: {resultado.LotePartida ?? "N/A"}\n\n" +
                         $"CANTIDADES Y DIFERENCIA\n" +
                         $"Cantidad en Stock: {resultado.CantidadStock?.ToString("N2") ?? "N/A"}\n" +
                         $"Cantidad Contada: {resultado.CantidadContada?.ToString("N2") ?? "N/A"}\n" +
                         $"Diferencia: {resultado.DiferenciaFormateada}\n\n" +
                         $"ESTADO Y APROBACIÓN\n" +
                         $"Acción: {resultado.AccionFormateada}\n" +
                         $"Estado: {resultado.EstadoTexto}\n" +
                         $"Operario: {resultado.UsuarioCodigo ?? "N/A"}\n" +
                         $"Aprobado por: {resultado.AprobadoPorCodigo ?? "Pendiente"}\n" +
                         $"Fecha Evaluación: {resultado.FechaEvaluacion:dd/MM/yyyy HH:mm}";

            var dialog = new WarningDialog("Detalles del Resultado", mensaje, "\uE946"); // Ícono de información
            ShowCenteredDialog(dialog);
        }

        [RelayCommand]
        private void LimpiarFiltrosSupervision()
        {
            // Limpiar filtros
            VerTodosLosResultadosSupervision = false; // Por defecto, solo ver los propios
            FechaDesdeSupervision = null;
            FechaHastaSupervision = null;
            DiferenciaDesdeSupervision = null;
            DiferenciaHastaSupervision = null;
            AlmacenSupervisionSeleccionado = null;
            UbicacionSupervisionSeleccionada = null;
            
            // Seleccionar "TODOS" en operarios
            if (OperariosSupervisionCombo?.Any() == true)
            {
                OperarioSupervisionSeleccionado = OperariosSupervisionCombo.FirstOrDefault();
                FiltroOperariosSupervisionCombo = "";
            }
            
            FiltroAlmacenesSupervisionCombo = "";
            FiltroUbicacionesSupervisionCombo = "";
            
            // Refrescar vista y notificar cambios
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        [RelayCommand]
        private async Task AbrirFiltrosSupervision()
        {
            try
            {
                // Asegurar que los operarios estén cargados
                if (OperariosDisponibles.Count == 0)
                {
                    await CargarOperarios();
                }

                // Crear el ViewModel del diálogo con los valores actuales
                var dialogViewModel = new FiltrosSupervisionDialogViewModel(
                    VerTodosLosResultadosSupervision,
                    FechaDesdeSupervision,
                    FechaHastaSupervision,
                    DiferenciaDesdeSupervision,
                    DiferenciaHastaSupervision,
                    AlmacenSupervisionSeleccionado,
                    UbicacionSupervisionSeleccionada,
                    OperarioSupervisionSeleccionado,
                    AlmacenesSupervisionCombo,
                    UbicacionesSupervisionCombo,
                    OperariosSupervisionCombo
                );

                // Crear y mostrar el diálogo
                var dialog = new FiltrosSupervisionDialog(dialogViewModel);

                // Configurar el owner del diálogo
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

                if (mainWindow != null && mainWindow != dialog)
                    dialog.Owner = mainWindow;

                // Mostrar el diálogo y esperar resultado
                var result = dialog.ShowDialog();

                // Si el usuario hizo clic en "Aplicar" (result == true), aplicar los filtros
                if (result == true)
                {
                    // Actualizar los filtros del ViewModel principal con los valores del diálogo
                    VerTodosLosResultadosSupervision = dialogViewModel.VerTodosLosResultados;
                    FechaDesdeSupervision = dialogViewModel.FechaDesde;
                    FechaHastaSupervision = dialogViewModel.FechaHasta;
                    DiferenciaDesdeSupervision = dialogViewModel.DiferenciaDesde;
                    DiferenciaHastaSupervision = dialogViewModel.DiferenciaHasta;
                    AlmacenSupervisionSeleccionado = dialogViewModel.AlmacenSeleccionado;
                    UbicacionSupervisionSeleccionada = dialogViewModel.UbicacionSeleccionada;
                    OperarioSupervisionSeleccionado = dialogViewModel.OperarioSeleccionado;

                    // Notificar cambios en propiedades calculadas
                    OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
                    OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));

                    // Refrescar la vista con los nuevos filtros
                    ResultadosView?.Refresh();
                    OnPropertyChanged(nameof(TotalResultados));
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al abrir el diálogo de filtros: {ex.Message}");
                ShowCenteredDialog(errorDialog);
            }
        }
        #endregion

        #region Private Methods
        private void ShowCenteredDialog(WarningDialog dialog)
        {
            // Configurar el owner para centrar el diálogo
            var mainWindow = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            
            if (mainWindow != null && mainWindow != dialog)
            {
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                // Si no hay ventana principal, centrar en pantalla
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
                
            dialog.ShowDialog();
        }

        private async Task InitializeAsync()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando almacenes...";

                await CargarAlmacenesAsync();
                
                // Solo cargar órdenes si NO hay un filtro especial pendiente
                // (OnFiltroSolicitado se encargará de cargar con el filtro)
                if (string.IsNullOrEmpty(_filtroEspecial))
                {
                    MensajeEstado = "Cargando órdenes de conteo...";
                    await CargarControles();
                }
                else
                {
                    // Hay filtro especial, OnFiltroSolicitado cargará los datos
                    MensajeEstado = "Aplicando filtro...";
                }
                
                MensajeEstado = "Listo";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
                var errorDialog = new WarningDialog("Error", $"Error al inicializar: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }

        private string _filtroEspecial = string.Empty;
        private bool _ajustandoFiltrosDesdeEvento = false;

        private async void OnFiltroSolicitado(object? sender, FiltroConteoEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnFiltroSolicitado recibido: {e.TipoFiltro}");
            
            _ajustandoFiltrosDesdeEvento = true;

            // Aplicar el filtro especial
            _filtroEspecial = e.TipoFiltro switch
            {
                TipoFiltroConteo.Pendientes => "PENDIENTES",
                TipoFiltroConteo.EnProceso => "EN_PROCESO",
                TipoFiltroConteo.PendientesRevision => "PENDIENTE_REVISION",
                TipoFiltroConteo.PrioridadAlta => "PRIORIDAD_ALTA",
                TipoFiltroConteo.Cerrados => "CERRADO",
                _ => string.Empty
            };

            System.Diagnostics.Debug.WriteLine($"Filtro especial asignado: {_filtroEspecial}");

            // Ajustar el filtro de estado según el tipo de filtro solicitado
            switch (_filtroEspecial)
            {
                case "PENDIENTES":
                    // Para pendientes, necesitamos filtrar por PLANIFICADO, ASIGNADO o EN_PROCESO
                    // Como el estado solo acepta uno, usaremos "TODOS" y el filtro especial lo manejará
                    EstadoFiltro = "TODOS";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a TODOS para pendientes");
                    break;
                case "EN_PROCESO":
                    EstadoFiltro = "EN_PROCESO";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a EN_PROCESO");
                    break;
                case "PENDIENTE_REVISION":
                    EstadoFiltro = "PENDIENTE_REVISION";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a PENDIENTE_REVISION");
                    break;
                case "PRIORIDAD_ALTA":
                    // Para prioridad alta, mantener TODOS y filtrar por prioridad
                    EstadoFiltro = "TODOS";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a TODOS para prioridad alta");
                    break;
                case "CERRADO":
                    EstadoFiltro = "CERRADO";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a CERRADO");
                    break;
                default:
                    EstadoFiltro = "TODOS";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a TODOS");
                    break;
            }

            // Asegurar que se muestren solo los propios conteos del usuario
            VerTodosLosConteos = false;

            // Cuando se navega desde WelcomeView, NO modificar las fechas visuales
            // En su lugar, marcaremos que hay un filtro especial activo
            // y CargarControles pasará null como fechas al backend para no filtrar por fecha

            // Forzar notificación de cambio de propiedad
            OnPropertyChanged(nameof(EstadoFiltro));
            OnPropertyChanged(nameof(VerTodosLosConteos));

            _ajustandoFiltrosDesdeEvento = false;

            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));

            // SIEMPRE recargar los datos con el filtro aplicado
            // Si está cargando (por ejemplo, cargando almacenes), esperar a que termine
            if (IsCargando)
            {
                // Esperar a que termine la carga actual (máximo 5 segundos)
                var intentos = 0;
                while (IsCargando && intentos < 50)
                {
                    await Task.Delay(100);
                    intentos++;
                }
            }
            
            System.Diagnostics.Debug.WriteLine("Cargando datos con filtro aplicado...");
            MensajeEstado = "Cargando órdenes de conteo...";
            await CargarControles();
            System.Diagnostics.Debug.WriteLine($"Datos cargados con filtro. Total órdenes: {OrdenesConteo?.Count}");
            OrdenesConteoView?.Refresh();
        }

        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

                AlmacenesCombo.Clear();

                // Añadir opción "Todas"
                AlmacenesCombo.Add(new AlmacenDto
                {
                    CodigoAlmacen = "Todas",
                    NombreAlmacen = "Todas",
                    CodigoEmpresa = empresa
                });

                foreach (var a in resultado)
                    AlmacenesCombo.Add(a);

                AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al cargar almacenes: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        private bool FiltroOrden(object item)
        {
            if (item is not OrdenConteoDto orden) return false;

            // Filtro por ID Orden (búsqueda parcial en el título)
            if (!string.IsNullOrWhiteSpace(IdOrdenFiltro))
            {
                if (!orden.Titulo.Contains(IdOrdenFiltro, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Filtro por almacén (solo si hay un almacén seleccionado y no es "Todas")
            if (AlmacenSeleccionadoCombo != null && 
                !string.IsNullOrEmpty(AlmacenSeleccionadoCombo.CodigoAlmacen) &&
                AlmacenSeleccionadoCombo.CodigoAlmacen != "Todas" &&
                orden.CodigoAlmacen != AlmacenSeleccionadoCombo.CodigoAlmacen)
                return false;

            // Filtro por estado o filtro especial
            if (!string.IsNullOrEmpty(_filtroEspecial))
            {
                switch (_filtroEspecial)
                {
                    case "PENDIENTES":
                        // Pendientes: PLANIFICADO, ASIGNADO o EN_PROCESO
                        if (orden.Estado != "PLANIFICADO" && 
                            orden.Estado != "ASIGNADO" && 
                            orden.Estado != "EN_PROCESO")
                            return false;
                        break;
                    case "PRIORIDAD_ALTA":
                        // Prioridad alta: PLANIFICADO o ASIGNADO con prioridad >= 4
                        if ((orden.Estado != "PLANIFICADO" && orden.Estado != "ASIGNADO") || 
                            orden.Prioridad < 4)
                            return false;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(EstadoFiltro) && 
                     EstadoFiltro != "TODOS" && 
                     orden.Estado != EstadoFiltro)
                return false;

            // Filtro por fechas (solo si no hay filtro especial)
            // Cuando hay filtro especial, el backend ya no filtró por fechas, así que tampoco lo hacemos aquí
            if (string.IsNullOrEmpty(_filtroEspecial))
            {
                if (orden.FechaCreacion.Date < FechaDesde.Date || 
                    orden.FechaCreacion.Date > FechaHasta.Date)
                    return false;
            }

            return true;
        }

        private string ObtenerNombreEmpresaActual()
        {
            return SessionManager.EmpresaSeleccionadaNombre ?? "Empresa no seleccionada";
        }


        partial void OnFechaDesdeChanged(DateTime value)
        {
            // Si la fecha hasta es anterior a la nueva fecha desde, ajustarla
            if (FechaHasta < value)
            {
                FechaHasta = value;
            }
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnFechaHastaChanged(DateTime value)
        {
            // Si la fecha hasta es anterior a la fecha desde, ajustarla
            if (value < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        private async Task CargarOperarios()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosDisponibles.Clear();
                OperariosCombo.Clear();
                
                // Agregar opción "Todos" al combo de filtro
                OperariosCombo.Add(new OperariosAccesoDto
                {
                    Operario = 0,
                    NombreOperario = "TODOS",
                    Contraseña = "",
                    MRH_CodigoAplicacion = 0
                });
                
                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                    OperariosCombo.Add(operario);
                }

                // Seleccionar "Todos" por defecto en el filtro solo si hay elementos
                if (OperariosCombo.Any())
                {
                    OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                }

                // Seleccionar el operario actual por defecto para supervisión
                var operarioActual = SessionManager.UsuarioActual?.operario;
                if (operarioActual.HasValue)
                {
                    OperarioAprobadorSeleccionado = OperariosDisponibles.FirstOrDefault(o => o.Operario == operarioActual.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // En caso de error, asegurar que las colecciones estén vacías
                OperariosDisponibles.Clear();
                OperariosCombo.Clear();
            }
        }

        private bool FiltroResultado(object item)
        {
            if (item is not ResultadoConteoDetalladoDto resultado) return false;

            // Solo mostrar los que requieren reasignación (SUPERVISION sin AprobadoPorCodigo)
            if (!resultado.RequiereAprobacion)
                return false;

            // Filtro por usuario actual (solo ver los propios a menos que se marque "Ver todos")
            if (!VerTodosLosResultadosSupervision)
            {
                var operarioActual = SessionManager.Operario;
                if (operarioActual > 0)
                {
                    // Filtrar por el creador de la orden
                    if (!string.IsNullOrEmpty(resultado.CreadoPorCodigo))
                    {
                        if (!resultado.CreadoPorCodigo.Equals(operarioActual.ToString(), StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else
                    {
                        // Si no hay CreadoPorCodigo, no mostrar
                        return false;
                    }
                }
            }

            // Filtro por fecha de evaluación
            if (FechaDesdeSupervision.HasValue)
            {
                if (resultado.FechaEvaluacion.Date < FechaDesdeSupervision.Value.Date)
                    return false;
            }

            if (FechaHastaSupervision.HasValue)
            {
                if (resultado.FechaEvaluacion.Date > FechaHastaSupervision.Value.Date)
                    return false;
            }

            // Filtro por diferencia
            if (DiferenciaDesdeSupervision.HasValue)
            {
                if (resultado.Diferencia < DiferenciaDesdeSupervision.Value)
                    return false;
            }

            if (DiferenciaHastaSupervision.HasValue)
            {
                if (resultado.Diferencia > DiferenciaHastaSupervision.Value)
                    return false;
            }

            // Filtro por almacén
            if (!string.IsNullOrEmpty(AlmacenSupervisionSeleccionado))
            {
                if (!resultado.CodigoAlmacen.Equals(AlmacenSupervisionSeleccionado, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Filtro por ubicación
            if (!string.IsNullOrEmpty(UbicacionSupervisionSeleccionada))
            {
                if (string.IsNullOrEmpty(resultado.CodigoUbicacion) || 
                    !resultado.CodigoUbicacion.Equals(UbicacionSupervisionSeleccionada, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Filtro por operario que hizo el conteo
            if (OperarioSupervisionSeleccionado != null && OperarioSupervisionSeleccionado.Operario > 0)
            {
                if (string.IsNullOrEmpty(resultado.UsuarioCodigo) || 
                    !resultado.UsuarioCodigo.Equals(OperarioSupervisionSeleccionado.Operario.ToString(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }



        partial void OnResultadoSeleccionadoChanged(ResultadoConteoDetalladoDto? value)
        {
            OnPropertyChanged(nameof(PuedeReasignar));
        }

        partial void OnOperarioAprobadorSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(PuedeReasignar));
        }

        partial void OnModoVisualizacionChanged(string value)
        {
            OnPropertyChanged(nameof(MostrandoOrdenes));
            OnPropertyChanged(nameof(MostrandoSupervision));
        }
        
        // Métodos para filtrado de operarios
        private bool FiltraOperarioCombo(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrEmpty(FiltroOperariosCombo)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(operario.NombreOperario, FiltroOperariosCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        private bool FiltraOperarioDisponibles(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrEmpty(FiltroOperariosDisponibles)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(operario.NombreOperario, FiltroOperariosDisponibles, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        private bool FiltraAlmacenes(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesTexto)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // Métodos para filtrado de supervisión
        private bool FiltraAlmacenesSupervisionCombo(object obj)
        {
            if (obj is not string almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesSupervisionCombo)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen, FiltroAlmacenesSupervisionCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        private bool FiltraUbicacionesSupervisionCombo(object obj)
        {
            if (obj is not string ubicacion) return false;
            if (string.IsNullOrEmpty(FiltroUbicacionesSupervisionCombo)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(ubicacion, FiltroUbicacionesSupervisionCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        private bool FiltraOperariosSupervisionCombo(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrEmpty(FiltroOperariosSupervisionCombo)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(operario.NombreOperario, FiltroOperariosSupervisionCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        // Métodos para manejar cambios en filtros
        partial void OnFiltroOperariosComboChanged(string value)
        {
            OperariosComboView?.Refresh();
        }
        
        partial void OnFiltroOperariosDisponiblesChanged(string value)
        {
            OperariosDisponiblesView?.Refresh();
        }
        
        partial void OnFiltroAlmacenesTextoChanged(string value)
        {
            AlmacenesComboView?.Refresh();
        }

        partial void OnFiltroAlmacenesSupervisionComboChanged(string value)
        {
            AlmacenesSupervisionComboView?.Refresh();
        }

        partial void OnFiltroUbicacionesSupervisionComboChanged(string value)
        {
            UbicacionesSupervisionComboView?.Refresh();
        }

        partial void OnFiltroOperariosSupervisionComboChanged(string value)
        {
            OperariosSupervisionComboView?.Refresh();
        }

        partial void OnVerTodosLosResultadosSupervisionChanged(bool value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnFechaDesdeSupervisionChanged(DateTime? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnFechaHastaSupervisionChanged(DateTime? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnDiferenciaDesdeSupervisionChanged(decimal? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnDiferenciaHastaSupervisionChanged(decimal? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnAlmacenSupervisionSeleccionadoChanged(string? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnUbicacionSupervisionSeleccionadaChanged(string? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }

        partial void OnOperarioSupervisionSeleccionadoChanged(OperariosAccesoDto? value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
            OnPropertyChanged(nameof(TieneFiltrosActivosSupervision));
            OnPropertyChanged(nameof(ResumenFiltrosActivosSupervision));
        }
        
        // Comandos para controlar dropdown
        [RelayCommand]
        private void AbrirDropDownCombo()
        {
            // Limpiar el filtro para permitir escribir desde cero
            FiltroOperariosCombo = "";
            IsDropDownOpenCombo = true;
        }
        
        [RelayCommand]
        private void CerrarDropDownCombo()
        {
            IsDropDownOpenCombo = false;
        }
        
        [RelayCommand]
        private void AbrirDropDownDisponibles()
        {
            // Limpiar el filtro para permitir escribir desde cero
            FiltroOperariosDisponibles = "";
            IsDropDownOpenDisponibles = true;
        }
        
        [RelayCommand]
        private void CerrarDropDownDisponibles()
        {
            IsDropDownOpenDisponibles = false;
        }
        
        [RelayCommand]
        private void AbrirDropDownAlmacenes()
        {
            // Limpiar el filtro para permitir escribir desde cero
            FiltroAlmacenesTexto = "";
            IsDropDownOpenAlmacenes = true;
        }
        
        [RelayCommand]
        private void CerrarDropDownAlmacenes()
        {
            IsDropDownOpenAlmacenes = false;
        }

        // Comandos para controlar dropdown de supervisión
        [RelayCommand]
        private void AbrirDropDownAlmacenesSupervision()
        {
            FiltroAlmacenesSupervisionCombo = "";
            IsDropDownOpenAlmacenesSupervision = true;
        }

        [RelayCommand]
        private void CerrarDropDownAlmacenesSupervision()
        {
            IsDropDownOpenAlmacenesSupervision = false;
        }

        [RelayCommand]
        private void AbrirDropDownUbicacionesSupervision()
        {
            FiltroUbicacionesSupervisionCombo = "";
            IsDropDownOpenUbicacionesSupervision = true;
        }

        [RelayCommand]
        private void CerrarDropDownUbicacionesSupervision()
        {
            IsDropDownOpenUbicacionesSupervision = false;
        }

        [RelayCommand]
        private void AbrirDropDownOperariosSupervision()
        {
            FiltroOperariosSupervisionCombo = "";
            IsDropDownOpenOperariosSupervision = true;
        }

        [RelayCommand]
        private void CerrarDropDownOperariosSupervision()
        {
            IsDropDownOpenOperariosSupervision = false;
        }

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEnableInputs));
            OnPropertyChanged(nameof(CanCargarControles));
            OnPropertyChanged(nameof(PuedeReasignar));
        }
        #endregion
    }
} 