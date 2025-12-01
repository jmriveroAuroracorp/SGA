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
        
        // Total del artículo sumando todas las ubicaciones sin importar lote ni fecha de caducidad
        public decimal TotalArticulo => Ubicaciones?.Sum(x => x.Disponible) ?? 0;
        
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
        
        // 🔷 NUEVO: Propiedades para filtro de partida (como ComboBox)
        public ObservableCollection<string> PartidasDisponibles { get; } = new();
        public ICollectionView PartidasDisponiblesView { get; private set; }
        
        [ObservableProperty]
        private string partidaSeleccionada;
        
        [ObservableProperty]
        private string filtroPartidaTexto = "";
        
        [ObservableProperty]
        private bool mostrarFiltroPartida = false;

        // 🔷 NUEVO: Propiedades para filtro de palets (como ComboBox)
        public ObservableCollection<string> PaletsDisponibles { get; } = new();
        public ICollectionView PaletsDisponiblesView { get; private set; }
        
        [ObservableProperty]
        private string paletSeleccionado;
        
        [ObservableProperty]
        private string filtroPaletsTexto = "";
        
        [ObservableProperty]
        private bool mostrarFiltroPalets = false;

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
            
            // 🔷 NUEVO: Inicializar la vista filtrable de partidas
            PartidasDisponiblesView = CollectionViewSource.GetDefaultView(PartidasDisponibles);
            PartidasDisponiblesView.Filter = FiltraPartidasDisponibles;
            
            // 🔷 NUEVO: Inicializar la vista filtrable de palets
            PaletsDisponiblesView = CollectionViewSource.GetDefaultView(PaletsDisponibles);
            PaletsDisponiblesView.Filter = FiltraPaletsDisponibles;
            
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
                // Esperar a que la UI se actualice completamente
                await Task.Delay(150);
                // Restaurar el estado de expansión después de refrescar en el hilo de la UI
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RestaurarEstadosExpansion();
                });
                // Forzar actualización adicional para asegurar que los expanders se abran
                await Task.Delay(100);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RestaurarEstadosExpansion();
                });
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
                MostrarFiltroPartida = false;
                PartidaSeleccionada = null;
                FiltroPartidaTexto = "";
                MostrarFiltroPalets = false;
                PaletSeleccionado = null;
                FiltroPaletsTexto = "";
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
                    MostrarFiltroPartida = false;
                    PartidaSeleccionada = null;
                    FiltroPartidaTexto = "";
                    MostrarFiltroPalets = false;
                    PaletSeleccionado = null;
                    FiltroPaletsTexto = "";
                    Feedback = "No hay stock para ese artículo.";
                    return;
                }

                // 🔷 NUEVA LÓGICA: Obtener todos los almacenes autorizados (individuales + centro)
                var almacenesAutorizados = await ObtenerAlmacenesAutorizadosAsync();
                
                // Filtrar por almacenes autorizados
                var stockAntesFiltro = stock.Count;
                stock = stock.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();
                
                // Si después de filtrar no hay stock, informar al usuario
                if (stock.Count == 0 && stockAntesFiltro > 0)
                {
                    Feedback = $"No tienes acceso a los almacenes donde hay stock de este artículo. Almacenes autorizados: {(almacenesAutorizados.Any() ? string.Join(", ", almacenesAutorizados) : "NINGUNO")}";
                    AlmacenesFiltro.Clear();
                    AlmacenFiltroSeleccionado = null;
                    _todosLosResultadosStock.Clear();
                    MostrarComboAlmacenes = false;
                    MostrarFiltroPartida = false;
                    PartidaSeleccionada = null;
                    FiltroPartidaTexto = "";
                    MostrarFiltroPalets = false;
                    PaletSeleccionado = null;
                    FiltroPaletsTexto = "";
                    return;
                }

                // 🔷 NUEVO: Guardar todos los resultados para filtrado local
                _todosLosResultadosStock = new List<StockDisponibleDto>(stock);

                // 🔷 NUEVO: Cargar combo con los almacenes que realmente tienen stock del artículo
                await CargarAlmacenesConStockAsync(stock);
                
                // 🔷 NUEVO: Cargar partidas disponibles del stock
                CargarPartidasDisponibles(stock);
                
                // 🔷 NUEVO: Cargar palets disponibles del stock
                CargarPaletsDisponibles(stock);

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
                // Esperar a que la UI se actualice completamente
                await Task.Delay(150);
                // Restaurar el estado de expansión después de refrescar en el hilo de la UI
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RestaurarEstadosExpansion();
                });
                // Forzar actualización adicional para asegurar que los expanders se abran
                await Task.Delay(100);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RestaurarEstadosExpansion();
                });
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
                    // Esperar a que la UI se actualice completamente
                    await Task.Delay(150);
                    // Restaurar el estado de expansión después de refrescar en el hilo de la UI
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        RestaurarEstadosExpansion();
                    });
                    // Forzar actualización adicional para asegurar que los expanders se abran
                    await Task.Delay(100);
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        RestaurarEstadosExpansion();
                    });
                }
            };
            dlg.ShowDialog();
        }

		[RelayCommand]
		public async Task AbrirDialogoRegularizacionMultipleAsync()
		{
			try
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
			catch (Exception ex)
			{
				var errorMsg = ex.Message.Contains("400") || ex.Message.Contains("Bad Request")
					? "Error al cargar los almacenes. Verifica que el usuario tenga almacenes asignados."
					: $"Error al abrir el diálogo: {ex.Message}";
				new WarningDialog("Error", errorMsg).ShowDialog();
			}
		}


        //  NUEVA FUNCIÓN: Obtener todos los almacenes autorizados (individuales + centro)
        private async Task<List<string>> ObtenerAlmacenesAutorizadosAsync()
        {
            try
            {
                var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                var centroLogistico = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var empresa = SessionManager.EmpresaSeleccionada!.Value;

                // Si el centro está vacío o es "0", usar solo los almacenes individuales
                if (string.IsNullOrEmpty(centroLogistico) || centroLogistico == "0")
                {
                    return almacenesIndividuales;
                }

                // Usar el método del servicio que ya combina almacenes individuales + centro
                var almacenesDto = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centroLogistico, almacenesIndividuales);
                return almacenesDto.Select(a => a.CodigoAlmacen).ToList();
            }
            catch (Exception ex)
            {
                // En caso de error, devolver al menos los almacenes individuales
                var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                return almacenesIndividuales;
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
                // Intentar buscar por clave completa primero
                var clave = $"{grupo.CodigoArticulo}_{grupo.DescripcionArticulo}";
                if (_estadosExpansion.ContainsKey(clave))
                {
                    grupo.IsExpanded = _estadosExpansion[clave];
                }
                else
                {
                    // Si no se encuentra, buscar solo por código de artículo
                    var clavePorCodigo = _estadosExpansion.Keys.FirstOrDefault(k => k.StartsWith($"{grupo.CodigoArticulo}_"));
                    if (clavePorCodigo != null && _estadosExpansion.ContainsKey(clavePorCodigo))
                    {
                        grupo.IsExpanded = _estadosExpansion[clavePorCodigo];
                    }
                }
            }
            
            // Forzar la actualización de la UI
            OnPropertyChanged(nameof(ArticulosConUbicaciones));
            
            // Los cambios en IsExpanded se notifican automáticamente gracias a [ObservableProperty]
            // No es necesario llamar manualmente a OnPropertyChanged desde fuera de la clase
        }

        // 🔷 NUEVO: Método para cargar partidas disponibles del stock
        private void CargarPartidasDisponibles(List<StockDisponibleDto> stock)
        {
            try
            {
                // Obtener partidas únicas del stock (excluyendo nulas o vacías)
                var partidasUnicas = stock
                    .Where(x => !string.IsNullOrEmpty(x.Partida))
                    .Select(x => x.Partida)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                
                // Limpiar y poblar la colección
                PartidasDisponibles.Clear();
                foreach (var partida in partidasUnicas)
                    PartidasDisponibles.Add(partida);
                
                // Limpiar selección previa si la partida ya no está disponible
                if (!string.IsNullOrEmpty(PartidaSeleccionada) && 
                    !partidasUnicas.Contains(PartidaSeleccionada))
                {
                    PartidaSeleccionada = null;
                }
                
                // Mostrar filtro solo si hay partidas
                MostrarFiltroPartida = PartidasDisponibles.Count > 0;
                
                OnPropertyChanged(nameof(PartidasDisponibles));
            }
            catch (Exception ex)
            {
                // En caso de error, continuar sin filtro de partidas
                PartidasDisponibles.Clear();
                MostrarFiltroPartida = false;
            }
        }
        
        // 🔷 NUEVO: Método para cargar palets disponibles del stock
        private void CargarPaletsDisponibles(List<StockDisponibleDto> stock)
        {
            try
            {
                var codigosPaletsUnicos = new List<string>();
                
                // 1) Obtener palets de la lista Palets (si existe)
                var paletsDeLista = stock
                    .Where(x => x.Palets != null && x.Palets.Any())
                    .SelectMany(x => x.Palets)
                    .Where(p => !string.IsNullOrEmpty(p.CodigoPalet))
                    .Select(p => p.CodigoPalet)
                    .ToList();
                
                codigosPaletsUnicos.AddRange(paletsDeLista);
                
                // 2) Obtener palets del campo CodigoPalet directo (si no está en la lista)
                var paletsDirectos = stock
                    .Where(x => !string.IsNullOrEmpty(x.CodigoPalet))
                    .Select(x => x.CodigoPalet)
                    .ToList();
                
                codigosPaletsUnicos.AddRange(paletsDirectos);
                
                // Eliminar duplicados y ordenar
                codigosPaletsUnicos = codigosPaletsUnicos
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                
                // Limpiar y poblar la colección
                PaletsDisponibles.Clear();
                foreach (var codigoPalet in codigosPaletsUnicos)
                    PaletsDisponibles.Add(codigoPalet);
                
                // Limpiar selección previa si el palet ya no está disponible
                if (!string.IsNullOrEmpty(PaletSeleccionado) && 
                    !codigosPaletsUnicos.Contains(PaletSeleccionado))
                {
                    PaletSeleccionado = null;
                }
                
                // Mostrar filtro solo si hay palets
                MostrarFiltroPalets = PaletsDisponibles.Count > 0;
                
                OnPropertyChanged(nameof(PaletsDisponibles));
                OnPropertyChanged(nameof(MostrarFiltroPalets));
            }
            catch (Exception ex)
            {
                // En caso de error, continuar sin filtro de palets
                PaletsDisponibles.Clear();
                MostrarFiltroPalets = false;
            }
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
                
                // 🔷 NUEVO: Mostrar filtro de partida si hay stock
                MostrarFiltroPartida = _todosLosResultadosStock.Count > 0;
                    
                OnPropertyChanged(nameof(AlmacenesFiltro));
            }
            catch (Exception ex)
            {
                // En caso de error, continuar sin filtro de almacenes
                AlmacenesFiltro.Clear();
                MostrarComboAlmacenes = false;
                MostrarFiltroPartida = false;
                PartidaSeleccionada = null;
                FiltroPartidaTexto = "";
                MostrarFiltroPalets = false;
                PaletSeleccionado = null;
                FiltroPaletsTexto = "";
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
        
        // 🔷 NUEVO: Método para filtrar partidas en el combo
        private bool FiltraPartidasDisponibles(object obj)
        {
            if (obj is not string partida) return false;
            if (string.IsNullOrEmpty(FiltroPartidaTexto)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(partida, FiltroPartidaTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        // 🔷 NUEVO: Método para filtrar palets en el combo
        private bool FiltraPaletsDisponibles(object obj)
        {
            if (obj is not string codigoPalet) return false;
            if (string.IsNullOrEmpty(FiltroPaletsTexto)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(codigoPalet, FiltroPaletsTexto, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }

        // 🔷 NUEVO: Método para manejar cambios en el filtro de almacenes
        partial void OnFiltroAlmacenesTextoChanged(string value)
        {
            AlmacenesFiltroView?.Refresh();
        }
        
        // 🔷 NUEVO: Método para manejar cambios en el filtro de partida
        partial void OnFiltroPartidaTextoChanged(string value)
        {
            PartidasDisponiblesView?.Refresh();
        }
        
        // 🔷 NUEVO: Método para manejar cambios en el filtro de palets
        partial void OnFiltroPaletsTextoChanged(string value)
        {
            PaletsDisponiblesView?.Refresh();
        }
        
        // 🔷 NUEVO: Método para manejar cambios en la selección de partida
        partial void OnPartidaSeleccionadaChanged(string value)
        {
            // Aplicar filtrado cuando se seleccione una partida
            FiltrarResultadosPorAlmacen();
        }
        
        // 🔷 NUEVO: Método para manejar cambios en la selección de palet
        partial void OnPaletSeleccionadoChanged(string value)
        {
            // Aplicar filtrado cuando se seleccione un palet
            FiltrarResultadosPorAlmacen();
        }

        // 🔷 NUEVO: Método para filtrar resultados por almacén y partida sin hacer nueva búsqueda
        private void FiltrarResultadosPorAlmacen()
        {
            // Guardar el estado de expansión antes de limpiar solo si no hay estados guardados previamente
            // Esto evita sobrescribir estados guardados desde RefrescarAsync()
            if (_estadosExpansion.Count == 0)
            {
                GuardarEstadosExpansion();
            }
            
            // Limpiar resultados actuales
            ArticulosConUbicaciones.Clear();
            
            // Obtener stock filtrado
            var stockFiltrado = _todosLosResultadosStock;
            
            // Aplicar filtro por almacén si hay uno seleccionado
            if (AlmacenFiltroSeleccionado != null)
            {
                stockFiltrado = stockFiltrado.Where(x => x.CodigoAlmacen == AlmacenFiltroSeleccionado.CodigoAlmacen).ToList();
            }
            
            // 🔷 NUEVO: Aplicar filtro por partida si hay una seleccionada
            if (!string.IsNullOrWhiteSpace(PartidaSeleccionada))
            {
                stockFiltrado = stockFiltrado.Where(x => 
                    !string.IsNullOrEmpty(x.Partida) && 
                    x.Partida.Equals(PartidaSeleccionada, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            // 🔷 NUEVO: Aplicar filtro por palet si hay uno seleccionado
            if (!string.IsNullOrWhiteSpace(PaletSeleccionado))
            {
                stockFiltrado = stockFiltrado.Where(x => 
                    // Buscar en la lista Palets
                    (x.Palets != null && 
                     x.Palets.Any(p => !string.IsNullOrEmpty(p.CodigoPalet) && 
                                      p.CodigoPalet.Equals(PaletSeleccionado, StringComparison.OrdinalIgnoreCase))) ||
                    // O buscar en el campo CodigoPalet directo
                    (!string.IsNullOrEmpty(x.CodigoPalet) && 
                     x.CodigoPalet.Equals(PaletSeleccionado, StringComparison.OrdinalIgnoreCase))).ToList();
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
                        CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                        Partida = StockSeleccionado.Partida,
                        // 🔷 NUEVO: Incluir ubicación origen para verificar bloqueos específicos
                        AlmacenOrigen = StockSeleccionado.CodigoAlmacen,
                        UbicacionOrigen = StockSeleccionado.Ubicacion
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
            // 🔷 CORREGIDO: Solo filtrar los resultados existentes, NO hacer otra búsqueda
            FiltrarResultadosPorAlmacen();
        }

	}
} 