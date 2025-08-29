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
        #endregion

        #region Constructor
        public ControlesRotativosViewModel(ConteosService conteosService, StockService stockService)
        {
            _conteosService = conteosService;
            _stockService = stockService;
            EmpresaActual = ObtenerNombreEmpresaActual();
            AlmacenesCombo = new ObservableCollection<AlmacenDto>();
            OrdenesConteo = new ObservableCollection<OrdenConteoDto>();

            OrdenesConteoView = CollectionViewSource.GetDefaultView(OrdenesConteo);
            OrdenesConteoView.Filter = new Predicate<object>(FiltroOrden);

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

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public ControlesRotativosViewModel() : this(new ConteosService(), new StockService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private string empresaActual;

        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; }
        public ObservableCollection<OrdenConteoDto> OrdenesConteo { get; }
        public ObservableCollection<string> EstadosCombo { get; }

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

        public ICollectionView OrdenesConteoView { get; }
        #endregion

        #region Computed Properties
        public bool CanEnableInputs => !IsCargando;
        public bool CanCargarControles => !IsCargando && AlmacenSeleccionadoCombo != null;

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
                var dialogViewModel = new CrearOrdenConteoDialogViewModel(_conteosService, _stockService);
                
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
                errorDialog.ShowDialog();
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

                OrdenesConteo.Clear();
                foreach (var orden in ordenes)
                {
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
                errorDialog.ShowDialog();
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

            var dialog = new WarningDialog(
                "Ver Orden de Conteo",
                $"Orden #{orden.Id}: {orden.Titulo}\nEstado: {orden.EstadoFormateado}\nAlcance: {orden.AlcanceFormateado}");
            dialog.ShowDialog();
        }

        [RelayCommand]
        private void EditarOrden(OrdenConteoDto orden)
        {
            if (orden == null) return;

            var dialog = new WarningDialog(
                "Editar Orden de Conteo", 
                $"Funcionalidad para editar la orden #{orden.Id} en desarrollo.");
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
        #endregion

        #region Private Methods
        private async Task InitializeAsync()
        {
            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando almacenes...";

                await CargarAlmacenesAsync();
                
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

            // Filtro por almacén (solo si hay un almacén seleccionado)
            if (AlmacenSeleccionadoCombo != null && 
                !string.IsNullOrEmpty(AlmacenSeleccionadoCombo.CodigoAlmacen) &&
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

        partial void OnIsCargandoChanged(bool value)
        {
            OnPropertyChanged(nameof(CanEnableInputs));
            OnPropertyChanged(nameof(CanCargarControles));
        }
        #endregion
    }
} 