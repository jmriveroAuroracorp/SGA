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
                await LoadOrdenesTraspasoAsync();
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
            OrdenesView?.Refresh();
        }

        partial void OnFechaHastaChanged(DateTime oldValue, DateTime newValue)
        {
            // Si la fecha hasta es anterior a la fecha desde, ajustarla
            if (newValue < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
            OrdenesView?.Refresh();
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            OrdenesView?.Refresh();
        }

        partial void OnEstadoFiltroChanged(string oldValue, string newValue)
        {
            OrdenesView?.Refresh();
        }

        [RelayCommand]
        private async Task LoadOrdenesTraspasoAsync()
        {
            try
            {
                IsLoading = true;
                IsCargando = true;
                MensajeEstado = "Cargando órdenes...";
                
                var ordenes = await _ordenTraspasoService.GetOrdenesTraspasoAsync();
                
                OrdenesTraspaso.Clear();
                
                foreach (var orden in ordenes)
                {
                    OrdenesTraspaso.Add(orden);
                }
                
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
                    MensajeEstado = $"{OrdenesTraspaso.Count} órdenes cargadas";
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
            // TODO: Implementar edición
            System.Diagnostics.Debug.WriteLine($"Editar orden: {orden.CodigoOrden}");
        }

        [RelayCommand]
        private async Task CancelarOrden(OrdenTraspasoDto orden)
        {
            try
            {
                var result = await _ordenTraspasoService.CancelarOrdenTraspasoAsync(orden.IdOrdenTraspaso);
                if (result)
                {
                    await LoadOrdenesTraspasoAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cancelar orden: {ex.Message}");
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

            return true;
        }
    }
} 