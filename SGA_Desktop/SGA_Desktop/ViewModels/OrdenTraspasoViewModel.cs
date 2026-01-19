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
using System.Collections.Generic;

namespace SGA_Desktop.ViewModels
{
    public partial class OrdenTraspasoViewModel : ObservableObject
    {
        private readonly OrdenTraspasoService _ordenTraspasoService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;

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
        private AlmacenDto? almacenOrigenSeleccionado;

        [ObservableProperty]
        private DateTime fechaDesde = DateTime.Today.AddDays(-7);

        [ObservableProperty]
        private DateTime fechaHasta = DateTime.Today;

        [ObservableProperty]
        private DateTime? fechaPlan;

        [ObservableProperty]
        private string estadoFiltro = "TODOS";

        [ObservableProperty]
        private string prioridadFiltro = "TODAS";

        [ObservableProperty]
        private string codigoOrdenFiltro = string.Empty;

        [ObservableProperty]
        private OperariosAccesoDto? operarioSeleccionadoCombo;

        [ObservableProperty]
        private int? creadorSeleccionado;

        [ObservableProperty]
        private bool verTodasLasOrdenes = false; // Por defecto, solo ver los propios

        // Colecciones para filtros
        public ObservableCollection<OperariosAccesoDto> OperariosCombo { get; } = new();
        public ObservableCollection<int> UsuariosCreacionCombo { get; } = new();
        public ObservableCollection<AlmacenDto> AlmacenesOrigenCombo { get; } = new();

        public ICollectionView OperariosComboView { get; private set; }

        public string TotalOrdenes
        {
            get
            {
                // Contar solo las órdenes que pasan el filtro (las visibles)
                int total = 0;
                if (OrdenesView != null)
                {
                    foreach (var item in OrdenesView)
                    {
                        total++;
                    }
                }
                return $"Total: {total} orden{(total != 1 ? "es" : "")} de traspaso";
            }
        }

        public bool TieneFiltrosActivos
        {
            get
            {
                // Verificar si hay filtros activos
                var almacenDestinoActivo = AlmacenDestinoSeleccionado != null && 
                                           AlmacenDestinoSeleccionado.CodigoAlmacen != "Todas";
                var almacenOrigenActivo = AlmacenOrigenSeleccionado != null && 
                                          AlmacenOrigenSeleccionado.CodigoAlmacen != "Todas";
                var estadoActivo = !string.IsNullOrEmpty(EstadoFiltro) && EstadoFiltro != "TODOS";
                var codigoOrdenActivo = !string.IsNullOrWhiteSpace(CodigoOrdenFiltro);
                var prioridadActiva = !string.IsNullOrEmpty(PrioridadFiltro) && PrioridadFiltro != "TODAS";
                var operarioActivo = OperarioSeleccionadoCombo != null && 
                                     OperarioSeleccionadoCombo.Operario != 0;
                var usuarioCreacionActivo = CreadorSeleccionado.HasValue && 
                                            CreadorSeleccionado.Value != 0;
                var fechaPlanActiva = FechaPlan.HasValue;
                var verTodasActivo = VerTodasLasOrdenes;
                // Siempre mostrar fechas como filtro activo (incluso si son los valores por defecto)
                var fechasActivas = true; // Siempre hay un rango de fechas aplicado
                // También considerar si hay un filtro especial activo
                var filtroEspecialActivo = !string.IsNullOrEmpty(_filtroEspecial);

                return almacenDestinoActivo || almacenOrigenActivo || estadoActivo || codigoOrdenActivo || 
                       prioridadActiva || operarioActivo || usuarioCreacionActivo || fechaPlanActiva || 
                       verTodasActivo || fechasActivas || filtroEspecialActivo;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

                // Siempre mostrar las fechas
                filtros.Add($"Fechas: {FechaDesde:dd/MM/yyyy} - {FechaHasta:dd/MM/yyyy}");

                if (AlmacenDestinoSeleccionado != null && AlmacenDestinoSeleccionado.CodigoAlmacen != "Todas")
                {
                    filtros.Add($"Destino: {AlmacenDestinoSeleccionado.CodigoAlmacen}");
                }

                if (AlmacenOrigenSeleccionado != null && AlmacenOrigenSeleccionado.CodigoAlmacen != "Todas")
                {
                    filtros.Add($"Origen: {AlmacenOrigenSeleccionado.CodigoAlmacen}");
                }

                if (!string.IsNullOrEmpty(EstadoFiltro) && EstadoFiltro != "TODOS")
                {
                    filtros.Add($"Estado: {EstadoFiltro}");
                }

                if (!string.IsNullOrWhiteSpace(CodigoOrdenFiltro))
                {
                    filtros.Add($"Código: {CodigoOrdenFiltro}");
                }

                if (!string.IsNullOrEmpty(PrioridadFiltro) && PrioridadFiltro != "TODAS")
                {
                    filtros.Add($"Prioridad: {PrioridadFiltro}");
                }

                if (OperarioSeleccionadoCombo != null && OperarioSeleccionadoCombo.Operario != 0)
                {
                    filtros.Add($"Operario: {OperarioSeleccionadoCombo.NombreOperario}");
                }

                if (CreadorSeleccionado.HasValue && CreadorSeleccionado.Value != 0)
                {
                    filtros.Add($"Creador: {CreadorSeleccionado.Value}");
                }

                if (FechaPlan.HasValue)
                {
                    filtros.Add($"Plan: {FechaPlan.Value:dd/MM/yyyy}");
                }

                if (VerTodasLasOrdenes)
                {
                    filtros.Add("Ver todas");
                }
                else
                {
                    filtros.Add("Solo propias");
                }

                // Si hay un filtro especial, mostrarlo
                if (!string.IsNullOrEmpty(_filtroEspecial))
                {
                    var textoFiltroEspecial = _filtroEspecial switch
                    {
                        "PENDIENTES" => "Pendientes",
                        "EN_PROCESO" => "En Proceso",
                        "PRIORIDAD_ALTA" => "Prioridad Alta",
                        "ASIGNADAS_A_MI" => "Asignadas a Mí",
                        "SIN_ASIGNAR" => "Sin Asignar",
                        _ => _filtroEspecial
                    };
                    filtros.Add($"Filtro: {textoFiltroEspecial}");
                }

                return string.Join(" | ", filtros);
            }
        }

        public OrdenTraspasoViewModel()
        {
            System.Diagnostics.Debug.WriteLine("OrdenTraspasoViewModel: Constructor iniciado");
            _ordenTraspasoService = new OrdenTraspasoService();
            _stockService = new StockService();
            _loginService = new LoginService();
            
            // Inicializar ICollectionView para filtrado
            OrdenesView = CollectionViewSource.GetDefaultView(OrdenesTraspaso);
            OrdenesView.Filter = FiltrarOrdenes;
            
            // Inicializar ICollectionView para filtrado de operarios
            OperariosComboView = CollectionViewSource.GetDefaultView(OperariosCombo);
            OperariosComboView.Filter = FiltraOperarios;
            
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
                await CargarOperariosAsync();
                await CargarAlmacenesOrigenAsync();
                
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

        private async Task CargarOperariosAsync()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoTraspasosAsync();

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
                    OperariosCombo.Add(operario);
                }

                // Seleccionar "Todos" por defecto
                if (OperariosCombo.Any() && OperarioSeleccionadoCombo == null)
                {
                    OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                OperariosCombo.Clear();
            }
        }

        private async Task CargarAlmacenesOrigenAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

                AlmacenesOrigenCombo.Clear();

                // Añadir opción "Todos"
                AlmacenesOrigenCombo.Add(new AlmacenDto
                {
                    CodigoAlmacen = "Todas",
                    NombreAlmacen = "Todas",
                    CodigoEmpresa = empresa
                });

                foreach (var a in resultado)
                    AlmacenesOrigenCombo.Add(a);

                AlmacenOrigenSeleccionado = AlmacenesOrigenCombo.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar almacenes origen: {ex.Message}");
            }
        }

        private async Task CargarUsuariosCreacionAsync()
        {
            try
            {
                // Cargar usuarios únicos desde las órdenes cargadas
                // Esto se actualizará cuando se carguen las órdenes
                UsuariosCreacionCombo.Clear();
                UsuariosCreacionCombo.Add(0); // Opción "Todos"
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando usuarios creación: {ex.Message}");
            }
        }

        // Método de filtrado para operarios (para el combo en el ViewModel principal si se necesita)
        private bool FiltraOperarios(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            return true; // Sin filtro de texto en el ViewModel principal
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
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
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
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            // Limpiar filtro especial si el usuario cambia almacén manualmente (no desde evento)
            if (!_ajustandoFiltrosDesdeEvento)
            {
                _filtroEspecial = string.Empty;
            }
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnEstadoFiltroChanged(string oldValue, string newValue)
        {
            // Limpiar filtro especial si el usuario cambia estado manualmente (no desde evento)
            if (!_ajustandoFiltrosDesdeEvento)
            {
                _filtroEspecial = string.Empty;
            }
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenOrigenSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnFechaPlanChanged(DateTime? oldValue, DateTime? newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnPrioridadFiltroChanged(string oldValue, string newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnCodigoOrdenFiltroChanged(string oldValue, string newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnOperarioSeleccionadoComboChanged(OperariosAccesoDto? oldValue, OperariosAccesoDto? newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnCreadorSeleccionadoChanged(int? oldValue, int? newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnVerTodasLasOrdenesChanged(bool oldValue, bool newValue)
        {
            OrdenesView?.Refresh();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
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
                
                // Actualizar lista de usuarios creación únicos
                var usuariosUnicos = ordenes.Select(o => o.UsuarioCreacion).Distinct().OrderBy(u => u).ToList();
                UsuariosCreacionCombo.Clear();
                UsuariosCreacionCombo.Add(0); // Opción "Todos"
                foreach (var usuario in usuariosUnicos)
                {
                    UsuariosCreacionCombo.Add(usuario);
                }
                
                // Restaurar el filtro especial después de recargar
                _filtroEspecial = filtroEspecialActual;
                
                // Refrescar la vista filtrada
                OrdenesView.Refresh();
                OnPropertyChanged(nameof(TotalOrdenes));
                OnPropertyChanged(nameof(TieneFiltrosActivos));
                OnPropertyChanged(nameof(ResumenFiltrosActivos));
                
                // Contar órdenes filtradas
                int ordenesFiltradas = 0;
                foreach (var item in OrdenesView)
                {
                    ordenesFiltradas++;
                }
                
                // Si no hay órdenes, mostrar mensaje
                if (OrdenesTraspaso.Count == 0)
                {
                    MensajeEstado = "No se encontraron órdenes de traspaso";
                }
                else
                {
                    MensajeEstado = $"{ordenesFiltradas} de {OrdenesTraspaso.Count} órdenes mostradas";
                    if (!string.IsNullOrEmpty(_filtroEspecial))
                    {
                        MensajeEstado += $" (Filtro: {_filtroEspecial})";
                    }
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
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                           ?? Application.Current.MainWindow;
                if (owner != null && owner != editarDialog)
                    editarDialog.Owner = owner;
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

        [RelayCommand]
        private async Task AbrirFiltros()
        {
            try
            {
                // Crear el ViewModel del diálogo con los valores actuales
                var dialogViewModel = new FiltrosOrdenTraspasoDialogViewModel(
                    AlmacenDestinoSeleccionado,
                    AlmacenOrigenSeleccionado,
                    FechaDesde,
                    FechaHasta,
                    FechaPlan,
                    EstadoFiltro,
                    PrioridadFiltro,
                    CodigoOrdenFiltro,
                    OperarioSeleccionadoCombo,
                    CreadorSeleccionado,
                    VerTodasLasOrdenes
                );

                // Crear y mostrar el diálogo
                var dialog = new Dialog.FiltrosOrdenTraspasoDialog(dialogViewModel);

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
                    AlmacenDestinoSeleccionado = dialogViewModel.AlmacenDestinoSeleccionado;
                    AlmacenOrigenSeleccionado = dialogViewModel.AlmacenOrigenSeleccionado;
                    FechaDesde = dialogViewModel.FechaDesde;
                    FechaHasta = dialogViewModel.FechaHasta;
                    FechaPlan = dialogViewModel.FechaPlan;
                    EstadoFiltro = dialogViewModel.EstadoFiltro;
                    PrioridadFiltro = dialogViewModel.PrioridadFiltro;
                    CodigoOrdenFiltro = dialogViewModel.CodigoOrdenFiltro;
                    OperarioSeleccionadoCombo = dialogViewModel.OperarioSeleccionadoCombo;
                    CreadorSeleccionado = dialogViewModel.CreadorSeleccionado;
                    VerTodasLasOrdenes = dialogViewModel.VerTodasLasOrdenes;

                    // Notificar cambios en propiedades calculadas
                    OnPropertyChanged(nameof(TieneFiltrosActivos));
                    OnPropertyChanged(nameof(ResumenFiltrosActivos));

                    // Recargar las órdenes con los nuevos filtros
                    await LoadOrdenesTraspasoAsync();
                    
                    // Refrescar la vista para aplicar los filtros
                    OrdenesView?.Refresh();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog(
                    "Error",
                    $"Error al abrir el diálogo de filtros: {ex.Message}");
                var mainWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                if (mainWindow != null && mainWindow != errorDialog)
                    errorDialog.Owner = mainWindow;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void LimpiarFiltros()
        {
            EstadoFiltro = "TODOS";
            PrioridadFiltro = "TODAS";
            CodigoOrdenFiltro = string.Empty;
            FechaPlan = null;
            VerTodasLasOrdenes = false;
            CreadorSeleccionado = 0;
            
            // Seleccionar "Todas" en almacenes
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenDestinoSeleccionado = AlmacenesCombo.FirstOrDefault();
            }

            if (AlmacenesOrigenCombo?.Any() == true)
            {
                AlmacenOrigenSeleccionado = AlmacenesOrigenCombo.FirstOrDefault();
            }

            // Seleccionar "Todos" en operarios
            if (OperariosCombo?.Any() == true)
            {
                OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
            }

            // Seleccionar "Todos" en creadores
            CreadorSeleccionado = 0;

            // Establecer fechas por defecto: desde hace 7 días hasta hoy
            FechaDesde = DateTime.Today.AddDays(-7);
            FechaHasta = DateTime.Today;

            // Limpiar filtro especial
            _filtroEspecial = string.Empty;

            // Notificar cambios
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));

            // Refrescar la vista
            OrdenesView?.Refresh();
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

                // Filtro por almacén origen
                if (AlmacenOrigenSeleccionado != null && 
                    AlmacenOrigenSeleccionado.CodigoAlmacen != "Todas" && 
                    !string.IsNullOrEmpty(orden.AlmacenOrigenDescripcion) &&
                    orden.AlmacenOrigenDescripcion != AlmacenOrigenSeleccionado.CodigoAlmacen)
                    return false;

                // Filtro por estado
                if (EstadoFiltro != "TODOS" && orden.Estado != EstadoFiltro)
                    return false;

                // Filtro por código de orden
                if (!string.IsNullOrWhiteSpace(CodigoOrdenFiltro))
                {
                    if (string.IsNullOrEmpty(orden.CodigoOrden) || 
                        !orden.CodigoOrden.Contains(CodigoOrdenFiltro, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // Filtro por prioridad
                if (!string.IsNullOrEmpty(PrioridadFiltro) && PrioridadFiltro != "TODAS")
                {
                    short prioridadBuscada = PrioridadFiltro switch
                    {
                        "1 - Muy Baja" => 1,
                        "2 - Baja" => 2,
                        "3 - Normal" => 3,
                        "4 - Alta" => 4,
                        "5 - Muy Alta" => 5,
                        _ => 0
                    };
                    if (prioridadBuscada > 0 && orden.Prioridad != prioridadBuscada)
                        return false;
                }

                // Filtro por operario (buscar en las líneas)
                if (OperarioSeleccionadoCombo != null && OperarioSeleccionadoCombo.Operario != 0)
                {
                    var tieneLineasConOperario = orden.Lineas.Any(l => l.IdOperarioAsignado == OperarioSeleccionadoCombo.Operario);
                    if (!tieneLineasConOperario)
                        return false;
                }

                // Filtro por usuario creación
                if (CreadorSeleccionado.HasValue && CreadorSeleccionado.Value != 0)
                {
                    if (orden.UsuarioCreacion != CreadorSeleccionado.Value)
                        return false;
                }
                else if (!VerTodasLasOrdenes)
                {
                    // Si no se especifica usuario y VerTodasLasOrdenes es false, filtrar por el usuario actual
                    var usuarioActual = SessionManager.UsuarioActual?.operario ?? 0;
                    if (orden.UsuarioCreacion != usuarioActual)
                        return false;
                }

                // Filtro por fecha plan
                if (FechaPlan.HasValue)
                {
                    if (!orden.FechaPlan.HasValue || orden.FechaPlan.Value.Date != FechaPlan.Value.Date)
                        return false;
                }
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

            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));

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