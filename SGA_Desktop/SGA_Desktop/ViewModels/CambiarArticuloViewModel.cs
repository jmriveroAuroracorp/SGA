using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class CambiarArticuloViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        private readonly StockService _stockService;
        private readonly PaletService _paletService;
        #endregion

        #region Constructor
        public CambiarArticuloViewModel(InventarioService inventarioService, StockService stockService, PaletService paletService)
        {
            _inventarioService = inventarioService;
            _stockService = stockService;
            _paletService = paletService;

            Almacenes = new ObservableCollection<AlmacenDto>();
            Ubicaciones = new ObservableCollection<UbicacionDto>();
            PartidasDisponibles = new ObservableCollection<LoteDto>();
            FechasDisponibles = new ObservableCollection<DateTime?>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _ = InitializeAsync();
            }
        }

        public CambiarArticuloViewModel() : this(new InventarioService(), new StockService(), new PaletService()) { }
        #endregion

        #region Observable Properties - Tipo de Cambio
        [ObservableProperty]
        private bool esCambioCodigo = true;

        [ObservableProperty]
        private bool esCambioFecha = false;
        #endregion

        #region Observable Properties - Artículo Origen
        [ObservableProperty]
        private string codigoArticuloOrigen = string.Empty;

        [ObservableProperty]
        private string descripcionArticuloOrigen = string.Empty;

        [ObservableProperty]
        private string codigoAlmacen = string.Empty;

        [ObservableProperty]
        private ObservableCollection<AlmacenDto> almacenes;

        [ObservableProperty]
        private AlmacenDto? almacenSeleccionado;

        [ObservableProperty]
        private string? ubicacion = string.Empty;

        [ObservableProperty]
        private ObservableCollection<UbicacionDto> ubicaciones;

        [ObservableProperty]
        private UbicacionDto? ubicacionSeleccionada;

        [ObservableProperty]
        private string? partida = string.Empty;

        [ObservableProperty]
        private ObservableCollection<LoteDto> partidasDisponibles;

        [ObservableProperty]
        private LoteDto? partidaSeleccionada;

        [ObservableProperty]
        private DateTime? fechaCaducidadOrigen;

        [ObservableProperty]
        private ObservableCollection<DateTime?> fechasDisponibles;

        private Dictionary<string, List<DateTime?>> _fechasPorPartida = new Dictionary<string, List<DateTime?>>();

        [ObservableProperty]
        private decimal cantidad = 0;

        [ObservableProperty]
        private decimal stockDisponible = 0;

        [ObservableProperty]
        private Guid? paletId;
        #endregion

        #region Observable Properties - Artículo Destino
        [ObservableProperty]
        private string codigoArticuloDestino = string.Empty;

        [ObservableProperty]
        private string descripcionArticuloDestino = string.Empty;

        [ObservableProperty]
        private DateTime? fechaCaducidadDestino;

        [ObservableProperty]
        private string? partidaDestino = string.Empty;

        private string? _partidaOriginal = string.Empty; // Almacena el lote original para validación
        #endregion

        #region Property Change Callbacks - Partida Destino
        partial void OnPartidaDestinoChanged(string? oldValue, string? newValue)
        {
            // Si no hay lote original guardado o estamos en modo cambio de código, no validar
            if (string.IsNullOrWhiteSpace(_partidaOriginal) || !EsCambioFecha)
                return;

            // Si el nuevo valor está vacío, permitirlo (se validará al guardar)
            if (string.IsNullOrWhiteSpace(newValue))
                return;

            // Validar que el lote original esté contenido en el nuevo valor
            if (!newValue.Contains(_partidaOriginal))
            {
                // Revertir al valor anterior o al lote original
                PartidaDestino = oldValue ?? _partidaOriginal;
                ShowDialog(new WarningDialog("Validación", 
                    $"El lote original '{_partidaOriginal}' debe mantenerse intacto. Solo puede agregar texto al inicio o al final."));
                return;
            }

            // Validar que solo se haya agregado al inicio o al final (no en el medio)
            int indiceOriginal = newValue.IndexOf(_partidaOriginal);
            if (indiceOriginal < 0)
            {
                // No se encontró el lote original, revertir
                PartidaDestino = oldValue ?? _partidaOriginal;
                ShowDialog(new WarningDialog("Validación", 
                    $"El lote original '{_partidaOriginal}' debe mantenerse intacto. Solo puede agregar texto al inicio o al final."));
                return;
            }

            // Verificar que el lote original esté al inicio o al final (o ambos)
            bool estaAlInicio = indiceOriginal == 0;
            bool estaAlFinal = (indiceOriginal + _partidaOriginal.Length) == newValue.Length;
            
            if (!estaAlInicio && !estaAlFinal)
            {
                // El lote original está en el medio, revertir
                PartidaDestino = oldValue ?? _partidaOriginal;
                ShowDialog(new WarningDialog("Validación", 
                    $"El lote original '{_partidaOriginal}' debe mantenerse intacto. Solo puede agregar texto al inicio o al final."));
                return;
            }
        }
        #endregion

        #region Observable Properties - Estado
        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;
        #endregion

        #region Property Change Callbacks
        partial void OnEsCambioCodigoChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                EsCambioFecha = false;
                CodigoArticuloDestino = string.Empty;
                DescripcionArticuloDestino = string.Empty;
                PartidaDestino = string.Empty;
                _partidaOriginal = string.Empty; // Limpiar el lote original al cambiar a cambio de código
            }
        }

        partial void OnEsCambioFechaChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                EsCambioCodigo = false;
                FechaCaducidadDestino = null;
                // Copiar el lote original al campo de destino para que pueda modificarlo
                // Usar PartidaSeleccionada si está disponible, sino usar Partida
                string? loteOriginal = null;
                if (PartidaSeleccionada != null && !string.IsNullOrWhiteSpace(PartidaSeleccionada.Partida))
                {
                    loteOriginal = PartidaSeleccionada.Partida;
                }
                else if (!string.IsNullOrWhiteSpace(Partida))
                {
                    loteOriginal = Partida;
                }
                
                _partidaOriginal = loteOriginal ?? string.Empty;
                PartidaDestino = _partidaOriginal;
            }
        }

        partial void OnAlmacenSeleccionadoChanged(AlmacenDto? oldValue, AlmacenDto? newValue)
        {
            if (newValue != null)
            {
                CodigoAlmacen = newValue.CodigoAlmacen;
                // Limpiar selección de ubicación y recargar ubicaciones del nuevo almacén
                UbicacionSeleccionada = null;
                Ubicacion = string.Empty;
                PartidaSeleccionada = null;
                Partida = string.Empty;
                FechaCaducidadOrigen = null;
                FechasDisponibles.Clear();
                _fechasPorPartida.Clear();
                _ = CargarUbicacionesAsync();
            }
        }

        partial void OnUbicacionSeleccionadaChanged(UbicacionDto? oldValue, UbicacionDto? newValue)
        {
            if (newValue != null)
            {
                Ubicacion = newValue.Ubicacion;
                // Si ya hay artículo seleccionado, cargar lotes
                if (!string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                {
                    _ = CargarLotesActivosAsync();
                }
                // Actualizar stock disponible
                if (!string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                {
                    _ = CargarStockDisponibleAsync();
                }
            }
        }

        partial void OnPartidaSeleccionadaChanged(LoteDto? oldValue, LoteDto? newValue)
        {
            FechasDisponibles.Clear();
            FechaCaducidadOrigen = null;

            if (newValue != null && !string.IsNullOrWhiteSpace(newValue.Partida))
            {
                Partida = newValue.Partida;

                // Si estamos en modo ampliación, copiar el lote al campo de destino
                if (EsCambioFecha)
                {
                    _partidaOriginal = newValue.Partida;
                    PartidaDestino = newValue.Partida;
                }

                // Cargar fechas desde el diccionario
                if (_fechasPorPartida.ContainsKey(newValue.Partida))
                {
                    var fechas = _fechasPorPartida[newValue.Partida];

                    // Agregar todas las fechas al ComboBox
                    foreach (var fecha in fechas)
                    {
                        FechasDisponibles.Add(fecha);
                    }

                    // Si hay una sola fecha, seleccionarla automáticamente
                    if (fechas.Count == 1)
                    {
                        FechaCaducidadOrigen = fechas[0];
                    }
                }

                // Actualizar stock disponible
                _ = CargarStockDisponibleAsync();
            }
        }

        partial void OnFechaCaducidadOrigenChanged(DateTime? oldValue, DateTime? newValue)
        {
            // Actualizar stock disponible cuando cambia la fecha
            if (!string.IsNullOrWhiteSpace(CodigoArticuloOrigen) && !string.IsNullOrWhiteSpace(Ubicacion))
            {
                _ = CargarStockDisponibleAsync();
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                IsCargando = true;
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new System.Collections.Generic.List<string>();

                // Obtener almacenes autorizados
                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

                if (almacenesAutorizados == null || !almacenesAutorizados.Any())
                {
                    MensajeEstado = "No hay almacenes autorizados disponibles";
                    return;
                }

                // Mostrar todos los almacenes autorizados (el filtro de ubicaciones con stock ya está implementado)
                Almacenes.Clear();
                foreach (var almacen in almacenesAutorizados.OrderBy(a => a.NombreAlmacen))
                {
                    Almacenes.Add(almacen);
                }

                if (Almacenes.Count == 1)
                {
                    AlmacenSeleccionado = Almacenes.First();
                }
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al inicializar: {ex.Message}"));
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task CargarUbicacionesAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CodigoAlmacen))
                    return;

                IsCargando = true;
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;

                // Si hay un artículo seleccionado, filtrar ubicaciones que tienen stock de ese artículo
                if (!string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                {
                    var stock = await _stockService.ObtenerPorArticuloAsync(
                        empresa,
                        CodigoArticuloOrigen,
                        codigoAlmacen: CodigoAlmacen);

                    // Obtener ubicaciones únicas que tienen stock del artículo (incluyendo ubicación vacía "")
                    var ubicacionesConStock = stock
                        .Where(s => s.UnidadSaldo > 0)
                        .Select(s => s.Ubicacion ?? string.Empty)
                        .Distinct()
                        .OrderBy(u => u)
                        .ToList();

                    // Obtener todas las ubicaciones del almacén para tener los objetos completos
                    var todasLasUbicaciones = await _stockService.ObtenerUbicacionesAsync(CodigoAlmacen, (short)empresa, soloConStock: false);

                    // Filtrar solo las que tienen stock del artículo
                    Ubicaciones.Clear();
                    
                    // Primero agregar "SIN UBICAR" si existe
                    if (ubicacionesConStock.Contains(string.Empty))
                    {
                        var ubicacionSinUbicar = todasLasUbicaciones.FirstOrDefault(u => string.IsNullOrEmpty(u.Ubicacion));
                        if (ubicacionSinUbicar != null)
                        {
                            Ubicaciones.Add(ubicacionSinUbicar);
                        }
                        else
                        {
                            // Si no existe en la lista pero hay stock sin ubicación, crear un objeto para "Sin ubicación"
                            Ubicaciones.Add(new UbicacionDto
                            {
                                Ubicacion = string.Empty,
                                CodigoAlmacen = CodigoAlmacen
                            });
                        }
                    }
                    
                    // Luego agregar las demás ubicaciones ordenadas
                    foreach (var ubicacionCodigo in ubicacionesConStock.Where(u => !string.IsNullOrEmpty(u)))
                    {
                        var ubicacion = todasLasUbicaciones.FirstOrDefault(u => u.Ubicacion == ubicacionCodigo);
                        if (ubicacion != null)
                        {
                            Ubicaciones.Add(ubicacion);
                        }
                    }
                }
                else
                {
                    // Si no hay artículo seleccionado, mostrar todas las ubicaciones con stock
                    var ubicacionesList = await _stockService.ObtenerUbicacionesAsync(CodigoAlmacen, (short)empresa, soloConStock: true);

                    Ubicaciones.Clear();
                    foreach (var ubicacion in ubicacionesList.OrderBy(u => u.Ubicacion))
                    {
                        Ubicaciones.Add(ubicacion);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al cargar ubicaciones: {ex.Message}"));
            }
            finally
            {
                IsCargando = false;
            }
        }

        [RelayCommand]
        private async Task BuscarArticuloOrigenAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                {
                    ShowDialog(new WarningDialog("Buscar artículo", "Introduce un código de artículo para buscar."));
                    return;
                }

                IsCargando = true;
                MensajeEstado = "Buscando artículo...";

                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var codigoArticuloBuscado = CodigoArticuloOrigen.Trim();

                var articulo = await _stockService.BuscarArticuloPorCodigoAsync(empresa, codigoArticuloBuscado);

                if (articulo == null)
                {
                    MensajeEstado = "Artículo no encontrado";
                    DescripcionArticuloOrigen = string.Empty;
                    StockDisponible = 0;
                    return;
                }

                CodigoArticuloOrigen = articulo.CodigoArticulo;
                DescripcionArticuloOrigen = articulo.DescripcionArticulo ?? string.Empty;
                MensajeEstado = "Artículo encontrado.";

                // Filtrar almacenes para mostrar solo los que tienen stock de este artículo
                await FiltrarAlmacenesPorStockAsync();

                // Limpiar lotes y partidas hasta que se seleccione almacén y ubicación
                PartidasDisponibles.Clear();
                PartidaSeleccionada = null;
                _fechasPorPartida.Clear();
                FechaCaducidadOrigen = null;
                FechasDisponibles.Clear();
                Partida = string.Empty;

                // Solo cargar lotes si ya hay almacén y ubicación seleccionados
                if (!string.IsNullOrWhiteSpace(CodigoAlmacen) && !string.IsNullOrWhiteSpace(Ubicacion))
                {
                    await CargarLotesActivosAsync();
                }
                else
                {
                    MensajeEstado = "Artículo encontrado. Seleccione almacén y ubicación para ver los lotes disponibles.";
                }
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al buscar artículo: {ex.Message}"));
            }
            finally
            {
                IsCargando = false;
            }
        }

        private async Task FiltrarAlmacenesPorStockAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                    return;

                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new System.Collections.Generic.List<string>();

                // Obtener almacenes autorizados
                var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin, SessionManager.Operario);

                // Obtener stock disponible del artículo específico
                var stockDisponible = await _stockService.ObtenerStockDisponibleAsync(codigoArticulo: CodigoArticuloOrigen, descripcion: null);
                
                // Extraer almacenes que tienen stock de este artículo
                var almacenesConStock = stockDisponible
                    .Where(s => s.Disponible > 0 && !string.IsNullOrWhiteSpace(s.CodigoAlmacen))
                    .Select(s => s.CodigoAlmacen!)
                    .Distinct()
                    .ToList();

                // Filtrar almacenes autorizados para mostrar solo los que tienen stock de este artículo
                var almacenesFiltrados = almacenesAutorizados
                    .Where(a => almacenesConStock.Contains(a.CodigoAlmacen))
                    .OrderBy(a => a.NombreAlmacen)
                    .ToList();

                // Si no hay almacenes con stock, mostrar todos los autorizados como fallback
                if (!almacenesFiltrados.Any())
                {
                    almacenesFiltrados = almacenesAutorizados
                        .OrderBy(a => a.NombreAlmacen)
                        .ToList();
                }

                // Actualizar la lista de almacenes
                var almacenSeleccionadoAnterior = AlmacenSeleccionado?.CodigoAlmacen;
                Almacenes.Clear();
                foreach (var almacen in almacenesFiltrados)
                {
                    Almacenes.Add(almacen);
                }

                // Restaurar selección si el almacén anterior sigue disponible
                if (!string.IsNullOrWhiteSpace(almacenSeleccionadoAnterior))
                {
                    AlmacenSeleccionado = Almacenes.FirstOrDefault(a => a.CodigoAlmacen == almacenSeleccionadoAnterior);
                }

                // Si no hay selección y solo hay un almacén, seleccionarlo
                if (AlmacenSeleccionado == null && Almacenes.Count == 1)
                {
                    AlmacenSeleccionado = Almacenes.First();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al filtrar almacenes por stock: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task BuscarArticuloDestinoAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CodigoArticuloDestino))
                {
                    ShowDialog(new WarningDialog("Buscar artículo", "Introduce un código de artículo para buscar."));
                    return;
                }

                IsCargando = true;
                MensajeEstado = "Buscando artículo destino...";

                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var codigoArticuloBuscado = CodigoArticuloDestino.Trim();

                var articulo = await _stockService.BuscarArticuloPorCodigoAsync(empresa, codigoArticuloBuscado);

                if (articulo == null)
                {
                    MensajeEstado = "Artículo destino no encontrado";
                    DescripcionArticuloDestino = string.Empty;
                    return;
                }

                DescripcionArticuloDestino = articulo.DescripcionArticulo ?? string.Empty;
                MensajeEstado = string.Empty;
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al buscar artículo destino: {ex.Message}"));
            }
            finally
            {
                IsCargando = false;
            }
        }

        private async Task CargarLotesActivosAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                    return;

                // Solo cargar lotes si hay almacén seleccionado
                // La ubicación puede ser una cadena vacía para "Sin ubicar", así que solo validamos que no sea null
                if (string.IsNullOrWhiteSpace(CodigoAlmacen) || Ubicacion == null)
                {
                    PartidasDisponibles.Clear();
                    PartidaSeleccionada = null;
                    _fechasPorPartida.Clear();
                    FechaCaducidadOrigen = null;
                    FechasDisponibles.Clear();
                    Partida = string.Empty;
                    return;
                }

                PartidasDisponibles.Clear();
                PartidaSeleccionada = null;
                _fechasPorPartida.Clear();
                FechaCaducidadOrigen = null;
                FechasDisponibles.Clear();

                // Primero obtener el stock de la ubicación específica para filtrar las partidas
                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var stock = await _stockService.ObtenerPorArticuloAsync(
                    empresa,
                    CodigoArticuloOrigen,
                    codigoAlmacen: CodigoAlmacen,
                    codigoUbicacion: Ubicacion);

                // Extraer partidas únicas que tienen stock en esta ubicación
                var partidasConStock = stock
                    .Where(s => s.UnidadSaldo > 0 && !string.IsNullOrWhiteSpace(s.Partida))
                    .Select(s => s.Partida)
                    .Distinct()
                    .ToList();

                if (!partidasConStock.Any())
                {
                    MensajeEstado = "No hay lotes con stock en la ubicación seleccionada.";
                    return;
                }

                // Ahora obtener los lotes activos del artículo
                var lotes = await _stockService.ObtenerLotesActivosAsync(
                    (short)SessionManager.EmpresaSeleccionada!.Value,
                    CodigoArticuloOrigen,
                    incluirHistoricos: true);

                if (lotes == null || !lotes.Any())
                {
                    MensajeEstado = "No hay lotes activos disponibles.";
                    return;
                }

                // Filtrar lotes para mostrar solo los que tienen stock en esta ubicación
                var lotesAgrupados = lotes
                    .Where(l => !string.IsNullOrWhiteSpace(l.Partida))
                    .Where(l => partidasConStock.Contains(l.Partida)) // Solo lotes con stock en esta ubicación
                    .GroupBy(l => l.Partida)
                    .ToList();

                foreach (var grupo in lotesAgrupados)
                {
                    var partida = grupo.Key;
                    if (string.IsNullOrWhiteSpace(partida))
                        continue;

                    var fechas = grupo.Where(l => l.FechaCaducidad.HasValue)
                                     .Select(l => l.FechaCaducidad.Value)
                                     .Distinct()
                                     .OrderBy(f => f)
                                     .Cast<DateTime?>()
                                     .ToList();

                    // Agregar la primera entrada del grupo a PartidasDisponibles (para mostrar en el ComboBox)
                    PartidasDisponibles.Add(new LoteDto
                    {
                        Partida = partida,
                        FechaCaducidad = fechas.FirstOrDefault()
                    });

                    // Guardar todas las fechas asociadas a cada partida
                    _fechasPorPartida[partida] = fechas;
                }

                if (PartidasDisponibles.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(CodigoAlmacen) && !string.IsNullOrWhiteSpace(Ubicacion))
                    {
                        MensajeEstado = "No hay lotes con stock en la ubicación seleccionada.";
                    }
                    else
                    {
                        MensajeEstado = "Artículo encontrado. No hay lotes activos disponibles.";
                    }
                }
                else
                {
                    MensajeEstado = $"Artículo encontrado. {PartidasDisponibles.Count} lote(s) disponible(s).";
                    
                    // Si hay una sola partida, seleccionarla automáticamente
                    if (PartidasDisponibles.Count == 1)
                    {
                        PartidaSeleccionada = PartidasDisponibles.First();
                        // Asegurar que se copie el lote si estamos en modo ampliación
                        if (EsCambioFecha && PartidaSeleccionada != null && !string.IsNullOrWhiteSpace(PartidaSeleccionada.Partida))
                        {
                            _partidaOriginal = PartidaSeleccionada.Partida;
                            PartidaDestino = PartidaSeleccionada.Partida;
                        }
                    }
                }

                // Actualizar stock disponible (la ubicación puede ser cadena vacía para "Sin ubicar")
                if (Ubicacion != null)
                {
                    await CargarStockDisponibleAsync();
                }
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error al cargar lotes: {ex.Message}";
            }
        }

        private async Task CargarStockDisponibleAsync()
        {
            try
            {
                // La ubicación puede ser cadena vacía para "Sin ubicar", así que solo validamos que no sea null
                if (string.IsNullOrWhiteSpace(CodigoArticuloOrigen) || string.IsNullOrWhiteSpace(CodigoAlmacen) || Ubicacion == null)
                    return;

                var empresa = SessionManager.EmpresaSeleccionada ?? 1;

                var stock = await _stockService.ObtenerPorArticuloAsync(
                    empresa,
                    CodigoArticuloOrigen,
                    partida: Partida,
                    codigoAlmacen: CodigoAlmacen,
                    codigoUbicacion: Ubicacion);

                // Filtrar por fecha de caducidad si está seleccionada
                var stockFiltrado = stock;
                if (FechaCaducidadOrigen.HasValue)
                {
                    stockFiltrado = stock.Where(s => s.FechaCaducidad.HasValue && s.FechaCaducidad.Value.Date == FechaCaducidadOrigen.Value.Date).ToList();
                }

                StockDisponible = stockFiltrado.Sum(s => s.UnidadSaldo);
                
                // Obtener PaletId desde CodigoPalet si existe
                var stockConPalet = stockFiltrado.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.CodigoPalet));
                if (stockConPalet != null && !string.IsNullOrWhiteSpace(stockConPalet.CodigoPalet))
                {
                    try
                    {
                        var palets = await _paletService.ObtenerPaletsAsync(
                            codigoEmpresa: (short)empresa,
                            codigo: stockConPalet.CodigoPalet,
                            limite: 1);
                        
                        var palet = palets.FirstOrDefault();
                        if (palet != null)
                        {
                            PaletId = palet.Id;
                        }
                        else
                        {
                            PaletId = null; // Si no se encuentra el palet, dejar null
                        }
                    }
                    catch
                    {
                        PaletId = null; // En caso de error, dejar null
                    }
                }
                else
                {
                    PaletId = null; // Si no hay CodigoPalet, dejar null
                }
            }
            catch (Exception ex)
            {
                StockDisponible = 0;
                PaletId = null;
            }
        }

        [RelayCommand]
        private async Task GuardarAsync()
        {
            try
            {
                // Validaciones
                if (string.IsNullOrWhiteSpace(CodigoArticuloOrigen))
                {
                    ShowDialog(new WarningDialog("Validación", "Debe especificar el código del artículo origen."));
                    return;
                }

                if (string.IsNullOrWhiteSpace(CodigoAlmacen))
                {
                    ShowDialog(new WarningDialog("Validación", "Debe seleccionar un almacén."));
                    return;
                }

                // La ubicación puede ser cadena vacía para "Sin ubicar", así que solo validamos que no sea null
                if (Ubicacion == null)
                {
                    ShowDialog(new WarningDialog("Validación", "Debe seleccionar una ubicación."));
                    return;
                }

                if (Cantidad <= 0)
                {
                    ShowDialog(new WarningDialog("Validación", "La cantidad debe ser mayor que cero."));
                    return;
                }

                if (Cantidad > StockDisponible)
                {
                    ShowDialog(new WarningDialog("Validación", $"La cantidad ({Cantidad:N2}) no puede ser mayor que el stock disponible ({StockDisponible:N2})."));
                    return;
                }

                if (EsCambioCodigo && string.IsNullOrWhiteSpace(CodigoArticuloDestino))
                {
                    ShowDialog(new WarningDialog("Validación", "Debe especificar el código del artículo destino."));
                    return;
                }

                if (EsCambioFecha && !FechaCaducidadDestino.HasValue)
                {
                    ShowDialog(new WarningDialog("Validación", "Debe especificar la nueva fecha de caducidad."));
                    return;
                }

                if (EsCambioFecha && string.IsNullOrWhiteSpace(PartidaDestino))
                {
                    ShowDialog(new WarningDialog("Validación", "Debe especificar la nueva partida (lote)."));
                    return;
                }

                IsCargando = true;
                MensajeEstado = "Procesando cambio...";

                var empresa = SessionManager.EmpresaSeleccionada ?? 1;
                var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;

                var dto = new CambioArticuloDto
                {
                    CodigoEmpresa = (short)empresa,
                    CodigoArticuloOrigen = CodigoArticuloOrigen,
                    CodigoAlmacen = CodigoAlmacen,
                    Ubicacion = Ubicacion,
                    Partida = Partida,
                    FechaCaducidadOrigen = FechaCaducidadOrigen,
                    Cantidad = Cantidad,
                    PaletId = PaletId,
                    CodigoArticuloDestino = EsCambioCodigo ? CodigoArticuloDestino : null,
                    FechaCaducidadDestino = EsCambioFecha ? FechaCaducidadDestino : null,
                    PartidaDestino = (EsCambioCodigo || EsCambioFecha) && !string.IsNullOrWhiteSpace(PartidaDestino) ? PartidaDestino : null,
                    UsuarioId = usuarioId
                };

                var resultado = await _inventarioService.CambiarArticuloAsync(dto);

                if (resultado)
                {
                    ShowDialog(new ConfirmationDialog("Éxito", "El cambio de artículo se ha procesado correctamente. Los ajustes se sincronizarán con el ERP."));
                    
                    // Limpiar formulario
                    CodigoArticuloOrigen = string.Empty;
                    DescripcionArticuloOrigen = string.Empty;
                    CodigoArticuloDestino = string.Empty;
                    DescripcionArticuloDestino = string.Empty;
                    Partida = string.Empty;
                    PartidaDestino = string.Empty;
                    FechaCaducidadOrigen = null;
                    FechaCaducidadDestino = null;
                    Cantidad = 0;
                    StockDisponible = 0;
                    MensajeEstado = string.Empty;
                }
                else
                {
                    ShowDialog(new WarningDialog("Error", "No se pudo procesar el cambio de artículo."));
                }
            }
            catch (Exception ex)
            {
                ShowDialog(new WarningDialog("Error", $"Error al procesar el cambio: {ex.Message}"));
            }
            finally
            {
                IsCargando = false;
            }
        }
        #endregion

        #region Helpers
        private void ShowDialog(Window dialog)
        {
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow;
            if (owner != null && owner != dialog)
                dialog.Owner = owner;
            dialog.ShowDialog();
        }
        #endregion
    }
}

