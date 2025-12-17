using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace SGA_Desktop.ViewModels
{
    public partial class RendimientosViewModel : ObservableObject
    {
        private readonly RendimientosService _rendimientosService;

        // Filtros
        [ObservableProperty]
        private DateTime? fechaDesde;

        [ObservableProperty]
        private DateTime? fechaHasta;

        [ObservableProperty]
        private int? operarioSeleccionadoId;

        [ObservableProperty]
        private string? tipoProcesoSeleccionado;

        [ObservableProperty]
        private bool estaCargando = false;

        // Datos
        public ObservableCollection<RendimientoOperarioDto> Operarios { get; } = new();
        
        private List<RendimientoOperarioDto> _operariosOriginales = new();
        
        // Filtros y ordenación
        [ObservableProperty]
        private string filtroNombre = string.Empty;
        
        [ObservableProperty]
        private string criterioOrdenacion = "TotalOperaciones";
        
        public ObservableCollection<RendimientoProcesoDto> Procesos { get; } = new();
        
        public ObservableCollection<ComparativaRendimientoDto> Comparativas { get; } = new();
        
        public ObservableCollection<TendenciaRendimientoDto> Tendencias { get; } = new();

        // Propiedades para binding seguro
        public ObservableCollection<ItemComparativaDto> ItemsComparativa { get; } = new();
        
        public ObservableCollection<PuntoTendenciaDto> PuntosTendencia { get; } = new();

        [ObservableProperty]
        private VolumenMovidoDto? volumenMovido;

        [ObservableProperty]
        private PlotModel? plotModelEvolucion;

        [ObservableProperty]
        private DistribucionDto? distribucion;

        [ObservableProperty]
        private bool distribucionPorUnidades = true; // true = por unidades, false = por traspasos

        // Control de pestañas
        [ObservableProperty]
        private bool mostrandoOperarios = true;

        [ObservableProperty]
        private bool mostrandoVolumen = false;

        [ObservableProperty]
        private bool mostrandoDistribucion = false;

        [ObservableProperty]
        private bool mostrandoArticulos = false;

        [ObservableProperty]
        private bool mostrandoEficiencia = false;

        [ObservableProperty]
        private bool mostrandoTendencias = false;

        [ObservableProperty]
        private bool mostrandoComparativas = false;

        public RendimientosViewModel()
        {
            _rendimientosService = new RendimientosService();
            
            // Inicializar fechas por defecto (últimos 7 días)
            FechaDesde = DateTime.Today.AddDays(-7);
            FechaHasta = DateTime.Today;

            // Cargar datos iniciales
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private async Task CargarDatosAsync()
        {
            try
            {
                EstaCargando = true;

                var filtros = CrearFiltros();

                // Cargar según la pestaña seleccionada
                if (MostrandoOperarios)
                {
                    await CargarOperariosAsync(filtros);
                }
                else if (MostrandoVolumen)
                {
                    await CargarVolumenAsync(filtros);
                }
                else if (MostrandoDistribucion)
                {
                    await CargarDistribucionAsync(filtros);
                }
                else if (MostrandoArticulos)
                {
                    // TODO: Implementar carga de artículos
                }
                else if (MostrandoEficiencia)
                {
                    await CargarProcesosAsync(filtros); // Por ahora usar procesos para eficiencia
                }
                else if (MostrandoTendencias)
                {
                    await CargarTendenciasAsync(filtros);
                }
                else if (MostrandoComparativas)
                {
                    await CargarComparativasAsync(filtros);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando datos: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        [RelayCommand]
        private async Task AplicarFiltrosAsync()
        {
            await CargarDatosAsync();
        }

        [RelayCommand]
        private void LimpiarFiltros()
        {
            FechaDesde = DateTime.Today.AddDays(-7);
            FechaHasta = DateTime.Today;
            OperarioSeleccionadoId = null;
            TipoProcesoSeleccionado = null;
        }

        // Comandos para cambiar de pestaña
        [RelayCommand]
        private void CambiarAOperarios()
        {
            MostrandoOperarios = true;
            MostrandoVolumen = false;
            MostrandoDistribucion = false;
            MostrandoArticulos = false;
            MostrandoEficiencia = false;
            MostrandoTendencias = false;
            MostrandoComparativas = false;
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private void CambiarAVolumen()
        {
            MostrandoOperarios = false;
            MostrandoVolumen = true;
            MostrandoDistribucion = false;
            MostrandoArticulos = false;
            MostrandoEficiencia = false;
            MostrandoTendencias = false;
            MostrandoComparativas = false;
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private void CambiarADistribucion()
        {
            MostrandoOperarios = false;
            MostrandoVolumen = false;
            MostrandoDistribucion = true;
            MostrandoArticulos = false;
            MostrandoEficiencia = false;
            MostrandoTendencias = false;
            MostrandoComparativas = false;
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private void CambiarAArticulos()
        {
            MostrandoOperarios = false;
            MostrandoVolumen = false;
            MostrandoDistribucion = false;
            MostrandoArticulos = true;
            MostrandoEficiencia = false;
            MostrandoTendencias = false;
            MostrandoComparativas = false;
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private void CambiarAEficiencia()
        {
            MostrandoOperarios = false;
            MostrandoVolumen = false;
            MostrandoDistribucion = false;
            MostrandoArticulos = false;
            MostrandoEficiencia = true;
            MostrandoTendencias = false;
            MostrandoComparativas = false;
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private void CambiarATendencias()
        {
            MostrandoOperarios = false;
            MostrandoVolumen = false;
            MostrandoDistribucion = false;
            MostrandoArticulos = false;
            MostrandoEficiencia = false;
            MostrandoTendencias = true;
            MostrandoComparativas = false;
            _ = CargarDatosAsync();
        }

        [RelayCommand]
        private void CambiarAComparativas()
        {
            MostrandoOperarios = false;
            MostrandoVolumen = false;
            MostrandoDistribucion = false;
            MostrandoArticulos = false;
            MostrandoEficiencia = false;
            MostrandoTendencias = false;
            MostrandoComparativas = true;
            _ = CargarDatosAsync();
        }

        private async Task CargarOperariosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var datos = await _rendimientosService.ObtenerRendimientoOperariosAsync(filtros);
                _operariosOriginales = datos;
                AplicarFiltrosYOrdenacion();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando operarios: {ex.Message}");
            }
        }
        
        private void AplicarFiltrosYOrdenacion()
        {
            try
            {
                var filtrados = _operariosOriginales.AsEnumerable();
                
                // Aplicar filtro por nombre
                if (!string.IsNullOrWhiteSpace(FiltroNombre))
                {
                    filtrados = filtrados.Where(o => 
                        o.NombreOperario?.Contains(FiltroNombre, StringComparison.OrdinalIgnoreCase) == true);
                }
                
                // Aplicar ordenación
                filtrados = CriterioOrdenacion switch
                {
                    "TotalOperaciones" => filtrados.OrderByDescending(o => o.TotalOperaciones),
                    "TraspasosCompletados" => filtrados.OrderByDescending(o => o.TraspasosCompletados),
                    "TiempoPromedio" => filtrados.OrderBy(o => o.TiempoPromedioTraspasosMinutos ?? double.MaxValue),
                    "TraspasosPorDia" => filtrados.OrderByDescending(o => o.TraspasosPorDia ?? 0),
                    "Ranking" => filtrados.OrderBy(o => o.Ranking),
                    "Nombre" => filtrados.OrderBy(o => o.NombreOperario ?? ""),
                    _ => filtrados.OrderByDescending(o => o.TotalOperaciones)
                };
                
                Operarios.Clear();
                foreach (var item in filtrados)
                {
                    Operarios.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando filtros: {ex.Message}");
            }
        }

        private async Task CargarProcesosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var datos = await _rendimientosService.ObtenerRendimientoProcesosAsync(filtros);
                Procesos.Clear();
                foreach (var item in datos)
                {
                    Procesos.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando procesos: {ex.Message}");
            }
        }

        private async Task CargarComparativasAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var datos = await _rendimientosService.ObtenerComparativaAsync(filtros, "OPERARIOS");
                Comparativas.Clear();
                Comparativas.Add(datos);
                
                // Actualizar la colección para binding
                ItemsComparativa.Clear();
                if (datos?.Items != null)
                {
                    foreach (var item in datos.Items)
                    {
                        ItemsComparativa.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando comparativas: {ex.Message}");
                ItemsComparativa.Clear();
            }
        }

        private async Task CargarTendenciasAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var datos = await _rendimientosService.ObtenerTendenciasAsync(filtros, "PRODUCTIVIDAD");
                Tendencias.Clear();
                PuntosTendencia.Clear();
                
                foreach (var item in datos)
                {
                    Tendencias.Add(item);
                    // Agregar los puntos de la primera tendencia (o todas si hay múltiples)
                    if (item?.Puntos != null)
                    {
                        foreach (var punto in item.Puntos)
                        {
                            PuntosTendencia.Add(punto);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando tendencias: {ex.Message}");
                PuntosTendencia.Clear();
            }
        }

        private async Task CargarVolumenAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var datos = await _rendimientosService.ObtenerVolumenMovidoAsync(filtros);
                VolumenMovido = datos;
                ActualizarGraficoEvolucion();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando volumen: {ex.Message}");
                VolumenMovido = null;
                PlotModelEvolucion = null;
            }
        }

        private async Task CargarDistribucionAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var datos = await _rendimientosService.ObtenerDistribucionAsync(filtros);
                Distribucion = datos;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando distribución: {ex.Message}");
                Distribucion = null;
            }
        }

        private void ActualizarGraficoEvolucion()
        {
            if (VolumenMovido?.EvolucionTemporal == null || !VolumenMovido.EvolucionTemporal.Any())
            {
                PlotModelEvolucion = null;
                return;
            }

            var plotModel = new PlotModel
            {
                Title = "Evolución Temporal",
                TitleFontSize = 14,
                PlotAreaBorderThickness = new OxyThickness(1, 0, 0, 1),
                IsLegendVisible = true
            };

            // Eje X - Fechas
            var fechaAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Fecha",
                StringFormat = "dd/MM",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineColor = OxyColors.LightGray,
                MinorGridlineThickness = 0.5,
                MajorGridlineThickness = 1
            };
            plotModel.Axes.Add(fechaAxis);

            // Eje Y - Unidades (compartido para ambas series)
            var unidadesAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Unidades",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.LightGray,
                MinorGridlineColor = OxyColors.LightGray,
                MinorGridlineThickness = 0.5,
                MajorGridlineThickness = 1,
                StringFormat = "N0"
            };
            plotModel.Axes.Add(unidadesAxis);

            // Serie de Unidades en Traspasos de Palet
            var serieUnidadesPalet = new LineSeries
            {
                Title = "Unidades - Traspasos Palet",
                MarkerType = MarkerType.Circle,
                MarkerSize = 5,
                MarkerStroke = OxyColors.Blue,
                MarkerFill = OxyColors.Blue,
                Color = OxyColors.Blue,
                StrokeThickness = 2.5
            };

            // Serie de Unidades en Traspasos de Artículo
            var serieUnidadesArticulo = new LineSeries
            {
                Title = "Unidades - Traspasos Artículo",
                MarkerType = MarkerType.Square,
                MarkerSize = 5,
                MarkerStroke = OxyColors.Orange,
                MarkerFill = OxyColors.Orange,
                Color = OxyColors.Orange,
                StrokeThickness = 2.5
            };

            // Agregar puntos
            foreach (var punto in VolumenMovido.EvolucionTemporal.OrderBy(p => p.Fecha))
            {
                var fechaOxy = DateTimeAxis.ToDouble(punto.Fecha);
                serieUnidadesPalet.Points.Add(new DataPoint(fechaOxy, (double)punto.UnidadesPalet));
                serieUnidadesArticulo.Points.Add(new DataPoint(fechaOxy, (double)punto.UnidadesArticulo));
            }

            plotModel.Series.Add(serieUnidadesPalet);
            plotModel.Series.Add(serieUnidadesArticulo);

            PlotModelEvolucion = plotModel;
        }

        private FiltroRendimientosDto CrearFiltros()
        {
            return new FiltroRendimientosDto
            {
                FechaDesde = FechaDesde,
                FechaHasta = FechaHasta,
                OperarioId = OperarioSeleccionadoId,
                TipoProceso = TipoProcesoSeleccionado,
                CodigoEmpresa = SessionManager.EmpresaSeleccionada
            };
        }
        
        partial void OnFiltroNombreChanged(string value)
        {
            if (MostrandoOperarios)
            {
                AplicarFiltrosYOrdenacion();
            }
        }
        
        partial void OnCriterioOrdenacionChanged(string value)
        {
            if (MostrandoOperarios)
            {
                AplicarFiltrosYOrdenacion();
            }
        }

        partial void OnVolumenMovidoChanged(VolumenMovidoDto? value)
        {
            ActualizarGraficoEvolucion();
        }

        partial void OnDistribucionChanged(DistribucionDto? value)
        {
            OnPropertyChanged(nameof(AlmacenesOrigenOrdenados));
            OnPropertyChanged(nameof(AlmacenesDestinoOrdenados));
            OnPropertyChanged(nameof(UbicacionesOrigenOrdenadas));
            OnPropertyChanged(nameof(UbicacionesDestinoOrdenadas));
            OnPropertyChanged(nameof(FlujosOrdenados));
        }

        partial void OnDistribucionPorUnidadesChanged(bool value)
        {
            OnPropertyChanged(nameof(AlmacenesOrigenOrdenados));
            OnPropertyChanged(nameof(AlmacenesDestinoOrdenados));
            OnPropertyChanged(nameof(UbicacionesOrigenOrdenadas));
            OnPropertyChanged(nameof(UbicacionesDestinoOrdenadas));
            OnPropertyChanged(nameof(FlujosOrdenados));
        }

        [RelayCommand]
        private void CambiarVistaDistribucion(string tipo)
        {
            DistribucionPorUnidades = tipo == "Unidades";
        }

        // Propiedades calculadas para ordenar según la vista seleccionada
        public List<AlmacenDistribucionDto> AlmacenesOrigenOrdenados
        {
            get
            {
                if (Distribucion?.TopAlmacenesOrigen == null) return new List<AlmacenDistribucionDto>();
                var ordenados = DistribucionPorUnidades
                    ? Distribucion.TopAlmacenesOrigen.OrderByDescending(a => a.UnidadesMovidas).ToList()
                    : Distribucion.TopAlmacenesOrigen.OrderByDescending(a => a.CantidadTraspasos).ToList();
                return ordenados.Select((a, index) => { a.Posicion = index + 1; return a; }).ToList();
            }
        }

        public List<AlmacenDistribucionDto> AlmacenesDestinoOrdenados
        {
            get
            {
                if (Distribucion?.TopAlmacenesDestino == null) return new List<AlmacenDistribucionDto>();
                var ordenados = DistribucionPorUnidades
                    ? Distribucion.TopAlmacenesDestino.OrderByDescending(a => a.UnidadesMovidas).ToList()
                    : Distribucion.TopAlmacenesDestino.OrderByDescending(a => a.CantidadTraspasos).ToList();
                return ordenados.Select((a, index) => { a.Posicion = index + 1; return a; }).ToList();
            }
        }

        public List<UbicacionDistribucionDto> UbicacionesOrigenOrdenadas
        {
            get
            {
                if (Distribucion?.TopUbicacionesOrigen == null) return new List<UbicacionDistribucionDto>();
                var ordenadas = DistribucionPorUnidades
                    ? Distribucion.TopUbicacionesOrigen.OrderByDescending(u => u.UnidadesMovidas).ToList()
                    : Distribucion.TopUbicacionesOrigen.OrderByDescending(u => u.CantidadTraspasos).ToList();
                return ordenadas.Select((u, index) => { u.Posicion = index + 1; return u; }).ToList();
            }
        }

        public List<UbicacionDistribucionDto> UbicacionesDestinoOrdenadas
        {
            get
            {
                if (Distribucion?.TopUbicacionesDestino == null) return new List<UbicacionDistribucionDto>();
                var ordenadas = DistribucionPorUnidades
                    ? Distribucion.TopUbicacionesDestino.OrderByDescending(u => u.UnidadesMovidas).ToList()
                    : Distribucion.TopUbicacionesDestino.OrderByDescending(u => u.CantidadTraspasos).ToList();
                return ordenadas.Select((u, index) => { u.Posicion = index + 1; return u; }).ToList();
            }
        }

        public List<FlujoDistribucionDto> FlujosOrdenados
        {
            get
            {
                if (Distribucion?.FlujosPrincipales == null) return new List<FlujoDistribucionDto>();
                var ordenados = DistribucionPorUnidades
                    ? Distribucion.FlujosPrincipales.OrderByDescending(f => f.UnidadesMovidas).ToList()
                    : Distribucion.FlujosPrincipales.OrderByDescending(f => f.CantidadTraspasos).ToList();
                return ordenados.Select((f, index) => { f.Posicion = index + 1; return f; }).ToList();
            }
        }
    }
}

