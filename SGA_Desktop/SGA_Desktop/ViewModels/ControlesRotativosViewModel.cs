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
            OrdenesConteo = new ObservableCollection<OrdenConteoDto>();
            ResultadosSupervision = new ObservableCollection<ResultadoConteoDetalladoDto>();
            OperariosDisponibles = new ObservableCollection<OperariosAccesoDto>();

            OrdenesConteoView = CollectionViewSource.GetDefaultView(OrdenesConteo);
            OrdenesConteoView.Filter = new Predicate<object>(FiltroOrden);

            ResultadosView = CollectionViewSource.GetDefaultView(ResultadosSupervision);
            ResultadosView.Filter = new Predicate<object>(FiltroResultado);

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
        public ObservableCollection<OrdenConteoDto> OrdenesConteo { get; }
        public ObservableCollection<string> EstadosCombo { get; }
        public ObservableCollection<ResultadoConteoDetalladoDto> ResultadosSupervision { get; }
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; }
        public ICollectionView ResultadosView { get; }

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionadoCombo;

        [ObservableProperty]
        private OrdenConteoDto? ordenSeleccionada;

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;

        [ObservableProperty]
        private DateTime fechaDesde = DateTime.Today.AddDays(-2);

        [ObservableProperty]
        private DateTime fechaHasta = DateTime.Today;

        [ObservableProperty]
        private string estadoFiltro = "TODOS";

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
        #endregion

        #region Commands
        [RelayCommand]
        private async Task CrearControlRotativo()
        {
            try
            {
                // Crear el ViewModel del diálogo
                var dialogViewModel = new CrearOrdenConteoDialogViewModel(_conteosService, _stockService, new LoginService());
                
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
        private async Task CargarControles()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando órdenes de conteo...";

                // Filtrar por estado si no es "TODOS"
                var estadoFiltro = EstadoFiltro == "TODOS" ? null : EstadoFiltro;

                var ordenes = await _conteosService.ListarOrdenesAsync(null, estadoFiltro);

                // Asegurar que tenemos los operarios cargados para el mapeo de nombres
                if (OperariosDisponibles.Count == 0)
                {
                    await CargarOperarios();
                }

                // Crear diccionario para mapear códigos a nombres de operarios
                var operariosDict = OperariosDisponibles
                    .Where(op => op.Operario > 0) // Excluir "Sin asignar"
                    .ToDictionary(op => op.Operario.ToString(), op => op.NombreCompleto);

                OrdenesConteo.Clear();
                foreach (var orden in ordenes)
                {
                    // Mapear nombre del operario si existe
                    if (!string.IsNullOrEmpty(orden.CodigoOperario) && 
                        operariosDict.TryGetValue(orden.CodigoOperario, out var nombreOperario))
                    {
                        orden.NombreOperario = nombreOperario;
                    }
                    
                    OrdenesConteo.Add(orden);
                }

                OrdenesConteoView.Refresh();
                OnPropertyChanged(nameof(TotalOrdenes));

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

            var mensaje = $"ORDEN DE CONTEO\n\n" +
                         $"Título: {orden.Titulo}\n" +
                         $"GUID: {orden.GuidID}\n" +
                         $"Estado: {orden.EstadoFormateado}\n" +
                         $"Alcance: {orden.AlcanceFormateado}\n" +
                         $"Prioridad: {orden.PrioridadTexto}\n\n" +
                         $"INFORMACIÓN DE EMPRESA Y ALMACÉN\n" +
                         $"Empresa: {orden.CodigoEmpresa}\n" +
                         $"Almacén: {orden.CodigoAlmacen ?? "N/A"}\n" +
                         $"Ubicación: {orden.CodigoUbicacion ?? "N/A"}\n" +
                         $"Artículo: {orden.CodigoArticulo ?? "N/A"}\n\n" +
                         $"ASIGNACIÓN Y FECHAS\n" +
                         $"Operario: {orden.CodigoOperario ?? "Sin asignar"}\n" +
                         $"Creado por: {orden.CreadoPorCodigo}\n" +
                         $"Fecha Plan: {orden.FechaPlan?.ToString("dd/MM/yyyy") ?? "N/A"}\n" +
                         $"Fecha Creación: {orden.FechaCreacion:dd/MM/yyyy HH:mm}\n" +
                         $"Fecha Asignación: {orden.FechaAsignacion?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"}\n" +
                         $"Fecha Inicio: {orden.FechaInicio?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"}\n" +
                         $"Fecha Cierre: {orden.FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "N/A"}\n\n" +
                         $"COMENTARIOS\n" +
                         $"{(string.IsNullOrEmpty(orden.Comentario) ? "Sin comentarios" : orden.Comentario)}";

            var dialog = new WarningDialog("Ver Orden de Conteo", mensaje, "\uE946"); // Ícono de información
            ShowCenteredDialog(dialog);
        }

        [RelayCommand]
        private void EditarOrden(OrdenConteoDto orden)
        {
            if (orden == null) return;

            var dialog = new WarningDialog(
                "Editar Orden de Conteo", 
                $"Funcionalidad para editar la orden '{orden.Titulo}' (GUID: {orden.GuidID}) en desarrollo.");
            dialog.ShowDialog();
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

        partial void OnAlmacenSeleccionadoComboChanged(AlmacenDto? value)
        {
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
            OnPropertyChanged(nameof(CanCargarControles));
        }

        partial void OnEstadoFiltroChanged(string value)
        {
            OrdenesConteoView?.Refresh();
            OnPropertyChanged(nameof(TotalOrdenes));
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
        }

        private async Task CargarOperarios()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosDisponibles.Clear();
                
                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }

                // Seleccionar el operario actual por defecto
                var operarioActual = SessionManager.UsuarioActual?.operario;
                if (operarioActual.HasValue)
                {
                    OperarioAprobadorSeleccionado = OperariosDisponibles.FirstOrDefault(o => o.Operario == operarioActual.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cargando operarios: {ex.Message}");
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

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEnableInputs));
            OnPropertyChanged(nameof(CanCargarControles));
            OnPropertyChanged(nameof(PuedeReasignar));
        }
        #endregion
    }
} 