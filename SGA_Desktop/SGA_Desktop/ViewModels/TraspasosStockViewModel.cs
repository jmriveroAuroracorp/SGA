using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SGA_Desktop.Dialog;
using System.Windows.Data;
using System.ComponentModel;

namespace SGA_Desktop.ViewModels
{
    public partial class ArticuloStockGroup : ObservableObject
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public ObservableCollection<StockDisponibleDto> Ubicaciones { get; set; } = new();
        public string HeaderArticulo => $"{CodigoArticulo} - {DescripcionArticulo}";
        
        [ObservableProperty]
        private bool isExpanded = false;
    }

    public partial class TraspasosStockViewModel : ObservableObject
    {
        private readonly StockService _stockService;
        private readonly TraspasosService _traspasosService;
        private DateTime? _fechaUltimaBusqueda;
        private Dictionary<string, bool> _estadosExpansion = new();
        
        // 🔷 NUEVO: Almacenar todos los resultados de stock para filtrado local
        private List<StockDisponibleDto> _todosLosResultadosStock = new();

        public TraspasosStockViewModel(StockService stockService, TraspasosService traspasosService)
        {
            _stockService = stockService;
            _traspasosService = traspasosService;
            ArticulosConUbicaciones = new ObservableCollection<ArticuloStockGroup>();
            UltimosTraspasos = new ObservableCollection<TraspasoArticuloDto>();
            AlmacenesDestino = new ObservableCollection<string>();
            UbicacionesDestino = new ObservableCollection<string>();
            
            // Inicializar la vista filtrable de almacenes
            AlmacenesFiltroView = CollectionViewSource.GetDefaultView(AlmacenesFiltro);
            AlmacenesFiltroView.Filter = FiltraAlmacenesFiltro;
            
            // NO cargar almacenes aquí - se cargarán cuando se busque un artículo
        }

        // Buscador de artículo
        [ObservableProperty]
        private string articuloBuscado;

        // Combo de almacenes para filtrar
        public ObservableCollection<AlmacenDto> AlmacenesFiltro { get; } = new();
        public ICollectionView AlmacenesFiltroView { get; private set; }
        
        [ObservableProperty]
        private AlmacenDto almacenFiltroSeleccionado;
        
        [ObservableProperty]
        private string filtroAlmacenesTexto = "";

        // 🔷 NUEVO: Propiedad para controlar la visibilidad del combo de almacenes
        [ObservableProperty]
        private bool mostrarComboAlmacenes = false;

        // Siempre usaremos los cards agrupados
        public ObservableCollection<ArticuloStockGroup> ArticulosConUbicaciones { get; } = new();
        [ObservableProperty]
        private StockDisponibleDto? stockSeleccionado;

        // Formulario de traspaso
        [ObservableProperty]
        private string? almacenDestino;
        [ObservableProperty]
        private string? ubicacionDestino;
        public ObservableCollection<string> AlmacenesDestino { get; }
        public ObservableCollection<string> UbicacionesDestino { get; }
        [ObservableProperty]
        private decimal cantidadMover;

        // Feedback y últimos traspasos
        [ObservableProperty]
        private string feedback;
        public ObservableCollection<TraspasoArticuloDto> UltimosTraspasos { get; }

        [ObservableProperty]
        private bool mostrarCardsAgrupados;

        [RelayCommand]
        public async Task RefrescarAsync()
        {
            if (!string.IsNullOrWhiteSpace(ArticuloBuscado))
            {
                // Guardar el estado de expansión actual antes de refrescar
                GuardarEstadosExpansion();
                await BuscarStockAsync();
                // Pequeño delay para asegurar que la UI se actualice
                await Task.Delay(50);
                // Restaurar el estado de expansión después de refrescar
                RestaurarEstadosExpansion();
            }
        }

        [RelayCommand]
        public async Task BuscarStockAsync()
        {
            _fechaUltimaBusqueda = DateTime.Now;
            ArticulosConUbicaciones.Clear();
            
            // 🔷 NUEVO: Limpiar combo de almacenes cuando no hay artículo
            if (string.IsNullOrWhiteSpace(ArticuloBuscado))
            {
                AlmacenesFiltro.Clear();
                AlmacenFiltroSeleccionado = null;
                _todosLosResultadosStock.Clear();
                MostrarComboAlmacenes = false;
                Feedback = "Introduce un código o descripción de artículo.";
                return;
            }
            
            try
            {
                // Nuevo: buscar stock disponible con Reservado y Disponible
                var stock = await _stockService.ObtenerStockDisponibleAsync(ArticuloBuscado, null);

                // Si no hay resultados, buscar por descripción
                if (stock == null || stock.Count == 0)
                {
                    stock = await _stockService.ObtenerStockDisponibleAsync(null, ArticuloBuscado);
                }

                if (stock.Count == 0)
                {
                    // 🔷 NUEVO: Limpiar combo cuando no hay stock
                    AlmacenesFiltro.Clear();
                    AlmacenFiltroSeleccionado = null;
                    _todosLosResultadosStock.Clear();
                    MostrarComboAlmacenes = false;
                    Feedback = "No hay stock para ese artículo.";
                    return;
                }

                // 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
                var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
                
                // Filtrar por almacenes autorizados
                stock = stock.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();

                // 🔷 NUEVO: Guardar todos los resultados para filtrado local
                _todosLosResultadosStock = new List<StockDisponibleDto>(stock);

                // 🔷 NUEVO: Cargar combo con los almacenes que realmente tienen stock del artículo
                await CargarAlmacenesConStockAsync(stock);

                // 🔷 NUEVO: Aplicar filtrado por almacén si hay uno seleccionado
                FiltrarResultadosPorAlmacen();
                
                Feedback = string.Empty;
            }
            catch (Exception ex)
            {
                Feedback = $"Error al buscar stock: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SeleccionarStock(StockDisponibleDto? seleccionado)
        {
            StockSeleccionado = seleccionado;
        }

        [RelayCommand]
        public async Task ConfirmarTraspasoAsync()
        {
            Feedback = string.Empty;
            if (StockSeleccionado == null)
            {
                Feedback = "Selecciona una línea de stock de origen.";
                return;
            }
            if (string.IsNullOrWhiteSpace(AlmacenDestino) || string.IsNullOrWhiteSpace(UbicacionDestino))
            {
                Feedback = "Selecciona almacén y ubicación destino.";
                return;
            }
            if (CantidadMover <= 0 || CantidadMover > StockSeleccionado.Disponible)
            {
                Feedback = $"Cantidad a mover no válida. Disponible real: {StockSeleccionado.Disponible}";
                return;
            }

            // 🔷 OPTIMIZADO: Solo validar si el artículo está bloqueado por calidad
            var validacion = await ValidarTraspasoAsync();
            if (!validacion)
            {
                return; // La validación ya mostró el mensaje de error
            }

            var resultado = await _traspasosService.CrearTraspasoArticuloAsync(new CrearTraspasoArticuloDto
            {
                AlmacenOrigen = StockSeleccionado.CodigoAlmacen,
                UbicacionOrigen = StockSeleccionado.Ubicacion,
                CodigoArticulo = StockSeleccionado.CodigoArticulo,
                Cantidad = CantidadMover,
                UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
                AlmacenDestino = AlmacenDestino,
                UbicacionDestino = UbicacionDestino,
                Finalizar = true
            });
            if (resultado.Success)
            {
                Feedback = "Traspaso realizado correctamente.";
                // Guardar el estado de expansión antes de refrescar
                GuardarEstadosExpansion();
                await BuscarStockAsync();
                await CargarUltimosTraspasosAsync();
                // Pequeño delay para asegurar que la UI se actualice
                await Task.Delay(50);
                // Restaurar el estado de expansión después de refrescar
                RestaurarEstadosExpansion();
            }
            else
            {
                Feedback = resultado.ErrorMessage ?? "Error al realizar el traspaso.";
            }
        }

        [RelayCommand]
        public async Task CargarUltimosTraspasosAsync()
        {
            UltimosTraspasos.Clear();
            var lista = await _traspasosService.GetUltimosTraspasosArticulosAsync();
            foreach (var t in lista)
                UltimosTraspasos.Add(t);
        }

        [RelayCommand]
        public async void AbrirDialogoTraspaso()
        {
            if (StockSeleccionado == null)
                return;

            // 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
            var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
            
            var almacenesDto = await _stockService.ObtenerAlmacenesAutorizadosAsync(
                SessionManager.EmpresaSeleccionada!.Value, 
                SessionManager.UsuarioActual?.codigoCentro ?? "0", 
                almacenesAutorizados
            );
            
            var vm = new TraspasoStockDialogViewModel(StockSeleccionado, _traspasosService, _fechaUltimaBusqueda)
            {
                AlmacenesDestino = new ObservableCollection<AlmacenDto>(almacenesDto)
            };
            var dlg = new TraspasoStockDialog(vm);
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
            if (owner != null && owner != dlg)
                dlg.Owner = owner;
            // Suscribirse al cierre para refrescar si fue correcto
            vm.RequestClose += async (ok) =>
            {
                dlg.DialogResult = ok;
                dlg.Close();
                if (ok)
                {
                    // Guardar el estado de expansión antes de refrescar
                    GuardarEstadosExpansion();
                    await BuscarStockAsync();
                    await CargarUltimosTraspasosAsync();
                    // Pequeño delay para asegurar que la UI se actualice
                    await Task.Delay(50);
                    // Restaurar el estado de expansión después de refrescar
                    RestaurarEstadosExpansion();
                }
            };
            dlg.ShowDialog();
        }

		[RelayCommand]
		public async Task AbrirDialogoRegularizacionMultipleAsync()
		{
			var vm = new RegularizacionMultipleDialogViewModel(_traspasosService, _stockService);
			await vm.InitializeAsync(); // <- Espera a que cargue datos antes de abrir la ventana

			var dlg = new SGA_Desktop.Dialog.RegularizacionMultipleDialog(vm);
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dlg)
				dlg.Owner = owner;
			dlg.ShowDialog();
		}

		[RelayCommand]
		public async Task VerHistorialAsync()
		{
			var vm = new TraspasoHistoricoDialogViewModel(_traspasosService);
			var dlg = new TraspasoHistoricoDialog(vm);
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dlg)
				dlg.Owner = owner;
			dlg.ShowDialog();
		}

        //  NUEVA FUNCIÓN: Obtener todos los almacenes autorizados (individuales + centro)
        private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync()
        {
            var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
            var centroLogistico = SessionManager.UsuarioActual?.codigoCentro ?? "0";

            // Si el usuario tiene almacenes individuales, incluir también los del centro
            if (almacenesIndividuales.Any())
            {
                // Obtener almacenes del centro logístico de forma asíncrona
                var almacenesCentro = await _stockService.ObtenerAlmacenesAsync(centroLogistico);
                
                // Combinar almacenes individuales + almacenes del centro
                var todosLosAlmacenes = new List<string>(almacenesIndividuales);
                todosLosAlmacenes.AddRange(almacenesCentro);
                
                // Eliminar duplicados
                return todosLosAlmacenes.Distinct().ToList();
            }
            else
            {
                // Si no tiene almacenes individuales, usar solo los del centro
                return await _stockService.ObtenerAlmacenesAsync(centroLogistico);
            }
        }

        private void GuardarEstadosExpansion()
        {
            _estadosExpansion.Clear();
            foreach (var grupo in ArticulosConUbicaciones)
            {
                var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
                _estadosExpansion[clave] = grupo.IsExpanded;
            }
        }

        private void RestaurarEstadosExpansion()
        {
            foreach (var grupo in ArticulosConUbicaciones)
            {
                var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
                if (_estadosExpansion.ContainsKey(clave))
                {
                    grupo.IsExpanded = _estadosExpansion[clave];
                }
            }
            
            // Forzar la actualización de la UI
            OnPropertyChanged(nameof(ArticulosConUbicaciones));
        }

        // 🔷 NUEVO: Método para cargar almacenes basándose en el stock encontrado
        private async Task CargarAlmacenesConStockAsync(List<StockDisponibleDto> stock)
        {
            try
            {
                // Obtener códigos únicos de almacenes del stock encontrado
                var codigosAlmacenesStock = stock.Select(x => x.CodigoAlmacen).Distinct().ToList();
                
                if (!codigosAlmacenesStock.Any())
                {
                    AlmacenesFiltro.Clear();
                    MostrarComboAlmacenes = false;
                    return;
                }

                // Obtener información completa de los almacenes
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                
                if (!permisos.Any())
                {
                    permisos = await _stockService.ObtenerAlmacenesAsync(centro);
                }
                
                // Obtener todos los almacenes autorizados
                var todosAlmacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
                
                // Filtrar solo los almacenes que tienen stock del artículo
                var almacenesConStock = todosAlmacenes
                    .Where(a => codigosAlmacenesStock.Contains(a.CodigoAlmacen))
                    .OrderBy(a => a.DescripcionCombo)
                    .ToList();
                
                // Limpiar y poblar el combo
                AlmacenesFiltro.Clear();
                foreach (var almacen in almacenesConStock)
                    AlmacenesFiltro.Add(almacen);
                    
                // Limpiar selección previa si el almacén ya no está disponible
                if (AlmacenFiltroSeleccionado != null && 
                    !almacenesConStock.Any(a => a.CodigoAlmacen == AlmacenFiltroSeleccionado.CodigoAlmacen))
                {
                    AlmacenFiltroSeleccionado = null;
                }
                
                // 🔷 NUEVO: Mostrar combo solo si hay almacenes
                MostrarComboAlmacenes = AlmacenesFiltro.Count > 0;
                    
                OnPropertyChanged(nameof(AlmacenesFiltro));
            }
            catch (Exception ex)
            {
                // En caso de error, continuar sin filtro de almacenes
                AlmacenesFiltro.Clear();
                MostrarComboAlmacenes = false;
            }
        }

        // 🔷 NUEVO: Método para filtrar almacenes en el combo
        private bool FiltraAlmacenesFiltro(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesTexto)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // 🔷 NUEVO: Método para manejar cambios en el filtro de almacenes
        partial void OnFiltroAlmacenesTextoChanged(string value)
        {
            AlmacenesFiltroView?.Refresh();
        }

        // 🔷 NUEVO: Método para filtrar resultados por almacén sin hacer nueva búsqueda
        private void FiltrarResultadosPorAlmacen()
        {
            // Guardar el estado de expansión antes de limpiar
            GuardarEstadosExpansion();
            
            // Limpiar resultados actuales
            ArticulosConUbicaciones.Clear();
            
            // Obtener stock filtrado
            var stockFiltrado = _todosLosResultadosStock;
            
            // Aplicar filtro por almacén si hay uno seleccionado
            if (AlmacenFiltroSeleccionado != null)
            {
                stockFiltrado = stockFiltrado.Where(x => x.CodigoAlmacen == AlmacenFiltroSeleccionado.CodigoAlmacen).ToList();
            }
            
            // Agrupar por artículo
            var grupos = stockFiltrado.GroupBy(x => new { x.CodigoArticulo, x.DescripcionArticulo })
                                      .Select(g => new ArticuloStockGroup
                                      {
                                          CodigoArticulo = g.Key.CodigoArticulo,
                                          DescripcionArticulo = g.Key.DescripcionArticulo,
                                          Ubicaciones = new ObservableCollection<StockDisponibleDto>(
                                              g.OrderBy(x => x.CodigoAlmacen)
                                                .ThenBy(x => x.Ubicacion)
                                                .ToList())
                                      })
                                      .OrderBy(a => a.CodigoArticulo)
                                      .ToList();
            
            // Añadir grupos a la colección
            foreach (var g in grupos)
                ArticulosConUbicaciones.Add(g);
            
            // Restaurar el estado de expansión después de añadir los elementos
            RestaurarEstadosExpansion();
        }

        // 🔷 OPTIMIZADO: Validar traspaso solo cuando sea necesario
        private async Task<bool> ValidarTraspasoAsync()
        {
            try
            {
                if (StockSeleccionado == null || string.IsNullOrWhiteSpace(UbicacionDestino))
                    return true; // No validar si no hay datos suficientes

                // 🔷 OPTIMIZACIÓN: Solo validar si el artículo está bloqueado por calidad
                // Primero verificar localmente si hay bloqueo de calidad
                if (StockSeleccionado.IsBloqueadoCalidad)
                {
                    var request = new ValidacionTraspasoRequest
                    {
                        CodigoArticulo = StockSeleccionado.CodigoArticulo,
                        AlmacenDestino = AlmacenDestino,
                        UbicacionDestino = UbicacionDestino,
                        CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value
                    };

                    var resultado = await _traspasosService.ValidarTraspasoArticuloAsync(request);
                    
                    if (!resultado.EsValido)
                    {
                        Feedback = $"❌ {resultado.MotivoBloqueo}";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validando traspaso: {ex.Message}");
                // En caso de error, permitir traspaso para no bloquear operaciones
                return true;
            }
        }

        // 🔷 NUEVO: Método para manejar cambios en la selección del almacén
        partial void OnAlmacenFiltroSeleccionadoChanged(AlmacenDto value)
        {
            // Actualizar el texto del filtro con la selección
            if (value != null)
            {
                FiltroAlmacenesTexto = value.DescripcionCombo;
            }
            
            // 🔷 CORREGIDO: Solo filtrar los resultados existentes, NO hacer otra búsqueda
            FiltrarResultadosPorAlmacen();
        }

	}
} 