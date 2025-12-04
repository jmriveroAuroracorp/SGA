using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Globalization;
using System.Windows;
using SGA_Desktop.Dialog;

namespace SGA_Desktop.ViewModels
{
    public partial class TraspasoHistoricoViewModel : ObservableObject
    {
        private readonly TraspasosService _traspasosService;
        private readonly StockService _stockService;
        private readonly LoginService _loginService;

        // Propiedades para filtros
        [ObservableProperty] private DateTime? fechaDesde;
        [ObservableProperty] private DateTime? fechaHasta;
        [ObservableProperty] private string codigoArticulo = "";
        [ObservableProperty] private string codigoLote = "";
        [ObservableProperty] private string codigoPalet = "";
        [ObservableProperty] private string filtroObservaciones = "";
        [ObservableProperty] private AlmacenDto? almacenOrigenSeleccionado;
        [ObservableProperty] private AlmacenDto? almacenDestinoSeleccionado;
        [ObservableProperty] private EstadoTraspasoDto? estadoSeleccionado;
        [ObservableProperty] private OperariosAccesoDto? operarioSeleccionado;
        [ObservableProperty] private bool estaCargando = false;

        // Colecciones para filtros
        public ObservableCollection<AlmacenDto> AlmacenesOrigen { get; } = new();
        public ObservableCollection<AlmacenDto> AlmacenesDestino { get; } = new();
        public ObservableCollection<EstadoTraspasoDto> Estados { get; } = new();
        public ObservableCollection<OperariosAccesoDto> OperariosDisponibles { get; } = new();
        
        // Propiedades para filtrado inteligente de almacenes
        [ObservableProperty] private string filtroAlmacenesOrigen = "";
        [ObservableProperty] private string filtroAlmacenesDestino = "";
        [ObservableProperty] private bool isDropDownOpenAlmacenesOrigen = false;
        [ObservableProperty] private bool isDropDownOpenAlmacenesDestino = false;
        public ICollectionView AlmacenesOrigenView { get; private set; }
        public ICollectionView AlmacenesDestinoView { get; private set; }
        
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
                // Verificar si hay filtros activos (no son los valores por defecto)
                var almacenOrigenActivo = AlmacenOrigenSeleccionado != null;
                var almacenDestinoActivo = AlmacenDestinoSeleccionado != null;
                var estadoActivo = EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.CodigoEstado);
                var operarioActivo = OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0;
                var articuloActivo = !string.IsNullOrWhiteSpace(CodigoArticulo);
                var loteActivo = !string.IsNullOrWhiteSpace(CodigoLote);
                var paletActivo = !string.IsNullOrWhiteSpace(CodigoPalet);
                var observacionesActivo = !string.IsNullOrWhiteSpace(FiltroObservaciones);
                var fechaDesdeActiva = FechaDesde.HasValue && FechaDesde.Value != DateTime.Today;
                var fechaHastaActiva = FechaHasta.HasValue && FechaHasta.Value != DateTime.Today;

                return almacenOrigenActivo || almacenDestinoActivo || estadoActivo || operarioActivo || 
                       articuloActivo || loteActivo || paletActivo || observacionesActivo || fechaDesdeActiva || fechaHastaActiva;
            }
        }

        public string ResumenFiltrosActivos
        {
            get
            {
                var filtros = new List<string>();

                // 🔷 SIEMPRE mostrar las fechas (incluso si son las por defecto)
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

                if (AlmacenOrigenSeleccionado != null)
                {
                    filtros.Add($"Origen: {AlmacenOrigenSeleccionado.CodigoAlmacen}");
                }

                if (AlmacenDestinoSeleccionado != null)
                {
                    filtros.Add($"Destino: {AlmacenDestinoSeleccionado.CodigoAlmacen}");
                }

                if (EstadoSeleccionado != null && !string.IsNullOrEmpty(EstadoSeleccionado.CodigoEstado))
                {
                    filtros.Add($"Estado: {EstadoSeleccionado.Descripcion}");
                }

                if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
                {
                    filtros.Add($"Operario: {OperarioSeleccionado.NombreOperario}");
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

                if (!string.IsNullOrWhiteSpace(FiltroObservaciones))
                {
                    filtros.Add($"Observaciones: {FiltroObservaciones}");
                }

                return filtros.Count > 0 ? string.Join(" | ", filtros) : "Sin filtros";
            }
        }

        // Datos principales
        public ObservableCollection<TraspasoDto> Traspasos { get; } = new();
        [ObservableProperty] private TraspasoDto? traspasoSeleccionado;
        
        // Pestañas
        [ObservableProperty] private bool mostrandoSgaActual = true;
        [ObservableProperty] private bool mostrandoStorageControl = false;
        [ObservableProperty] private bool mostrandoCombinado = false;
        
        // Datos para StorageControl
        public ObservableCollection<TraspasoStorageControlDto> TraspasosStorageControl { get; } = new();
        [ObservableProperty] private TraspasoStorageControlDto? traspasoStorageControlSeleccionado;
        
        // Datos combinados (SGA Actual + StorageControl)
        public ObservableCollection<object> TraspasosCombinados { get; } = new();
        [ObservableProperty] private object? traspasoCombinadoSeleccionado;

        // Comandos
        public IRelayCommand AbrirFiltrosCommand { get; }
        public IAsyncRelayCommand AplicarFiltrosCommand { get; }
        public IRelayCommand LimpiarFiltrosCommand { get; }
        public IRelayCommand VerDetallesCommand { get; }
        public IRelayCommand CambiarASgaActualCommand { get; }
        public IRelayCommand CambiarAStorageControlCommand { get; }
        public IRelayCommand CambiarACombinadoCommand { get; }
        public IAsyncRelayCommand AplicarFiltrosStorageControlCommand { get; }
        
        // Comandos para manejo del dropdown de almacenes origen
        public IRelayCommand AbrirDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesOrigenCommand { get; }
        public IRelayCommand LimpiarSeleccionAlmacenesOrigenCommand { get; }
        
        // Comandos para manejo del dropdown de almacenes destino
        public IRelayCommand AbrirDropDownAlmacenesDestinoCommand { get; }
        public IRelayCommand CerrarDropDownAlmacenesDestinoCommand { get; }
        public IRelayCommand LimpiarSeleccionAlmacenesDestinoCommand { get; }

        public TraspasoHistoricoViewModel(TraspasosService traspasosService)
        {
            _traspasosService = traspasosService;
            _stockService = new StockService();
            _loginService = new LoginService();

            // Inicializar ICollectionView para filtrado de operarios
            OperariosView = CollectionViewSource.GetDefaultView(OperariosDisponibles);
            OperariosView.Filter = FiltraOperario;
            
            // Inicializar ICollectionView para filtrado de almacenes
            AlmacenesOrigenView = CollectionViewSource.GetDefaultView(AlmacenesOrigen);
            AlmacenesOrigenView.Filter = FiltraAlmacenesOrigen;
            
            AlmacenesDestinoView = CollectionViewSource.GetDefaultView(AlmacenesDestino);
            AlmacenesDestinoView.Filter = FiltraAlmacenesDestino;

            // Inicializar comandos
            AbrirFiltrosCommand = new RelayCommand(AbrirFiltros);
            AplicarFiltrosCommand = new AsyncRelayCommand(AplicarFiltrosAsync);
            LimpiarFiltrosCommand = new RelayCommand(LimpiarFiltros);
            VerDetallesCommand = new RelayCommand(VerDetalles, PuedeVerDetalles);
            CambiarASgaActualCommand = new RelayCommand(CambiarASgaActual);
            CambiarAStorageControlCommand = new RelayCommand(CambiarAStorageControl);
            CambiarACombinadoCommand = new RelayCommand(CambiarACombinado);
            AplicarFiltrosStorageControlCommand = new AsyncRelayCommand(AplicarFiltrosStorageControlAsync);
            
            // Inicializar comandos para dropdown de almacenes origen
            AbrirDropDownAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesOrigen = "";
                IsDropDownOpenAlmacenesOrigen = true;
            });
            
            CerrarDropDownAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenesOrigen = false;
            });
            
            LimpiarSeleccionAlmacenesOrigenCommand = new RelayCommand(() =>
            {
                // No necesitamos limpiar selección aquí, solo actualizar el filtro
            });
            
            // Inicializar comandos para dropdown de almacenes destino
            AbrirDropDownAlmacenesDestinoCommand = new RelayCommand(() =>
            {
                FiltroAlmacenesDestino = "";
                IsDropDownOpenAlmacenesDestino = true;
            });
            
            CerrarDropDownAlmacenesDestinoCommand = new RelayCommand(() =>
            {
                IsDropDownOpenAlmacenesDestino = false;
            });
            
            LimpiarSeleccionAlmacenesDestinoCommand = new RelayCommand(() =>
            {
                // No necesitamos limpiar selección aquí, solo actualizar el filtro
            });

            // Inicialización
            _ = InitializeAsync();
        }

        public TraspasoHistoricoViewModel() : this(new TraspasosService()) { }

        // Validaciones de fechas sin carga automática
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

        // Los cambios en filtros no cargan automáticamente - el usuario debe presionar "Aplicar filtros"
        
        partial void OnCodigoArticuloChanged(string value)
        {
            // Notificar cambio en PuedeFiltrarPorLote
            OnPropertyChanged(nameof(PuedeFiltrarPorLote));
            
            // Si se limpia el artículo, limpiar también el lote
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

        partial void OnFiltroObservacionesChanged(string value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenOrigenSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnEstadoSeleccionadoChanged(EstadoTraspasoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        partial void OnOperarioSeleccionadoChanged(OperariosAccesoDto? value)
        {
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }
        
        partial void OnFiltroOperariosChanged(string value)
        {
            OperariosView.Refresh(); // Actualiza el filtrado al teclear
        }
        
        // Métodos para manejar cambios en los filtros de almacenes
        partial void OnFiltroAlmacenesOrigenChanged(string value)
        {
            AlmacenesOrigenView?.Refresh();
        }
        
        partial void OnFiltroAlmacenesDestinoChanged(string value)
        {
            AlmacenesDestinoView?.Refresh();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Establecer fechas por defecto (últimos días para ver traspasos recientes)
                FechaDesde = DateTime.Today; // Fecha de hoy
                FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00

                // Cargar almacenes
                await CargarAlmacenesAsync();

                // Cargar operarios
                await CargarOperariosAsync();

                // Cargar estados
                await CargarEstadosAsync();
                
                // Establecer "-- Todos los estados --" como selección por defecto
                EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado));

                // Cargar automáticamente los traspasos del día actual
                await CargarTraspasosAsync();
            }
            catch (Exception ex)
            {
                // Manejar error de inicialización
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

                AlmacenesOrigen.Clear();
                AlmacenesDestino.Clear();

                foreach (var almacen in almacenes)
                {
                    AlmacenesOrigen.Add(almacen);
                    AlmacenesDestino.Add(almacen);
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
                
                // Opción para mostrar todos los estados
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "", Descripcion = "-- Todos los estados --" });
                
                // Estados reales del sistema de traspasos
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "PENDIENTE", Descripcion = "Pendiente" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "PENDIENTE_ERP", Descripcion = "Pendiente ERP" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "ERROR_ERP", Descripcion = "Error ERP" });
                Estados.Add(new EstadoTraspasoDto { CodigoEstado = "COMPLETADO", Descripcion = "Completado" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando estados: {ex.Message}");
            }
        }

        private async Task CargarTraspasosAsync()
        {
            try
            {
                EstaCargando = true;
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                
                // Asegurar que las fechas estén bien configuradas (misma lógica que InventarioViewModel)
                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;
                
                System.Diagnostics.Debug.WriteLine($"Cargando traspasos desde: {fechaDesde:yyyy-MM-dd} hasta: {fechaHasta:yyyy-MM-dd}");
                
                // Determinar el estado para el filtro (si es vacío, no filtrar por estado)
                var estadoFiltro = EstadoSeleccionado?.CodigoEstado;
                if (string.IsNullOrEmpty(estadoFiltro))
                {
                    estadoFiltro = null; // No filtrar por estado
                }
                
                var codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                    ? CodigoPalet.Trim()
                    : null;
                var observacionesFiltro = !string.IsNullOrWhiteSpace(FiltroObservaciones) && FiltroObservaciones.Trim().Length >= 5
                    ? FiltroObservaciones.Trim()
                    : null;

                var traspasos = await _traspasosService.ObtenerTraspasosFiltradosAsync(
                    estado: estadoFiltro,
                    codigoPalet: codigoPaletFiltro,
                    almacenOrigen: AlmacenOrigenSeleccionado?.CodigoAlmacen,
                    almacenDestino: AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    fechaInicioDesde: fechaDesde.Date, // Solo la fecha, hora 00:00:00
                    fechaInicioHasta: fechaHasta.Date // Solo la fecha, la API se encarga de incluir todo el día
                );

                System.Diagnostics.Debug.WriteLine($"API devolvió {traspasos.Count} traspasos");

                Traspasos.Clear();
                
                // 🔒 FILTRO DE SEGURIDAD: Aplicar filtro automático por almacenes permitidos del usuario
                var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                var traspasosFiltrados = traspasos.Where(t => 
                    almacenesPermitidos.Contains(t.AlmacenOrigen) || 
                    almacenesPermitidos.Contains(t.AlmacenDestino)
                ).ToList();
                
                System.Diagnostics.Debug.WriteLine($"Después del filtro de almacenes permitidos: {traspasosFiltrados.Count} traspasos");
                
                // Aplicar filtros adicionales (artículo y operario)
                var traspasosFiltradosFinal = traspasosFiltrados;
                
                if (codigoPaletFiltro != null)
                {
                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t =>
                        !string.IsNullOrWhiteSpace(t.CodigoPalet) &&
                        t.CodigoPalet.Contains(codigoPaletFiltro, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"Después del filtro de palet: {traspasosFiltradosFinal.Count} traspasos");
                }
                else if (!string.IsNullOrWhiteSpace(CodigoPalet))
                {
                    System.Diagnostics.Debug.WriteLine($"Filtro de palet ignorado: se necesitan al menos 3 caracteres (actual: {CodigoPalet.Trim().Length})");
                }

                if (observacionesFiltro != null)
                {
                    var observacionesFiltroNormalizado = NormalizarIdentificadorOrden(observacionesFiltro);

                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t =>
                    {
                        bool coincideComentario = !string.IsNullOrWhiteSpace(t.Comentarios) &&
                            (t.Comentarios.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                             NormalizarIdentificadorOrden(t.Comentarios).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase));

                        bool coincideOrdenTrabajo = !string.IsNullOrWhiteSpace(t.OrdenTrabajoId) &&
                            (t.OrdenTrabajoId.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                             NormalizarIdentificadorOrden(t.OrdenTrabajoId).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase));

                        return coincideComentario || coincideOrdenTrabajo;
                    }).ToList();

                    System.Diagnostics.Debug.WriteLine($"Después del filtro de observaciones: {traspasosFiltradosFinal.Count}");
                }
                else if (!string.IsNullOrWhiteSpace(FiltroObservaciones))
                {
                    System.Diagnostics.Debug.WriteLine($"Filtro de observaciones ignorado: se necesitan al menos 5 caracteres (actual: {FiltroObservaciones.Trim().Length})");
                }
                
                // Filtro por código de artículo
                if (!string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t => 
                        !string.IsNullOrWhiteSpace(t.CodigoArticulo) && 
                        t.CodigoArticulo.Contains(CodigoArticulo, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"Después del filtro de artículo: {traspasosFiltradosFinal.Count} traspasos");
                }
                
                // Filtro por lote (solo si hay artículo escrito)
                if (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t => 
                        (!string.IsNullOrWhiteSpace(t.Partida) && 
                        t.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)) ||
                        (t.LineasPalet != null && t.LineasPalet.Any(l => 
                            !string.IsNullOrWhiteSpace(l.Lote) && 
                            l.Lote.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"Después del filtro de lote: {traspasosFiltradosFinal.Count} traspasos");
                }
                
                // Filtro por operario seleccionado
                if (OperarioSeleccionado != null && OperarioSeleccionado.Operario > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Aplicando filtro de operario: {OperarioSeleccionado.Operario}");
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] OperarioSeleccionado.Operario tipo: {OperarioSeleccionado.Operario.GetType()}");
                    
                    // Log de los usuarios que tienen los traspasos antes del filtro
                    var usuariosEnTraspasos = traspasosFiltradosFinal.Select(t => t.UsuarioInicioId).Distinct().Take(10).ToList();
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Usuarios en traspasos (primeros 10): {string.Join(", ", usuariosEnTraspasos)}");
                    
                    // Log de tipos de datos para debugging
                    if (traspasosFiltradosFinal.Any())
                    {
                        var primerTraspaso = traspasosFiltradosFinal.First();
                        System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Primer traspaso UsuarioInicioId: {primerTraspaso.UsuarioInicioId} (tipo: {primerTraspaso.UsuarioInicioId.GetType()})");
                    }
                    
                    traspasosFiltradosFinal = traspasosFiltradosFinal.Where(t => 
                        t.UsuarioInicioId == OperarioSeleccionado.Operario
                    ).ToList();
                    
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Después del filtro de operario: {traspasosFiltradosFinal.Count} traspasos");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] No se aplica filtro de operario (OperarioSeleccionado: {OperarioSeleccionado?.Operario})");
                }

                // Resolver nombres de operarios
                var operariosDict = OperariosDisponibles.ToDictionary(o => o.Operario.ToString(), o => ExtraerSoloNombre(o.NombreCompleto ?? "Sin nombre"));

                foreach (var traspaso in traspasosFiltradosFinal.OrderByDescending(t => t.FechaInicio))
                {
                    // Resolver nombre del operario de inicio
                    if (traspaso.UsuarioInicioId > 0 && string.IsNullOrEmpty(traspaso.UsuarioInicioNombre))
                    {
                        traspaso.UsuarioInicioNombre = operariosDict.GetValueOrDefault(traspaso.UsuarioInicioId.ToString(), $"ID: {traspaso.UsuarioInicioId}");
                    }

                    // Resolver nombre del operario de finalización si existe
                    if (traspaso.UsuarioFinalizacionId.HasValue && traspaso.UsuarioFinalizacionId > 0 && string.IsNullOrEmpty(traspaso.UsuarioFinalizacionNombre))
                    {
                        traspaso.UsuarioFinalizacionNombre = operariosDict.GetValueOrDefault(traspaso.UsuarioFinalizacionId.ToString(), $"ID: {traspaso.UsuarioFinalizacionId}");
                    }

                    Traspasos.Add(traspaso);
                    System.Diagnostics.Debug.WriteLine($"Traspaso: {traspaso.CodigoArticulo} - {traspaso.FechaInicio:yyyy-MM-dd HH:mm}");
                }
                
                System.Diagnostics.Debug.WriteLine($"Total final: {Traspasos.Count} traspasos");
                
                // Si la pestaña combinada está visible, actualizar la lista combinada
                if (MostrandoCombinado)
                {
                    ActualizarTraspasosCombinados();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando traspasos: {ex.Message}");
                // Aquí podrías mostrar un mensaje de error al usuario
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task AplicarFiltrosAsync()
        {
            if (!ValidarFiltroObservacionesMinimo())
                return;

            // Aplicar filtros a ambas pestañas
            await CargarTraspasosAsync(); // SGA Actual
            
            // Si la pestaña StorageControl está visible o tiene datos, también aplicar filtros ahí
            if (MostrandoStorageControl || TraspasosStorageControl.Any())
            {
                await CargarTraspasosStorageControlAsync();
            }
            
            // Si la pestaña combinada está visible, actualizar la lista combinada
            if (MostrandoCombinado)
            {
                ActualizarTraspasosCombinados();
            }

            // Notificar cambios en propiedades calculadas después de aplicar filtros
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
        }

        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today; // Fecha de hoy
            FechaHasta = DateTime.Today; // Solo la fecha, hora 00:00:00
            CodigoArticulo = "";
            CodigoLote = "";
            CodigoPalet = "";
            FiltroObservaciones = "";
            OperarioSeleccionado = null;
            AlmacenOrigenSeleccionado = null;
            AlmacenDestinoSeleccionado = null;
            EstadoSeleccionado = Estados.FirstOrDefault(e => string.IsNullOrEmpty(e.CodigoEstado)); // "-- Todos los estados --"
            
            // Notificar cambios en propiedades calculadas
            OnPropertyChanged(nameof(TieneFiltrosActivos));
            OnPropertyChanged(nameof(ResumenFiltrosActivos));
            
            // Limpiar la lista de traspasos
            Traspasos.Clear();
        }

        private async Task CargarOperariosAsync()
        {
            try
            {
                // Intentar permiso específico para traspasos (permiso 12)
                var operarios = await _loginService.ObtenerOperariosConAccesoTraspasosAsync();

                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Operarios con permiso 12: {operarios.Count}");

                OperariosDisponibles.Clear();

                // Si no hay operarios con permiso 12, usar fallback automáticamente
                if (operarios.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[TraspasoHistorico] No hay operarios con permiso 12, usando fallback a permiso 13");
                    var operariosFallback = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                    operarios = operariosFallback;
                }

                foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
                {
                    OperariosDisponibles.Add(operario);
                }

                System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Total operarios cargados: {OperariosDisponibles.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
                // Fallback final: usar operarios de conteos
                try
                {
                    var operariosFallback = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                    OperariosDisponibles.Clear();
                    foreach (var operario in operariosFallback.OrderBy(o => o.NombreOperario))
                    {
                        OperariosDisponibles.Add(operario);
                    }
                    System.Diagnostics.Debug.WriteLine($"[TraspasoHistorico] Fallback: {OperariosDisponibles.Count} operarios cargados");
                }
                catch
                {
                    OperariosDisponibles.Clear();
                    System.Diagnostics.Debug.WriteLine("[TraspasoHistorico] Error total: No se pudieron cargar operarios");
                }
            }
        }

        private bool PuedeVerDetalles()
        {
            return TraspasoSeleccionado != null;
        }

        private void VerDetalles()
        {
            if (TraspasoSeleccionado == null) return;

            // Aquí puedes implementar la lógica para mostrar detalles del traspaso
            // Por ejemplo, abrir otro dialog con información detallada
            System.Diagnostics.Debug.WriteLine($"Ver detalles del traspaso: {TraspasoSeleccionado.Id}");
        }

        private void AbrirFiltros()
        {
            var dlg = new HistorialTraspasosFiltrosDialog
            {
                DataContext = this
            };
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow;
            if (owner != null && owner != dlg)
                dlg.Owner = owner;
            dlg.ShowDialog();
        }

        /// <summary>
        /// Extrae solo el nombre del operario, quitando el código al inicio
        /// Ejemplo: "1226 - RIVERO CAMPOS, JOSE MANUEL" -> "RIVERO CAMPOS, JOSE MANUEL"
        /// </summary>
        private static string ExtraerSoloNombre(string nombreCompleto)
        {
            if (string.IsNullOrEmpty(nombreCompleto))
                return "Sin nombre";
                
            // Buscar el patrón "CÓDIGO - NOMBRE" y extraer solo el nombre
            var indiceGuion = nombreCompleto.IndexOf(" - ");
            if (indiceGuion > 0)
            {
                return nombreCompleto.Substring(indiceGuion + 3).Trim();
            }
            
            // Si no tiene el formato esperado, devolver tal como está
            return nombreCompleto.Trim();
        }

        // Comandos para controlar el dropdown de operarios
        [RelayCommand]
        private void AbrirDropDownOperarios()
        {
            FiltroOperarios = ""; // Limpiar el filtro para permitir escribir desde cero
            IsDropDownOpenOperarios = true;
        }

        [RelayCommand]
        private void CerrarDropDownOperarios()
        {
            IsDropDownOpenOperarios = false;
        }

        // Método de filtrado para operarios (búsqueda en cualquier parte del texto)
        private bool FiltraOperario(object obj)
        {
            if (string.IsNullOrWhiteSpace(FiltroOperarios)) return true;
            if (obj is not OperariosAccesoDto operario) return false;

            // Búsqueda acento-insensible, sin mayúsc/minúsc, en cualquier parte del texto
            var compare = CultureInfo.CurrentCulture.CompareInfo;
            var options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

            bool contiene(string s) =>
                !string.IsNullOrEmpty(s) &&
                compare.IndexOf(s, FiltroOperarios, options) >= 0;

            return contiene(operario.NombreOperario) || contiene(operario.NombreCompleto);
        }
        
        // Método de filtrado para almacenes origen
        private bool FiltraAlmacenesOrigen(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesOrigen)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesOrigen, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        // Método de filtrado para almacenes destino
        private bool FiltraAlmacenesDestino(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesDestino)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesDestino, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // 🔒 MÉTODO DE SEGURIDAD: Obtener almacenes permitidos del usuario
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
                
                // Retornar solo los códigos de almacén permitidos
                return almacenesAutorizados.Select(a => a.CodigoAlmacen).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo almacenes permitidos: {ex.Message}");
                // En caso de error, retornar lista vacía para máxima seguridad
                return new List<string>();
            }
        }

        // Comandos para cambiar de pestaña
        private void CambiarASgaActual()
        {
            MostrandoSgaActual = true;
            MostrandoStorageControl = false;
            MostrandoCombinado = false;
        }

        private void CambiarAStorageControl()
        {
            MostrandoSgaActual = false;
            MostrandoStorageControl = true;
            MostrandoCombinado = false;
            
            // Cargar traspasos de StorageControl si la colección está vacía
            if (!TraspasosStorageControl.Any())
            {
                _ = CargarTraspasosStorageControlAsync();
            }
        }
        
        private void CambiarACombinado()
        {
            MostrandoSgaActual = false;
            MostrandoStorageControl = false;
            MostrandoCombinado = true;
            ActualizarTraspasosCombinados();
        }
        
        private void ActualizarTraspasosCombinados()
        {
            TraspasosCombinados.Clear();
            
            // Agregar traspasos de SGA Actual
            foreach (var traspaso in Traspasos)
            {
                traspaso.Fuente = "SGA_Actual";
                TraspasosCombinados.Add(traspaso);
            }
            
            // Agregar traspasos de StorageControl
            foreach (var traspaso in TraspasosStorageControl)
            {
                traspaso.Fuente = "SAGE";
                TraspasosCombinados.Add(traspaso);
            }
            
            // Ordenar por fecha descendente (más recientes primero)
            // Para SGA Actual: usar FechaInicio
            // Para StorageControl: usar FechaRegistro o Fecha
            var traspasosOrdenados = TraspasosCombinados
                .OrderByDescending(t => 
                {
                    if (t is TraspasoDto traspasoSga)
                        return traspasoSga.FechaInicio;
                    if (t is TraspasoStorageControlDto traspasoStorage)
                        return traspasoStorage.FechaRegistro ?? traspasoStorage.Fecha;
                    return DateTime.MinValue;
                })
                .ToList();
            
            TraspasosCombinados.Clear();
            foreach (var traspaso in traspasosOrdenados)
            {
                TraspasosCombinados.Add(traspaso);
            }
        }

        private async Task CargarTraspasosStorageControlAsync()
        {
            try
            {
                EstaCargando = true;
                
                var fechaDesde = FechaDesde ?? DateTime.Today;
                var fechaHasta = FechaHasta ?? DateTime.Today;
                
                var codigoPaletFiltro = !string.IsNullOrWhiteSpace(CodigoPalet) && CodigoPalet.Trim().Length >= 3
                    ? CodigoPalet.Trim()
                    : null;
                var observacionesFiltro = !string.IsNullOrWhiteSpace(FiltroObservaciones) && FiltroObservaciones.Trim().Length >= 5
                    ? FiltroObservaciones.Trim()
                    : null;

                var partidaFiltro = (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
                    ? CodigoLote
                    : null;

                var traspasos = await _traspasosService.ObtenerTraspasosStorageControlAsync(
                    fechaDesde: fechaDesde.Date,
                    fechaHasta: fechaHasta.Date,
                    almacenOrigen: AlmacenOrigenSeleccionado?.CodigoAlmacen,
                    almacenDestino: AlmacenDestinoSeleccionado?.CodigoAlmacen,
                    codigoArticulo: string.IsNullOrWhiteSpace(CodigoArticulo) ? null : CodigoArticulo,
                    partida: partidaFiltro
                );

                TraspasosStorageControl.Clear();
                
                // 🔒 FILTRO DE SEGURIDAD: Aplicar filtro automático por almacenes permitidos del usuario
                var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Traspasos recibidos del API: {traspasos.Count}");
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Almacenes permitidos: {string.Join(", ", almacenesPermitidos)}");
                
                var traspasosFiltrados = traspasos.Where(t => 
                    (t.AlmacenOrigen != null && almacenesPermitidos.Contains(t.AlmacenOrigen)) || 
                    (t.AlmacenDestino != null && almacenesPermitidos.Contains(t.AlmacenDestino))
                ).ToList();
                
                var traspasosDescartados = traspasos.Count - traspasosFiltrados.Count;
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Traspasos después del filtro de almacenes: {traspasosFiltrados.Count}");
                System.Diagnostics.Debug.WriteLine($"[StorageControl] Traspasos descartados por filtro de almacenes: {traspasosDescartados}");

                if (codigoPaletFiltro != null)
                {
                    traspasosFiltrados = traspasosFiltrados.Where(t =>
                        !string.IsNullOrWhiteSpace(t.CodigoPalet) &&
                        t.CodigoPalet.Contains(codigoPaletFiltro, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Después del filtro de palet: {traspasosFiltrados.Count}");
                }
                else if (!string.IsNullOrWhiteSpace(CodigoPalet))
                {
                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Filtro de palet ignorado: se necesitan al menos 3 caracteres (actual: {CodigoPalet.Trim().Length})");
                }

                if (observacionesFiltro != null)
                {
                    var observacionesFiltroNormalizado = NormalizarIdentificadorOrden(observacionesFiltro);

                    traspasosFiltrados = traspasosFiltrados.Where(t =>
                        !string.IsNullOrWhiteSpace(t.Comentario) &&
                        (t.Comentario.Contains(observacionesFiltro, StringComparison.OrdinalIgnoreCase) ||
                         NormalizarIdentificadorOrden(t.Comentario).Contains(observacionesFiltroNormalizado, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Después del filtro de observaciones: {traspasosFiltrados.Count}");
                }
                else if (!string.IsNullOrWhiteSpace(FiltroObservaciones))
                {
                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Filtro de observaciones ignorado: se necesitan al menos 5 caracteres (actual: {FiltroObservaciones.Trim().Length})");
                }

                // Filtro por lote (solo si hay artículo escrito y el API no lo filtró)
                if (!string.IsNullOrWhiteSpace(CodigoLote) && !string.IsNullOrWhiteSpace(CodigoArticulo))
                {
                    traspasosFiltrados = traspasosFiltrados.Where(t =>
                        !string.IsNullOrWhiteSpace(t.Partida) &&
                        t.Partida.Contains(CodigoLote, StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    System.Diagnostics.Debug.WriteLine($"[StorageControl] Después del filtro de lote: {traspasosFiltrados.Count}");
                }

                foreach (var traspaso in traspasosFiltrados.OrderByDescending(t => t.FechaRegistro ?? t.Fecha))
                {
                    TraspasosStorageControl.Add(traspaso);
                }
                
                // Si la pestaña combinada está visible, actualizar la lista combinada
                if (MostrandoCombinado)
                {
                    ActualizarTraspasosCombinados();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando traspasos StorageControl: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task AplicarFiltrosStorageControlAsync()
        {
            if (!ValidarFiltroObservacionesMinimo())
                return;

            await CargarTraspasosStorageControlAsync();
        }

        private bool ValidarFiltroObservacionesMinimo()
        {
            if (!string.IsNullOrWhiteSpace(FiltroObservaciones) && FiltroObservaciones.Trim().Length < 5)
            {
                MostrarWarningDialog("Filtro de observaciones", "Introduce al menos 5 caracteres para filtrar por observaciones.");
                return false;
            }

            return true;
        }

        private void MostrarWarningDialog(string title, string message, string iconGlyph = "\uE7BA")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new WarningDialog(title, message, iconGlyph);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                if (owner != null && owner != dialog)
                {
                    dialog.Owner = owner;
                }
                dialog.ShowDialog();
            });
        }

        private static string NormalizarIdentificadorOrden(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return string.Empty;

            var builder = new System.Text.StringBuilder(valor.Length);
            foreach (var ch in valor)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToUpperInvariant(ch));
            }
            return builder.ToString();
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
        private void CopiarAlmacenOrigen(string almacen)
        {
            if (!string.IsNullOrWhiteSpace(almacen))
                Clipboard.SetText(almacen);
        }

        [RelayCommand]
        private void CopiarUbicacionOrigen(string ubicacion)
        {
            if (!string.IsNullOrWhiteSpace(ubicacion))
                Clipboard.SetText(ubicacion);
        }

        [RelayCommand]
        private void CopiarAlmacenDestino(string almacen)
        {
            if (!string.IsNullOrWhiteSpace(almacen))
                Clipboard.SetText(almacen);
        }

        [RelayCommand]
        private void CopiarUbicacionDestino(string ubicacion)
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
        private void CopiarCantidad(decimal? cantidad)
        {
            if (cantidad.HasValue)
                Clipboard.SetText(cantidad.Value.ToString("0.########"));
        }

        [RelayCommand]
        private void CopiarUsuario(string usuario)
        {
            if (!string.IsNullOrWhiteSpace(usuario))
                Clipboard.SetText(usuario);
        }

        [RelayCommand]
        private void CopiarComentarios(string comentarios)
        {
            if (!string.IsNullOrWhiteSpace(comentarios))
                Clipboard.SetText(comentarios);
        }

        [RelayCommand]
        private void CopiarTipoTraspaso(string tipoTraspaso)
        {
            if (!string.IsNullOrWhiteSpace(tipoTraspaso))
                Clipboard.SetText(tipoTraspaso);
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
    }
}

