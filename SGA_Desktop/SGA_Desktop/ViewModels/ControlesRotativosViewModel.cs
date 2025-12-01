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

            EstadosCombo = new ObservableCollection<string>
            {
                "TODOS",
                "PLANIFICADO", 
                "ASIGNADO",
                "EN_PROCESO",
                "CERRADO",
                "CANCELADO"
            };

            EstadoFiltro = "TODOS";
            ModoVisualizacion = "ORDENES"; // Por defecto mostrar órdenes

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

        [ObservableProperty]
        private string filtroArticuloSupervision = string.Empty;

        [ObservableProperty]
        private string filtroAlmacenSupervision = string.Empty;
        
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
        #endregion

        #region Computed Properties
        public bool CanEnableInputs => !IsCargando;
        public bool CanCargarControles => !IsCargando && AlmacenSeleccionadoCombo != null;
        
        // Propiedades calculadas para supervisión
        public bool MostrandoOrdenes => ModoVisualizacion == "ORDENES";
        public bool MostrandoSupervision => ModoVisualizacion == "SUPERVISION";
        public int TotalResultados => ResultadosSupervision?.Count ?? 0;
        public int ResultadosPendientes => ResultadosSupervision?.Count(r => r.RequiereAprobacion) ?? 0;
        public bool PuedeReasignar => ResultadoSeleccionado != null && 
                                   ResultadoSeleccionado.RequiereAprobacion && 
                                   OperarioAprobadorSeleccionado != null &&
                                   OperarioAprobadorSeleccionado.Operario != 0;

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
                // Siempre mostrar fechas como filtro activo (incluso si son los valores por defecto)
                var fechasActivas = true; // Siempre hay un rango de fechas aplicado

                return almacenActivo || estadoActivo || operarioActivo || fechasActivas;
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
                    OperarioSeleccionadoCombo
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

                    // Notificar cambios en propiedades calculadas
                    OnPropertyChanged(nameof(TieneFiltrosActivos));
                    OnPropertyChanged(nameof(ResumenFiltrosActivos));

                    // Recargar los controles con los nuevos filtros
                    await CargarControles();
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
                
                // Pasar las fechas al servicio para filtrar en el backend
                var ordenes = await _conteosService.ListarTodasLasOrdenesAsync(
                    estadoFiltro, 
                    operarioFiltro,
                    FechaDesde,
                    FechaHasta);
                
                Debug.WriteLine($"CargarControles - Se obtuvieron {ordenes.Count} órdenes");

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
        }

        [RelayCommand]
        private async Task CambiarASupervision()
        {
            ModoVisualizacion = "SUPERVISION";
            OnPropertyChanged(nameof(MostrandoOrdenes));
            OnPropertyChanged(nameof(MostrandoSupervision));
            
            // Cargar datos de supervisión si es la primera vez
            if (ResultadosSupervision.Count == 0)
            {
                await CargarResultadosSupervision();
                await CargarOperarios();
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

                ResultadosSupervision.Clear();
                foreach (var resultado in resultados.OrderByDescending(r => r.FechaEvaluacion))
                {
                    ResultadosSupervision.Add(resultado);
                }

                ResultadosView.Refresh();
                OnPropertyChanged(nameof(TotalResultados));
                OnPropertyChanged(nameof(ResultadosPendientes));

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
            FiltroArticuloSupervision = string.Empty;
            FiltroAlmacenSupervision = string.Empty;
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
                
                // Cargar órdenes de conteo automáticamente
                MensajeEstado = "Cargando órdenes de conteo...";
                await CargarControles();
                
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

        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

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

            // Filtro por almacén (solo si hay un almacén seleccionado y no es "Todas")
            if (AlmacenSeleccionadoCombo != null && 
                !string.IsNullOrEmpty(AlmacenSeleccionadoCombo.CodigoAlmacen) &&
                AlmacenSeleccionadoCombo.CodigoAlmacen != "Todas" &&
                orden.CodigoAlmacen != AlmacenSeleccionadoCombo.CodigoAlmacen)
                return false;

            // Filtro por estado
            if (!string.IsNullOrEmpty(EstadoFiltro) && 
                EstadoFiltro != "TODOS" && 
                orden.Estado != EstadoFiltro)
                return false;

            // Filtro por fechas
            if (orden.FechaCreacion.Date < FechaDesde.Date || 
                orden.FechaCreacion.Date > FechaHasta.Date)
                return false;

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

            // Filtro por artículo
            if (!string.IsNullOrEmpty(FiltroArticuloSupervision) && 
                !string.IsNullOrEmpty(resultado.CodigoArticulo) &&
                !resultado.CodigoArticulo.Contains(FiltroArticuloSupervision, StringComparison.OrdinalIgnoreCase) &&
                !(resultado.DescripcionArticulo?.Contains(FiltroArticuloSupervision, StringComparison.OrdinalIgnoreCase) ?? false))
                return false;

            // Filtro por almacén
            if (!string.IsNullOrEmpty(FiltroAlmacenSupervision) && 
                !resultado.CodigoAlmacen.Contains(FiltroAlmacenSupervision, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        partial void OnFiltroArticuloSupervisionChanged(string value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
        }

        partial void OnFiltroAlmacenSupervisionChanged(string value)
        {
            ResultadosView?.Refresh();
            OnPropertyChanged(nameof(TotalResultados));
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

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEnableInputs));
            OnPropertyChanged(nameof(CanCargarControles));
            OnPropertyChanged(nameof(PuedeReasignar));
        }
        #endregion
    }
} 