using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;

namespace SGA_Desktop.ViewModels
{
    public partial class TraspasoPaletDialogViewModel : ObservableObject
    {
        private readonly PaletService _paletService;
        private readonly UbicacionesService _ubicacionesService;
        private readonly TraspasosService _traspasosService = new TraspasosService();
        private readonly StockService _stockService = new StockService();

        // Buscador
        [ObservableProperty] private string? paletBuscado;

        // Lista de palets cerrados y con traspaso completado
        public ObservableCollection<PaletMovibleDto> PaletsCerrados { get; } = new();

        [ObservableProperty] private PaletMovibleDto? paletSeleccionado;

        // Destino
        public ObservableCollection<AlmacenDto> AlmacenesDestino { get; } = new();
        [ObservableProperty] private AlmacenDto? almacenDestinoSeleccionado;
        public ObservableCollection<UbicacionDto> UbicacionesDestino { get; } = new();
        [ObservableProperty] private UbicacionDto? ubicacionDestinoSeleccionada;
        [ObservableProperty] private bool ubicacionesHabilitadas = false;

        // Comandos
        public IRelayCommand BuscarPaletCommand { get; }
        public IRelayCommand<PaletMovibleDto> SeleccionarPaletCommand { get; }
        public IRelayCommand MoverPaletCommand { get; }


        public bool PuedeMoverPalet => PaletSeleccionado != null && AlmacenDestinoSeleccionado != null && UbicacionDestinoSeleccionada != null;

        //// Fecha de inicio del traspaso
        //private readonly DateTime _fechaInicioTraspaso = DateTime.Now;

        [ObservableProperty]
        private string? comentario;

        public TraspasoPaletDialogViewModel()
        {
            _paletService = new PaletService();
            _ubicacionesService = new UbicacionesService();

            BuscarPaletCommand = new RelayCommand(BuscarPalets);
            SeleccionarPaletCommand = new RelayCommand<PaletMovibleDto>(SeleccionarPalet);
            MoverPaletCommand = new RelayCommand(MoverPalet, () => PuedeMoverPalet);

            _ = CargarAlmacenesDestinoAsync();
        }

        public TraspasoPaletDialogViewModel(SGA_Desktop.Models.PaletDto palet)
        {
            _paletService = new PaletService();
            _ubicacionesService = new UbicacionesService();

            BuscarPaletCommand = new RelayCommand(BuscarPalets);
            SeleccionarPaletCommand = new RelayCommand<PaletMovibleDto>(SeleccionarPalet);
            MoverPaletCommand = new RelayCommand(MoverPalet, () => PuedeMoverPalet);

            // Precargar el palet seleccionado con datos básicos
            PaletSeleccionado = new PaletMovibleDto
            {
                Id = palet.Id,
                Codigo = palet.Codigo,
                Estado = palet.Estado,
                AlmacenOrigen = null, // Se cargará desde el servicio
                UbicacionOrigen = null,
                FechaUltimoTraspaso = null
            };

            _ = CargarDatosCompletosPaletAsync(palet.Id);
            _ = CargarAlmacenesDestinoAsync();
        }

        private async Task CargarDatosCompletosPaletAsync(Guid paletId)
        {
            try
            {
                // Obtener los datos completos del palet desde el servicio de traspasos
                var paletsCompletos = await _traspasosService.ObtenerPaletsCerradosMoviblesAsync();
                var paletCompleto = paletsCompletos.FirstOrDefault(p => p.Id == paletId);
                
                if (paletCompleto != null && PaletSeleccionado != null)
                {
                    PaletSeleccionado.AlmacenOrigen = paletCompleto.AlmacenOrigen;
                    PaletSeleccionado.UbicacionOrigen = paletCompleto.UbicacionOrigen;
                    PaletSeleccionado.FechaUltimoTraspaso = paletCompleto.FechaUltimoTraspaso;
                    PaletSeleccionado.UsuarioUltimoTraspaso = paletCompleto.UsuarioUltimoTraspaso;
                    
                    // Notificar cambios en las propiedades
                    OnPropertyChanged(nameof(PaletSeleccionado));
                }
            }
            catch (Exception ex)
            {
                // Manejo de error opcional - los datos básicos ya están cargados
                System.Diagnostics.Debug.WriteLine($"Error al cargar datos completos del palet: {ex.Message}");
            }
        }

        private async Task CargarAlmacenesDestinoAsync()
        {
            try
            {
                AlmacenesDestino.Clear();
                var empresa = Helpers.SessionManager.EmpresaSeleccionada;
                var centro = Helpers.SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var permisos = Helpers.SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                
                if (empresa == null) return;
                
                // Si no hay permisos específicos, obtener almacenes del centro
                if (!permisos.Any())
                {
                    permisos = await _stockService.ObtenerAlmacenesAsync(centro);
                }
                
                var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa.Value, centro, permisos, Helpers.SessionManager.Operario);
                
                foreach (var a in almacenes)
                {
                    AlmacenesDestino.Add(a);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes: {ex.Message}");
            }
        }

        partial void OnPaletSeleccionadoChanged(PaletMovibleDto? value)
        {
            MoverPaletCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeMoverPalet));
        }

        partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto? value)
        {
            MoverPaletCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeMoverPalet));
            
            if (value is not null)
            {
                UbicacionesHabilitadas = false;
                _ = CargarUbicacionesParaAlmacenAsync(value.CodigoAlmacen);
            }
            else
            {
                UbicacionesDestino.Clear();
                UbicacionesHabilitadas = false;
            }
        }

        partial void OnUbicacionDestinoSeleccionadaChanged(UbicacionDto? value)
        {
            MoverPaletCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(PuedeMoverPalet));
        }

        private async Task CargarUbicacionesParaAlmacenAsync(string codigoAlmacen)
        {
            UbicacionesDestino.Clear();
            UbicacionDestinoSeleccionada = null;
            UbicacionesHabilitadas = false;
            var empresa = Helpers.SessionManager.EmpresaSeleccionada;
            if (!empresa.HasValue) return;
            try
            {
                var lista = await _ubicacionesService.ObtenerUbicacionesVaciasOEspAsync(empresa.Value, codigoAlmacen);
                foreach (var u in lista)
                    UbicacionesDestino.Add(new Models.UbicacionDto
                    {
                        CodigoAlmacen = u.CodigoAlmacen,
                        Ubicacion = u.Ubicacion
                    });

                UbicacionesHabilitadas = UbicacionesDestino.Any();
            }
            catch
            {
                // Manejo de error opcional
                UbicacionesHabilitadas = false;
            }
        }

        private async void BuscarPalets()
        {
            PaletsCerrados.Clear();
            var lista = await _traspasosService.ObtenerPaletsCerradosMoviblesAsync();

            var filtrados = string.IsNullOrWhiteSpace(PaletBuscado)
                ? lista
                : lista.Where(p =>
                    !string.IsNullOrEmpty(p.Codigo) &&
                    p.Codigo.Contains(PaletBuscado)
                ).ToList();

            foreach (var palet in filtrados)
                PaletsCerrados.Add(palet);
        }

        private void SeleccionarPalet(PaletMovibleDto palet)
        {
            PaletSeleccionado = palet;
            // Cargar almacenes y ubicaciones destino según el palet seleccionado
        }

        private async void MoverPalet()
        {
            if (PaletSeleccionado == null || AlmacenDestinoSeleccionado == null || UbicacionDestinoSeleccionada == null)
                return;

            // 🔷 NUEVO: Validar traspaso de palet antes de ejecutarlo
            var validacion = await ValidarTraspasoPaletAsync();
            if (!validacion.EsValido)
            {
                System.Windows.MessageBox.Show($"❌ {validacion.MotivoBloqueo}", "Traspaso Bloqueado", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                var usuarioId = Helpers.SessionManager.UsuarioActual?.operario ?? 0;
                var empresa = Helpers.SessionManager.EmpresaSeleccionada.Value;
                var dto = new SGA_Desktop.Models.MoverPaletDto
                {
                    PaletId = PaletSeleccionado.Id,
                    CodigoPalet = PaletSeleccionado.Codigo,
                    UsuarioId = usuarioId,
                    AlmacenDestino = AlmacenDestinoSeleccionado.CodigoAlmacen,
                    UbicacionDestino = UbicacionDestinoSeleccionada.Ubicacion, // Puede ser ""
                    CodigoEstado = "PENDIENTE_ERP",
                    UsuarioFinalizacionId = usuarioId,
                    CodigoEmpresa = empresa,
                    TipoTraspaso = "PALET",
                    Comentario = Comentario // Nuevo campo
                    
                };
                var resp = await _traspasosService.MoverPaletAsync(dto);
                if (resp.Success)
                {
                    System.Windows.MessageBox.Show("Traspaso realizado correctamente.", "Éxito", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    // Cerrar el diálogo
                    CerrarVentana();
                }
                else
                {
                    System.Windows.MessageBox.Show($"Error al mover palet: {resp.ErrorMessage}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Error inesperado: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CerrarVentana()
        {
            // Busca la ventana asociada a este VM y la cierra
            var win = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.DataContext == this);
            win?.Close();
        }

        // 🔷 NUEVO: Validar traspaso de palet antes de ejecutarlo
        private async Task<ValidacionTraspasoResult> ValidarTraspasoPaletAsync()
        {
            try
            {
                if (PaletSeleccionado == null || UbicacionDestinoSeleccionada == null)
                    return ValidacionTraspasoResult.Valido();

                // 1. Obtener las líneas del palet (artículos que contiene)
                var lineasPalet = await _paletService.ObtenerLineasAsync(PaletSeleccionado.Id);
                
                if (!lineasPalet.Any())
                    return ValidacionTraspasoResult.Valido(); // Palet vacío, no hay problema

                // 2. Obtener códigos de artículos únicos del palet
                var codigosArticulos = lineasPalet
                    .Select(l => l.CodigoArticulo)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct()
                    .ToList();

                if (!codigosArticulos.Any())
                    return ValidacionTraspasoResult.Valido();

                // 3. Consultar bloqueos de calidad para los artículos del palet
                var bloqueosCalidad = await _stockService.ObtenerBloqueosCalidadAsync(
                    SessionManager.EmpresaSeleccionada!.Value, 
                    codigosArticulos);

                // 4. Verificar si algún artículo del palet está bloqueado por calidad
                var articulosBloqueados = codigosArticulos
                    .Where(codigo => bloqueosCalidad.ContainsKey(codigo) && 
                                   bloqueosCalidad[codigo].IsBloqueado)
                    .ToList();

                if (!articulosBloqueados.Any())
                    return ValidacionTraspasoResult.Valido(); // No hay artículos bloqueados

                // 5. Validar cada artículo bloqueado individualmente con su partida y ubicación origen
                var ubicacionDestino = UbicacionDestinoSeleccionada.Ubicacion;
                var almacenOrigen = PaletSeleccionado.AlmacenOrigen;
                var ubicacionOrigen = PaletSeleccionado.UbicacionOrigen;
                
                foreach (var codigoArticulo in articulosBloqueados)
                {
                    // Obtener la partida de este artículo desde las líneas del palet
                    var lineaArticulo = lineasPalet.FirstOrDefault(l => l.CodigoArticulo == codigoArticulo);
                    var partida = lineaArticulo?.Lote;
                    
                    var request = new ValidacionTraspasoRequest
                    {
                        CodigoArticulo = codigoArticulo,
                        AlmacenDestino = AlmacenDestinoSeleccionado?.CodigoAlmacen ?? "",
                        UbicacionDestino = ubicacionDestino,
                        CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
                        Partida = partida,
                        // 🔷 NUEVO: Incluir ubicación origen del palet para verificar bloqueos específicos
                        AlmacenOrigen = almacenOrigen,
                        UbicacionOrigen = ubicacionOrigen
                    };

                    var resultado = await _traspasosService.ValidarTraspasoArticuloAsync(request);
                    
                    if (!resultado.EsValido)
                    {
                        return ValidacionTraspasoResult.Bloqueado(
                            $"No se puede traspasar el palet. {resultado.MotivoBloqueo}");
                    }
                }

                return ValidacionTraspasoResult.Valido();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validando traspaso de palet: {ex.Message}");
                // En caso de error, permitir traspaso para no bloquear operaciones
                return ValidacionTraspasoResult.Valido();
            }
        }

        // Métodos adicionales eliminados: los combos ahora muestran todas las opciones sin filtrado
    }
} 