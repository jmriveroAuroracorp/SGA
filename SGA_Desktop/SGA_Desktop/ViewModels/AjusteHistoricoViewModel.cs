using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Globalization;
using System.Windows;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using SGA_Desktop.Dialog;

namespace SGA_Desktop.ViewModels
{
    public partial class AjusteHistoricoViewModel : ObservableObject
    {
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;

        // Propiedades para filtros
        [ObservableProperty] private DateTime? fechaDesde;
        [ObservableProperty] private DateTime? fechaHasta;
        [ObservableProperty] private string codigoArticulo = "";
        [ObservableProperty] private string codigoLote = "";
        [ObservableProperty] private string codigoPalet = "";
        [ObservableProperty] private AlmacenDto? almacenSeleccionado;
        [ObservableProperty] private EstadoAjusteDto? estadoSeleccionado;
        [ObservableProperty] private OperariosAccesoDto? operarioSeleccionado;
        [ObservableProperty] private OrigenAjusteDto? origenSeleccionado;
        [ObservableProperty] private bool estaCargando = false;

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> Almacenes { get; } = new();
        public ObservableCollection<EstadoAjusteDto> Estados { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; } = new();
        public ObservableCollection<OrigenAjusteDto> Origenes { get; } = new();

        // Propiedades para filtrado inteligente de almacenes
        [ObservableProperty] private string filtroAlmacenes = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenes = false;
        public ICollectionView AlmacenesView { get; private set; }

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
                var almacenActivo = AlmacenSeleccionado != null;
                var estadoActivo = EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.CodigoEstado);
                var operarioActivo = OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0;
                var origenActivo = OrigenSeleccionado != null && !string.IsNullOrEmpty(OrigenSeleccionado.CodigoOrigen);
                var articuloActivo = !string.IsNullOrWhiteSpace(CodigoArticulo);
                var loteActivo = !string.IsNullOrWhiteSpace(CodigoLote);
                var paletActivo = !string.IsNullOrWhiteSpace(CodigoPalet);
                var fechaDesdeActiva = FechaDesde.HasValue && FechaDesde.Value != DateTime.Today;
                var fechaHastaActiva = FechaHasta.HasValue && FechaHasta.Value != DateTime.Today;

                return almacenActivo || estadoActivo || operarioActivo || origenActivo ||
                       articuloActivo || loteActivo || paletActivo || fechaDesdeActiva || fechaHastaActiva;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

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

                if (AlmacenSeleccionado != null)
                {
                    filtros.Add($"Almacén: {AlmacenSeleccionado.CodigoAlmacen}");
                }

                if (EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.CodigoEstado))
                {
                    filtros.Add($"Estado: {EstadoSeleccionado.Descripcion}");
                }

                if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
                {
                    filtros.Add($"Operario: {OperarioSeleccionado.NombreOperario}");
                }

                if (OrigenSeleccionado != null && !string.IsNullOrEmpty(OrigenSeleccionado.CodigoOrigen))
                {
                    filtros.Add($"Origen: {OrigenSeleccionado.Descripcion}");
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

                return filtros.Count > 0 ? string.Join(" | ", filtros) : "Sin filtros";
            }
        }

        // Datos principales
        public ObservableCollection<AjusteDto> Ajustes { get; } = new();
        [ObservableProperty] private AjusteDto? ajusteSeleccionado;

        // Comandos
        public IRelayCommand AbrirFiltrosCommand { get; }
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }

        // Comandos para manejo del dropdown de almacenes
        public IRelayCommand AbrirDropDownAlmacenesCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesCommand { get; }

        // Comandos para manejo del dropdown de operarios
        public IRelayCommand AbrirDropDownOperariosCommand { get; }
        public IRelayCommand CerrarDropDownOperariosCommand { get; }

        public AjusteHistoricoViewModel(InventarioService inventarioService)
        {
            _inventarioService = inventarioService;
            _stockService = new StockService();
            _loginService = new LoginService();

            // Inicializar ICollectionView para filtrado de operarios
            OperariosView = CollectionViewSource.GetDefaultView(OperariosDisponibles);
            OperariosView.Filter = FiltraOperario;

            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesView = CollectionViewSource.GetDefaultView(Almacenes);
            AlmacenesView.Filter = FiltraAlmacenes;

            // Inicializar comandos
            AbrirFiltrosCommand = new RelayCommand(AbrirFiltros);
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);

            // Inicializar comandos para dropdown de almacenes
            AbrirDropDownAlmacenesCommand = new RelayCommand(() =>
            {
                FiltroAlmacenes = "";
                IsDropDownOpenAlmacenes = true;
            });

            CerrarDropDownAlmacenesCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenes = false;
            });

            // Inicializar comandos para dropdown de operarios
            AbrirDropDownOperariosCommand = new RelayCommand(() =>
            {
                FiltroOperarios = "";
                IsDropDownOpenOperarios = true;
            });

            CerrarDropDownOperariosCommand = new RelayCommand(() =>
            {
                IsDropDownOpenOperarios = false;
            });

            // Inicialización
            _ = InitializeAsync();
        }

        public AjusteHistoricoViewModel() : this(new InventarioService()) { }

        // Validaciones de fechas
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

        partial void OnCodigoArticuloChanged(string value)
        {
            OnPropertyChanged(nameof(PuedeFiltrarPorLote));

            if (string.IsNullOrWhiteSpace(value))
            {
                CodigoLote = "";
            }
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnCodigoLoteChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnCodigoPaletChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnEstadoSeleccionadoChanged(EstadoAjusteDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnOrigenSeleccionadoChanged(OrigenAjusteDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnFiltroOperariosChanged(string value)
        {
            OperariosView.Refresh();
        }

        partial void OnFiltroAlmacenesChanged(string value)
        {
            AlmacenesView?.Refresh();
        }

        private async Task InitializeAsync()
        {
            try
            {
                FechaDesde = DateTime.Today;
                FechaHasta = DateTime.Today;

                await CargarAlmacenesAsync();
                await CargarOperariosAsync();
                await CargarEstadosAsync();
                await CargarOrigenesAsync();

                EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado));
                OrigenSeleccionado = Origenes.FirstOrDefault(o => string.IsNullOrEmpty(o.CodigoOrigen));

                await CargarAjustesAsync();
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
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                if (!permisos.Any())
                {
                    permisos = await _stockService.ObtenerAlmacenesAsync(centro);
                }

                var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);

                Almacenes.Clear();
                foreach (var almacen in almacenes)
                {
                    Almacenes.Add(almacen);
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

                Estados.Add(new EstadoAjusteDto { CodigoEstado = "", Descripcion = "-- Todos los estados --" });
                Estados.Add(new EstadoAjusteDto { CodigoEstado = "PENDIENTE_ERP", Descripcion = "Pendiente ERP" });
                Estados.Add(new EstadoAjusteDto { CodigoEstado = "COMPLETADO", Descripcion = "Completado" });
                Estados.Add(new EstadoAjusteDto { CodigoEstado = "ERROR_ERP", Descripcion = "Error ERP" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando estados: {ex.Message}");
            }
        }

        private async Task CargarOrigenesAsync()
        {
            try
            {
                Origenes.Clear();

                Origenes.Add(new OrigenAjusteDto { CodigoOrigen = "", Descripcion = "-- Todos los orígenes --" });
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
                var empresa = SessionManager.EmpresaSeleccionada!.Value;

                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;

                var estadoFiltro = EstadoSeleccionado?.CodigoEstado;
                if (string.IsNullOrEmpty(estadoFiltro))
                {
                    estadoFiltro = null;
                }

                var codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                    ? CodigoPalet.Trim()
                    : null;

                var ajustes = await _inventarioService.ObtenerAjustesFiltradosAsync(
                    codigoEmpresa: empresa,
                    fechaDesde: fechaDesde.Date,
                    fechaHasta: fechaHasta.Date,
                    codigoArticulo: string.IsNullOrWhiteSpace(CodigoArticulo) ? null : CodigoArticulo,
                    codigoAlmacen: AlmacenSeleccionado?.CodigoAlmacen,
                    estado: estadoFiltro,
                    usuarioId: OperarioSeleccionado?.Operario > 0 ? OperarioSeleccionado.Operario : null,
                    partida: (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo)) ? CodigoLote : null,
                    codigoPalet: codigoPaletFiltro
                );

                Ajustes.Clear();

                // Filtrar por almacenes permitidos del usuario
                var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                var ajustesFiltrados = ajustes.Where(a =>
                    almacenesPermitidos.Contains(a.CodigoAlmacen)
                ).ToList();

                // Filtro adicional por lote (si hay artículo)
                if (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    ajustesFiltrados = ajustesFiltrados.Where(a =>
                        !string.IsNullOrWhiteSpace(a.Partida) &&
                        a.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                }

                // Filtro por origen (Inventario, Conteo, Manual)
                if (OrigenSeleccionado != null && !string.IsNullOrEmpty(OrigenSeleccionado.CodigoOrigen))
                {
                    ajustesFiltrados = ajustesFiltrados.Where(a =>
                        a.Origen == OrigenSeleccionado.CodigoOrigen
                    ).ToList();
                }

                foreach (var ajuste in ajustesFiltrados.OrderByDescending(a => a.Fecha))
                {
                    Ajustes.Add(ajuste);
                }
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

        private async Task AplicarFiltrosAsync()
        {
            await CargarAjustesAsync();
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today;
            FechaHasta = DateTime.Today;
            CodigoArticulo = "";
            CodigoLote = "";
            CodigoPalet = "";
            OperarioSeleccionado = null;
            AlmacenSeleccionado = null;
            EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado));
            OrigenSeleccionado = Origenes.FirstOrDefault(o => string.IsNullOrEmpty(o.CodigoOrigen));

            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));

            Ajustes.Clear();
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                // Obtener operarios con permiso 13 (conteos)
                var operariosConteos = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                
                // Obtener operarios con permiso 14 (inventarios)
                var operariosInventarios = await _loginService.ObtenerOperariosConAccesoInventariosAsync();

                System.Diagnostics.Debug.WriteLine($"[AjusteHistorico] Operarios con permiso 13 (conteos): {operariosConteos.Count}");
                System.Diagnostics.Debug.WriteLine($"[AjusteHistorico] Operarios con permiso 14 (inventarios): {operariosInventarios.Count}");

                OperariosDisponibles.Clear();

                // Combinar ambas listas y eliminar duplicados
                var todosOperarios = operariosConteos
                    .Concat(operariosInventarios)
                    .GroupBy(o => o.Operario)
                    .Select(g => g.First())
                    .OrderBy(o => o.NombreOperario)
                    .ToList();

                foreach (var operario in todosOperarios)
                {
                    OperariosDisponibles.Add(operario);
                }

                System.Diagnostics.Debug.WriteLine($"[AjusteHistorico] Total operarios cargados (permisos 13 y 14): {OperariosDisponibles.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                try
                {
                    // Fallback: intentar solo con conteos
                    var operariosFallback = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                    OperariosDisponibles.Clear();
                    foreach (var operario in operariosFallback.OrderBy(o => o.NombreOperario))
                    {
                        OperariosDisponibles.Add(operario);
                    }
                }
                catch
                {
                    OperariosDisponibles.Clear();
                }
            }
        }

        private void AbrirFiltros()
        {
            var dlg = new HistorialAjustesFiltrosDialog
            {
                DataContext = this
            };
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow;
            if (owner != null && owner != dlg)
                dlg.Owner = owner;
            dlg.ShowDialog();
        }

        // Método de filtrado para operarios
        private bool FiltraOperario(object obj)
        {
            if (string.IsNullOrWhiteSpace(FiltroOperarios)) return true;
            if (obj is not OperariosAccesoDto operario) return false;

            var compare = CultureInfo.CurrentCulture.CompareInfo;
            var options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

            bool contiene(string s) =>
                !string.IsNullOrEmpty(s) &&
                compare.IndexOf(s, FiltroOperarios, options) >= 0;

            return contiene(operario.NombreOperario) || contiene(operario.NombreCompleto);
        }

        // Método de filtrado para almacenes
        private bool FiltraAlmacenes(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenes)) return true;

            return CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenes, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }

        // Método de seguridad: Obtener almacenes permitidos del usuario
        private async Task<List<string>> ObtenerAlmacenesPermitidosAsync()
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

                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);

                return almacenesAutorizados.Select(a => a.CodigoAlmacen).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo almacenes permitidos: {ex.Message}");
                return new List<string>();
            }
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
        private void CopiarCodigoPalet(string codigoPalet)
        {
            if (!string.IsNullOrWhiteSpace(codigoPalet))
                Clipboard.SetText(codigoPalet);
        }

        [RelayCommand]
        private void CopiarDiferencia(decimal diferencia)
        {
            Clipboard.SetText(diferencia.ToString("0.######"));
        }

        [RelayCommand]
        private void CopiarUsuario(string usuario)
        {
            if (!string.IsNullOrWhiteSpace(usuario))
                Clipboard.SetText(usuario);
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
        private void CopiarCodigoInventario(string codigoInventario)
        {
            if (!string.IsNullOrWhiteSpace(codigoInventario))
                Clipboard.SetText(codigoInventario);
        }
    }

    /// <summary>
    /// DTO para estados de ajuste
    /// </summary>
    public class EstadoAjusteDto
    {
        public string CodigoEstado { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }

    /// <summary>
    /// DTO para orígenes de ajuste
    /// </summary>
    public class OrigenAjusteDto
    {
        public string CodigoOrigen { get; set; } = "";
        public string Descripcion { get; set; } = "";
    }
}

