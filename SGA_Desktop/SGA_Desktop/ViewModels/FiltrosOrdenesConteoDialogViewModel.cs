using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;

namespace SGA_Desktop.ViewModels
{
    public partial class FiltrosOrdenesConteoDialogViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly LoginService _loginService;

        // Propiedades para filtros
        [ObservableProperty] private AlmacenDto? almacenSeleccionadoCombo;
        [ObservableProperty] private DateTime fechaDesde = DateTime.Today.AddDays(-7);
        [ObservableProperty] private DateTime fechaHasta = DateTime.Today;
        [ObservableProperty] private string estadoFiltro = "TODOS";
        [ObservableProperty] private OperariosAccesoDto? operarioSeleccionadoCombo;

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; } = new();
        public ObservableCollection<string> EstadosCombo { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosCombo { get; } = new();

        // Propiedades para autocompletado de almacenes
        [ObservableProperty] private string filtroAlmacenesTexto = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenes = false;
        public ICollectionView AlmacenesComboView { get; private set; }

        // Propiedades para autocompletado de operarios
        [ObservableProperty] private string filtroOperariosCombo = "";
        [ObservableProperty] private bool isDropDownOpenCombo = false;
        public ICollectionView OperariosComboView { get; private set; }

        // Comandos
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand CerrarCommand { get; }

        // Comandos para controlar dropdown de almacenes
        public IRelayCommand AbrirDropDownAlmacenesCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesCommand { get; }

        // Comandos para controlar dropdown de operarios
        public IRelayCommand AbrirDropDownComboCommand { get; }
        public IRelayCommand CerrarDropDownComboCommand { get; }

        // Evento para comunicar con el diálogo
        public event Action<bool> RequestClose;

        public FiltrosOrdenesConteoDialogViewModel()
        {
            _stockService = new StockService();
            _loginService = new LoginService();

            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesComboView = CollectionViewSource.GetDefaultView(AlmacenesCombo);
            AlmacenesComboView.Filter = FiltraAlmacenes;

            // Inicializar ICollectionView para filtrado de operarios
            OperariosComboView = CollectionViewSource.GetDefaultView(OperariosCombo);
            OperariosComboView.Filter = FiltraOperarioCombo;

            // Inicializar estados
            EstadosCombo.Add("TODOS");
            EstadosCombo.Add("PLANIFICADO");
            EstadosCombo.Add("ASIGNADO");
            EstadosCombo.Add("EN_PROCESO");
            EstadosCombo.Add("CERRADO");
            EstadosCombo.Add("CANCELADO");

            // Inicializar comandos
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
            CerrarCommand = new RelayCommand(Cerrar);

            // Comandos para dropdown de almacenes
            AbrirDropDownAlmacenesCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesTexto = "";
                IsDropDownOpenAlmacenes = true;
            });

            CerrarDropDownAlmacenesCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenes = false;
            });

            // Comandos para dropdown de operarios
            AbrirDropDownComboCommand = new RelayCommand(() =>
            {
                FiltroOperariosCombo = "";
                IsDropDownOpenCombo = true;
            });

            CerrarDropDownComboCommand = new RelayCommand(() =>
            {
                IsDropDownOpenCombo = false;
            });

            // Inicialización
            _ = InitializeAsync();
        }

        // Constructor con valores iniciales desde el ViewModel principal
        public FiltrosOrdenesConteoDialogViewModel(
            AlmacenDto? almacenSeleccionado,
            DateTime fechaDesde,
            DateTime fechaHasta,
            string estadoFiltro,
            OperariosAccesoDto? operarioSeleccionado) : this()
        {
            AlmacenSeleccionadoCombo = almacenSeleccionado;
            FechaDesde = fechaDesde;
            FechaHasta = fechaHasta;
            EstadoFiltro = estadoFiltro;
            OperarioSeleccionadoCombo = operarioSeleccionado;
        }

        // Validaciones de fechas
        partial void OnFechaDesdeChanged(DateTime value)
        {
            if (FechaHasta < value)
            {
                FechaHasta = value;
            }
        }

        partial void OnFechaHastaChanged(DateTime value)
        {
            if (value < FechaDesde)
            {
                FechaHasta = FechaDesde;
            }
        }

        // Métodos para manejar cambios en los filtros
        partial void OnFiltroAlmacenesTextoChanged(string value)
        {
            AlmacenesComboView?.Refresh();
        }

        partial void OnFiltroOperariosComboChanged(string value)
        {
            OperariosComboView?.Refresh();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Cargar almacenes
                await CargarAlmacenesAsync();

                // Cargar operarios
                await CargarOperariosAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en inicialización: {ex.Message}");
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

                // Si no hay almacén seleccionado, seleccionar "Todas"
                if (AlmacenSeleccionadoCombo == null)
                {
                    AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
            }
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();

                OperariosCombo.Clear();

                // Agregar opción "Todos"
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

                // Si no hay operario seleccionado, seleccionar "TODOS"
                if (OperarioSeleccionadoCombo == null)
                {
                    OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
            }
        }

        private async Task AplicarFiltrosAsync()
        {
            // Cerrar el diálogo con resultado true (aplicar filtros)
            RequestClose?.Invoke(true);
        }

        private void LimpiarFiltros()
        {
            EstadoFiltro = "TODOS";

            // Seleccionar "Todas" en almacenes
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenSeleccionadoCombo = AlmacenesCombo.FirstOrDefault();
                FiltroAlmacenesTexto = "";
            }

            // Seleccionar "TODOS" en operarios
            if (OperariosCombo?.Any() == true)
            {
                OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                FiltroOperariosCombo = "";
            }

            // Establecer fechas: desde hace 7 días hasta hoy
            FechaDesde = DateTime.Today.AddDays(-7);
            FechaHasta = DateTime.Today;
        }

        private void Cerrar()
        {
            // Cerrar el diálogo sin aplicar filtros
            RequestClose?.Invoke(false);
        }

        // Método de filtrado para almacenes
        private bool FiltraAlmacenes(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesTexto)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // Método de filtrado para operarios
        private bool FiltraOperarioCombo(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrEmpty(FiltroOperariosCombo)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(operario.NombreOperario, FiltroOperariosCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
    }
}

