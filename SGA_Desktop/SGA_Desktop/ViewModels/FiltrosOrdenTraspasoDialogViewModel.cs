using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public partial class FiltrosOrdenTraspasoDialogViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly LoginService _loginService;

        // Propiedades para filtros
        [ObservableProperty] private AlmacenDto? almacenDestinoSeleccionado;
        [ObservableProperty] private AlmacenDto? almacenOrigenSeleccionado;
        [ObservableProperty] private DateTime fechaDesde = DateTime.Today.AddDays(-7);
        [ObservableProperty] private DateTime fechaHasta = DateTime.Today;
        [ObservableProperty] private DateTime? fechaPlan;
        [ObservableProperty] private string estadoFiltro = "TODOS";
        [ObservableProperty] private string prioridadFiltro = "TODAS";
        [ObservableProperty] private string codigoOrdenFiltro = string.Empty;
        [ObservableProperty] private OperariosAccesoDto? operarioSeleccionadoCombo;
        [ObservableProperty] private int? creadorSeleccionado;
        [ObservableProperty] private bool verTodasLasOrdenes = false; // Por defecto, solo ver los propios

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; } = new();
        public ObservableCollection<AlmacenDto> AlmacenesOrigenCombo { get; } = new();
        public ObservableCollection<string> EstadosCombo { get; } = new();
        public ObservableCollection<string> PrioridadesCombo { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosCombo { get; } = new();
        public ObservableCollection<UsuarioDto> UsuariosCombo { get; } = new();

        // Propiedades para autocompletado de almacenes
        [ObservableProperty] private string filtroAlmacenesTexto = "";
        [ObservableProperty] private string filtroAlmacenesOrigenTexto = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenes = false;
        [ObservableProperty] private bool isDropDownOpenAlmacenesOrigen = false;
        public ICollectionView AlmacenesComboView { get; private set; }
        public ICollectionView AlmacenesOrigenComboView { get; private set; }

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
        public IRelayCommand AbrirDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesOrigenCommand { get; }

        // Comandos para controlar dropdown de operarios
        public IRelayCommand AbrirDropDownComboCommand { get; }
        public IRelayCommand CerrarDropDownComboCommand { get; }

        // Evento para comunicar con el diálogo
        public event Action<bool> RequestClose;

        public FiltrosOrdenTraspasoDialogViewModel()
        {
            _stockService = new StockService();
            _loginService = new LoginService();

            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesComboView = CollectionViewSource.GetDefaultView(AlmacenesCombo);
            AlmacenesComboView.Filter = FiltraAlmacenes;

            AlmacenesOrigenComboView = CollectionViewSource.GetDefaultView(AlmacenesOrigenCombo);
            AlmacenesOrigenComboView.Filter = FiltraAlmacenesOrigen;

            // Inicializar ICollectionView para filtrado de operarios
            OperariosComboView = CollectionViewSource.GetDefaultView(OperariosCombo);
            OperariosComboView.Filter = FiltraOperarioCombo;

            // Inicializar estados
            EstadosCombo.Add("TODOS");
            EstadosCombo.Add("PENDIENTE");
            EstadosCombo.Add("EN_PROCESO");
            EstadosCombo.Add("COMPLETADA");
            EstadosCombo.Add("CANCELADA");
            EstadosCombo.Add("SIN_ASIGNAR");

            // Inicializar prioridades
            PrioridadesCombo.Add("TODAS");
            PrioridadesCombo.Add("1 - Muy Baja");
            PrioridadesCombo.Add("2 - Baja");
            PrioridadesCombo.Add("3 - Normal");
            PrioridadesCombo.Add("4 - Alta");
            PrioridadesCombo.Add("5 - Muy Alta");

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

            AbrirDropDownAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesOrigenTexto = "";
                IsDropDownOpenAlmacenesOrigen = true;
            });

            CerrarDropDownAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenesOrigen = false;
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
        public FiltrosOrdenTraspasoDialogViewModel(
            AlmacenDto? almacenDestinoSeleccionado,
            AlmacenDto? almacenOrigenSeleccionado,
            DateTime fechaDesde,
            DateTime fechaHasta,
            DateTime? fechaPlan,
            string estadoFiltro,
            string prioridadFiltro,
            string codigoOrdenFiltro,
            OperariosAccesoDto? operarioSeleccionadoCombo,
            int? creadorSeleccionado,
            bool verTodasLasOrdenes) : this()
        {
            AlmacenDestinoSeleccionado = almacenDestinoSeleccionado;
            AlmacenOrigenSeleccionado = almacenOrigenSeleccionado;
            FechaDesde = fechaDesde;
            FechaHasta = fechaHasta;
            FechaPlan = fechaPlan;
            EstadoFiltro = estadoFiltro;
            PrioridadFiltro = prioridadFiltro;
            CodigoOrdenFiltro = codigoOrdenFiltro;
            OperarioSeleccionadoCombo = operarioSeleccionadoCombo;
            CreadorSeleccionado = creadorSeleccionado;
            VerTodasLasOrdenes = verTodasLasOrdenes;
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

        partial void OnFiltroAlmacenesOrigenTextoChanged(string value)
        {
            AlmacenesOrigenComboView?.Refresh();
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
                // Cargar usuarios (creadores)
                await CargarUsuariosAsync();
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
                AlmacenesOrigenCombo.Clear();

                // Añadir opción "Todas"
                var todas = new AlmacenDto
                {
                    CodigoAlmacen = "Todas",
                    NombreAlmacen = "Todas",
                    CodigoEmpresa = empresa
                };
                AlmacenesCombo.Add(todas);
                AlmacenesOrigenCombo.Add(todas);

                foreach (var a in resultado)
                {
                    AlmacenesCombo.Add(a);
                    AlmacenesOrigenCombo.Add(a);
                }

                // Si no hay almacén seleccionado, seleccionar "Todas"
                if (AlmacenDestinoSeleccionado == null)
                {
                    AlmacenDestinoSeleccionado = AlmacenesCombo.FirstOrDefault();
                }
                if (AlmacenOrigenSeleccionado == null)
                {
                    AlmacenOrigenSeleccionado = AlmacenesOrigenCombo.FirstOrDefault();
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

                // Seleccionar "Todos" por defecto en el filtro solo si hay elementos
                if (OperariosCombo.Any() && OperarioSeleccionadoCombo == null)
                {
                    OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
            }
        }

        private async Task CargarUsuariosAsync()
        {
            try
            {
                // Obtener todos los operarios con acceso a traspasos como usuarios creadores
                var operarios = await _loginService.ObtenerOperariosConAccesoTraspasosAsync();

                UsuariosCombo.Clear();

                // Agregar opción "Todos"
                UsuariosCombo.Add(new UsuarioDto
                {
                    UsuarioId = 0,
                    NombreUsuario = "TODOS"
                });

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    UsuariosCombo.Add(new UsuarioDto
                    {
                        UsuarioId = operario.Operario,
                        NombreUsuario = operario.NombreOperario
                    });
                }

                // Si no hay creador seleccionado, seleccionar "Todos"
                if (UsuariosCombo.Any() && CreadorSeleccionado == null)
                {
                    CreadorSeleccionado = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando usuarios: {ex.Message}");
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
            PrioridadFiltro = "TODAS";
            CodigoOrdenFiltro = string.Empty;
            FechaPlan = null;
            VerTodasLasOrdenes = false;

            // Seleccionar "Todas" en almacenes
            if (AlmacenesCombo?.Any() == true)
            {
                AlmacenDestinoSeleccionado = AlmacenesCombo.FirstOrDefault();
                FiltroAlmacenesTexto = "";
            }
            if (AlmacenesOrigenCombo?.Any() == true)
            {
                AlmacenOrigenSeleccionado = AlmacenesOrigenCombo.FirstOrDefault();
                FiltroAlmacenesOrigenTexto = "";
            }

            // Seleccionar "Todos" en operarios
            if (OperariosCombo?.Any() == true)
            {
                OperarioSeleccionadoCombo = OperariosCombo.FirstOrDefault();
                FiltroOperariosCombo = "";
            }

            // Seleccionar "Todos" en creadores
            CreadorSeleccionado = 0;

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

        private bool FiltraAlmacenesOrigen(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesOrigenTexto)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesOrigenTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        private bool FiltraOperarioCombo(object obj)
        {
            if (obj is not OperariosAccesoDto operario) return false;
            if (string.IsNullOrEmpty(FiltroOperariosCombo)) return true;

            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(operario.NombreOperario, FiltroOperariosCombo, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
    }

    // Clase auxiliar para usuarios creadores
    public class UsuarioDto
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
    }
}

