using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using System.Collections.ObjectModel;
using SGA_Desktop.Helpers;
using System.Windows;
using System.Linq;
using System.Windows.Data;
using System.ComponentModel;

namespace SGA_Desktop.ViewModels
{
    public partial class OrdenTraspasoViewModel : ObservableObject
    {
        private readonly OrdenTraspasoService _ordenTraspasoService;
        private readonly StockService _stockService;

        [ObservableProperty]
        private ObservableCollection<OrdenTraspasoDto> ordenesTraspaso = new();

        public ICollectionView OrdenesView { get; private set; }

        [ObservableProperty]
        private OrdenTraspasoDto? ordenSeleccionada;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool isCargando;

        [ObservableProperty]
        private string mensajeEstado = "Cargando órdenes...";

        [ObservableProperty]
        private bool canEnableInputs = true;

        [ObservableProperty]
        private bool canCargarOrdenes = true;

        // Filtros
        [ObservableProperty]
        private ObservableCollection<AlmacenDto> almacenesCombo = new();

        [ObservableProperty]
        private AlmacenDto? almacenDestinoSeleccionado;

        [ObservableProperty]
        private DateTime fechaDesde = DateTime.Today.AddDays(-2);

        [ObservableProperty]
        private DateTime fechaHasta = DateTime.Today.AddDays(1).AddSeconds(-1);

        [ObservableProperty]
        private string estadoFiltro = "TODOS";

        public string TotalOrdenes
        {
            get
            {
                var total = OrdenesTraspaso?.Count ?? 0;
                return $"Total: {total} orden{(total != 1 ? "es" : "")} de traspaso";
            }
        }

        public OrdenTraspasoViewModel()
        {
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoViewModel: Constructor iniciado");
            _ordenTraspasoService = new OrdenTraspasoService();
            _stockService = new StockService();
            
            // Inicializar ICollectionView para filtrado
            OrdenesView = CollectionViewSource.GetDefaultView(OrdenesTraspaso);
            OrdenesView.Filter = FiltrarOrdenes;
            
            // Suscribirse a solicitudes de filtro
            OrdenTraspasoFiltroStore.FiltroSolicitado += OnFiltroSolicitado;
            
            CargarDatosIniciales();
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoViewModel: Datos iniciales cargados");
            _ = InitializeAsync();
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoViewModel: Constructor completado");
        }

        private void CargarDatosIniciales()
        {
            // Los almacenes se cargarán desde la API en InitializeAsync
            // Las fechas ya están inicializadas en las propiedades
        }

        private async Task InitializeAsync()
        {
            try
            {
                await CargarAlmacenesAsync();
                
                // Cargar las órdenes PRIMERO
                await LoadOrdenesTraspasoAsync();
                
                // DESPUÉS de cargar los datos, aplicar cualquier filtro pendiente
                // Esto asegura que el filtro se aplique sobre datos reales, no una colección vacía
                if (!string.IsNullOrEmpty(_filtroEspecial))
                {
                    System.Diagnostics.Debug.WriteLine($"Aplicando filtro especial después de carga inicial: {_filtroEspecial}");
                    OrdenesView?.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en InitializeAsync: {ex.Message}");
            }
        }

        private async Task CargarAlmacenesAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
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

                AlmacenDestinoSeleccionado = AlmacenesCombo.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar almacenes: {ex.Message}");
                // En caso de error, agregar almacenes de prueba
                AlmacenesCombo.Clear();
                AlmacenesCombo.Add(new AlmacenDto { CodigoAlmacen = "Todas", NombreAlmacen = "Todas", CodigoEmpresa = 1 });
                AlmacenesCombo.Add(new AlmacenDto { CodigoAlmacen = "01", NombreAlmacen = "Almacén Principal", CodigoEmpresa = 1 });
                AlmacenesCombo.Add(new AlmacenDto { CodigoAlmacen = "02", NombreAlmacen = "Almacén Secundario", CodigoEmpresa = 1 });
                AlmacenDestinoSeleccionado = AlmacenesCombo.FirstOrDefault();
            }
        }

        // Validación de fechas y actualización de filtros
        partial void OnFechaDesdeChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la nueva fecha desde, ajustarla
            if (FechaHasta < newValue)
            {
                FechaHasta = newValue;
            }
            // Limpiar filtro especial si el usuario cambia fechas manualmente (no desde evento)
            if (!_ajustandoFiltrosDesdeEvento)
            {
                _filtroEspecial = string.Empty;
            }
            OrdenesView?.Refresh();
        }

        partial void OnFechaHastaChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la fecha desde, ajustarla
            if (newValue < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
            // Limpiar filtro especial si el usuario cambia fechas manualmente (no desde evento)
            if (!_ajustandoFiltrosDesdeEvento)
            {
                _filtroEspecial = string.Empty;
            }
            OrdenesView?.Refresh();
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            // Limpiar filtro especial si el usuario cambia almacén manualmente (no desde evento)
            if (!_ajustandoFiltrosDesdeEvento)
            {
                _filtroEspecial = string.Empty;
            }
            OrdenesView?.Refresh();
        }

        partial void OnEstadoFiltroChanged(string oldValue, string newValue)
        {
            // Limpiar filtro especial si el usuario cambia estado manualmente (no desde evento)
            if (!_ajustandoFiltrosDesdeEvento)
            {
                _filtroEspecial = string.Empty;
            }
            OrdenesView?.Refresh();
        }

        [RelayCommand]
        public async Task LoadOrdenesTraspasoAsync()
        {
            try
            {
                IsLoading = true;
                IsCargando = true;
                MensajeEstado = "Cargando órdenes...";
                
                var ordenes = await _ordenTraspasoService.GetOrdenesTraspasoAsync();
                
                // Guardar el filtro especial actual antes de limpiar
                var filtroEspecialActual = _filtroEspecial;
                
                OrdenesTraspaso.Clear();
                
                foreach (var orden in ordenes)
                {
                    OrdenesTraspaso.Add(orden);
                }
                
                // Restaurar el filtro especial después de recargar
                _filtroEspecial = filtroEspecialActual;
                
                // Refrescar la vista filtrada
                OrdenesView.Refresh();
                OnPropertyChanged(nameof(TotalOrdenes));
                
                // Si no hay órdenes, mostrar mensaje
                if (OrdenesTraspaso.Count == 0)
                {
                    MensajeEstado = "No se encontraron órdenes de traspaso";
                }
                else
                {
                    MensajeEstado = $"{OrdenesTraspaso.Count} órdenes cargadas. Filtro activo: {_filtroEspecial}";
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error al cargar órdenes de traspaso: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                IsCargando = false;
            }
        }


        [RelayCommand]
        private async Task CargarOrdenes()
        {
            await LoadOrdenesTraspasoAsync();
        }

        [RelayCommand]
        private void CrearOrden()
        {
            var dialog = new CrearOrdenTraspasoDialog();
            
            // Establecer el owner para que se centre correctamente
            var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                       ?? System.Windows.Application.Current.MainWindow;
            if (owner != null && owner != dialog)
                dialog.Owner = owner;
            
            var result = dialog.ShowDialog();
            
            // Recargar órdenes independientemente del resultado
            _ = LoadOrdenesTraspasoAsync();
        }

        [RelayCommand]
        private void VerOrden(OrdenTraspasoDto orden)
        {
            try
            {
                var dialog = new VerOrdenTraspasoDialog(orden);
                
                // Establecer la ventana padre
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                    dialog.Owner = owner;
                
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al abrir detalles de la orden: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void EditarOrden(OrdenTraspasoDto orden)
        {
            try
            {
                // Crear el ViewModel de edición con la orden seleccionada
                var editarViewModel = new EditarOrdenTraspasoDialogViewModel(orden);
                
                // Crear y mostrar el diálogo de edición
                var editarDialog = new EditarOrdenTraspasoDialog(editarViewModel);
                editarDialog.Owner = Application.Current.MainWindow;
                editarDialog.ShowDialog();
                
                // Recargar las órdenes después de cerrar el diálogo
                CargarOrdenesCommand.Execute(null);
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error al abrir edición", 
                    $"No se pudo abrir el diálogo de edición: {ex.Message}", 
                    "Aceptar");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task CancelarOrden(OrdenTraspasoDto orden)
        {
            try
            {
                // Verificar si la orden se puede cancelar antes de intentar
                if (orden.Estado != "PENDIENTE" && orden.Estado != "SIN_ASIGNAR" && orden.Estado != "EN_PROCESO")
                {
                    var warningDialog = new WarningDialog(
                        "No se puede cancelar",
                        $"No se puede cancelar la orden {orden.CodigoOrden} porque está en estado '{orden.EstadoTexto}'.\n\n" +
                        "Solo se pueden cancelar órdenes en estado 'Pendiente', 'Sin Asignar' o 'En Proceso'.",
                        "Aceptar");
                    warningDialog.ShowDialog();
                    return;
                }

                // Verificar si hay líneas en proceso
                var tieneLineasEnProceso = orden.Lineas.Any(l => l.Estado == "EN_PROCESO");
                if (tieneLineasEnProceso)
                {
                    // Si hay líneas en proceso, ofrecer cancelar solo las líneas pendientes
                    var confirmacionProceso = new ConfirmationDialog(
                        "Orden en proceso",
                        $"La orden {orden.CodigoOrden} ya ha comenzado y tiene líneas en proceso.\n\n" +
                        "¿Desea cancelar solo las líneas que no han comenzado?\n\n" +
                        "Las líneas en proceso deben completarse.");

                    if (confirmacionProceso.ShowDialog() == true)
                    {
                        await CancelarLineasPendientes(orden);
                    }
                    return;
                }

                // Verificar si hay movimientos realizados
                var tieneMovimientos = orden.Lineas.Any(l => l.CantidadMovida > 0);
                if (tieneMovimientos)
                {
                    var warningDialog = new WarningDialog(
                        "No se puede cancelar",
                        $"No se puede cancelar la orden {orden.CodigoOrden} porque ya tiene movimientos realizados.\n\n" +
                        "Debe completar la orden en lugar de cancelarla.",
                        "Aceptar");
                    warningDialog.ShowDialog();
                    return;
                }

                // Confirmar cancelación
                var confirmacionCancelacion = new ConfirmationDialog(
                    "Confirmar cancelación",
                    $"¿Está seguro de que desea cancelar la orden '{orden.CodigoOrden}'?\n\n" +
                    "Todas las líneas pendientes se marcarán como canceladas.");

                if (confirmacionCancelacion.ShowDialog() == true)
                {
                    var result = await _ordenTraspasoService.CancelarOrdenTraspasoAsync(orden.IdOrdenTraspaso);
                    if (result)
                    {
                        var successDialog = new WarningDialog(
                            "Éxito",
                            "Orden cancelada correctamente.",
                            "Aceptar");
                        successDialog.ShowDialog();
                        await LoadOrdenesTraspasoAsync();
                    }
                    else
                    {
                        var errorDialog = new WarningDialog(
                            "Error",
                            "Error al cancelar la orden. Verifique que cumple las condiciones necesarias.",
                            "Aceptar");
                        errorDialog.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al cancelar la orden: {ex.Message}",
                    "Aceptar");
                errorDialog.ShowDialog();
            }
        }

        private async Task CancelarLineasPendientes(OrdenTraspasoDto orden)
        {
            try
            {
                var result = await _ordenTraspasoService.CancelarLineasPendientesAsync(orden.IdOrdenTraspaso);
                if (result)
                {
                    var successDialog = new WarningDialog(
                        "Líneas canceladas",
                        $"Se han cancelado las líneas pendientes de la orden {orden.CodigoOrden}.\n\n" +
                        "Las líneas en proceso deben completarse.",
                        "Aceptar");
                    successDialog.ShowDialog();
                    await LoadOrdenesTraspasoAsync();
                }
                else
                {
                    var errorDialog = new WarningDialog(
                        "Error",
                        "No se pudieron cancelar las líneas pendientes.\n\n" +
                        "Verifique que la orden esté en estado EN_PROCESO y tenga líneas pendientes.",
                        "Aceptar");
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al cancelar las líneas pendientes: {ex.Message}",
                    "Aceptar");
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void ExportarOrdenes()
        {
            // TODO: Implementar exportación a Excel
            System.Diagnostics.Debug.WriteLine("Exportar órdenes a Excel");
        }

        // Propiedad calculada para mostrar el texto de la prioridad
        public string GetPrioridadTexto(short prioridad)
        {
            return prioridad switch
            {
                1 => "1 - Muy Baja",
                2 => "2 - Baja", 
                3 => "3 - Normal",
                4 => "4 - Alta",
                5 => "5 - Muy Alta",
                _ => $"{prioridad} - Desconocida"
            };
        }

        // Método de filtrado para ICollectionView
        private bool FiltrarOrdenes(object obj)
        {
            if (obj is not OrdenTraspasoDto orden) return false;

            // Filtro especial desde dashboard (tiene prioridad y NO filtra por fecha/almacén)
            if (!string.IsNullOrEmpty(_filtroEspecial))
            {
                var idOperarioActual = SessionManager.UsuarioActual?.operario ?? 0;

                switch (_filtroEspecial)
                {
                    case "PENDIENTES":
                        // Solo mostrar estado PENDIENTE
                        if (orden.Estado != "PENDIENTE")
                            return false;
                        break;
                    case "EN_PROCESO":
                        // Solo mostrar estado EN_PROCESO
                        if (orden.Estado != "EN_PROCESO")
                            return false;
                        break;
                    case "PRIORIDAD_ALTA":
                        // Solo PENDIENTES con prioridad alta (>= 4)
                        if (orden.Estado != "PENDIENTE" || orden.Prioridad < 4)
                            return false;
                        break;
                    case "ASIGNADAS_A_MI":
                        // Solo PENDIENTES con líneas asignadas al operario actual
                        var tieneLineasAsignadas = orden.Lineas.Any(l => l.IdOperarioAsignado == idOperarioActual && l.IdOperarioAsignado != 0);
                        if (orden.Estado != "PENDIENTE" || !tieneLineasAsignadas)
                            return false;
                        break;
                    case "SIN_ASIGNAR":
                        // Solo estado SIN_ASIGNAR
                        if (orden.Estado != "SIN_ASIGNAR")
                            return false;
                        break;
                }
            }
            else
            {
                // Filtros normales (cuando NO hay filtro especial)
                
                // Filtro por fecha
                if (orden.FechaCreacion.Date < FechaDesde.Date || orden.FechaCreacion.Date > FechaHasta.Date)
                    return false;

                // Filtro por almacén destino
                if (AlmacenDestinoSeleccionado != null && 
                    AlmacenDestinoSeleccionado.CodigoAlmacen != "Todas" && 
                    !string.IsNullOrEmpty(orden.CodigoAlmacenDestino) &&
                    orden.CodigoAlmacenDestino != AlmacenDestinoSeleccionado.CodigoAlmacen)
                    return false;

                // Filtro por estado
                if (EstadoFiltro != "TODOS" && orden.Estado != EstadoFiltro)
                    return false;
            }

            return true;
        }

        private string _filtroEspecial = string.Empty;
        private bool _ajustandoFiltrosDesdeEvento = false;

        private async void OnFiltroSolicitado(object? sender, FiltroOrdenTraspasoEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"OnFiltroSolicitado recibido: {e.TipoFiltro}");
            
            _ajustandoFiltrosDesdeEvento = true;

            // Aplicar el filtro especial
            _filtroEspecial = e.TipoFiltro switch
            {
                TipoFiltroOrden.TodasPendientes => "PENDIENTES",
                TipoFiltroOrden.EnProceso => "EN_PROCESO",
                TipoFiltroOrden.PrioridadAlta => "PRIORIDAD_ALTA",
                TipoFiltroOrden.AsignadasAMi => "ASIGNADAS_A_MI",
                TipoFiltroOrden.SinAsignar => "SIN_ASIGNAR",
                _ => string.Empty
            };

            System.Diagnostics.Debug.WriteLine($"Filtro especial asignado: {_filtroEspecial}");

            // NO ajustar fechas ni almacén cuando hay filtro especial
            // El filtro especial ignora fecha/almacén y solo filtra por estado

            // Ajustar el filtro de estado para que sea compatible con el filtro especial
            switch (_filtroEspecial)
            {
                case "PENDIENTES":
                case "PRIORIDAD_ALTA":
                case "ASIGNADAS_A_MI":
                    // Todos estos filtros solo muestran PENDIENTES
                    EstadoFiltro = "PENDIENTE";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a PENDIENTE");
                    break;
                case "EN_PROCESO":
                    EstadoFiltro = "EN_PROCESO";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a EN_PROCESO");
                    break;
                case "SIN_ASIGNAR":
                    EstadoFiltro = "SIN_ASIGNAR";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a SIN_ASIGNAR");
                    break;
                default:
                    EstadoFiltro = "TODOS";
                    System.Diagnostics.Debug.WriteLine("EstadoFiltro establecido a TODOS");
                    break;
            }

            // Forzar notificación de cambio de propiedad
            OnPropertyChanged(nameof(EstadoFiltro));

            _ajustandoFiltrosDesdeEvento = false;

            // SOLUCIÓN CORRECTA: Si no hay datos cargados, cargar los datos AHORA con el filtro aplicado
            if (OrdenesTraspaso?.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No hay datos cargados, cargando datos con filtro aplicado...");
                await LoadOrdenesTraspasoAsync();
                System.Diagnostics.Debug.WriteLine($"Datos cargados con filtro. Total órdenes: {OrdenesTraspaso?.Count}");
            }
            else
            {
                // Si ya hay datos cargados, solo aplicar el filtro
                OrdenesView?.Refresh();
                System.Diagnostics.Debug.WriteLine($"Filtro aplicado sobre {OrdenesTraspaso.Count} órdenes existentes");
            }

            System.Diagnostics.Debug.WriteLine($"Filtro configurado completamente. Estado: {EstadoFiltro}, Especial: {_filtroEspecial}");
        }
    }
} 