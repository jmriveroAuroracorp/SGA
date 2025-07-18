using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        // Comandos
        public IRelayCommand BuscarPaletCommand { get; }
        public IRelayCommand<PaletMovibleDto> SeleccionarPaletCommand { get; }
        public IRelayCommand MoverPaletCommand { get; }

        public bool PuedeMoverPalet => PaletSeleccionado != null && AlmacenDestinoSeleccionado != null && UbicacionDestinoSeleccionada != null;

        public TraspasoPaletDialogViewModel()
        {
            _paletService = new PaletService();
            _ubicacionesService = new UbicacionesService();

            BuscarPaletCommand = new RelayCommand(BuscarPalets);
            SeleccionarPaletCommand = new RelayCommand<PaletMovibleDto>(SeleccionarPalet);
            MoverPaletCommand = new RelayCommand(MoverPalet, () => PuedeMoverPalet);

            _ = CargarAlmacenesDestinoAsync();
        }

        private async Task CargarAlmacenesDestinoAsync()
        {
            AlmacenesDestino.Clear();
            var empresa = Helpers.SessionManager.EmpresaSeleccionada;
            var centro = Helpers.SessionManager.UsuarioActual?.codigoCentro ?? "0";
            var desdeLogin = Helpers.SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
            if (empresa == null) return;
            var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa.Value, centro, desdeLogin);
            foreach (var a in almacenes)
                AlmacenesDestino.Add(a);
            AlmacenDestinoSeleccionado = AlmacenesDestino.FirstOrDefault();
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
                _ = CargarUbicacionesParaAlmacenAsync(value.CodigoAlmacen);
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
            }
            catch
            {
                // Manejo de error opcional
            }
        }

        private async void BuscarPalets()
        {
            PaletsCerrados.Clear();
            var lista = await _traspasosService.ObtenerPaletsCerradosMoviblesAsync();

            var filtro = PaletBuscado?.Replace("-", "").Replace(" ", "").ToUpperInvariant() ?? "";
            var filtrados = string.IsNullOrWhiteSpace(filtro)
                ? lista
                : lista.Where(p =>
                    !string.IsNullOrEmpty(p.Codigo) &&
                    p.Codigo.Replace("-", "").Replace(" ", "").ToUpperInvariant().Contains(filtro)
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

            try
            {
                var usuarioId = Helpers.SessionManager.UsuarioActual?.operario ?? 0;
                var dto = new SGA_Desktop.Models.MoverPaletDto
                {
                    PaletId = PaletSeleccionado.Id,
                    CodigoPalet = PaletSeleccionado.Codigo,
                    UsuarioId = usuarioId,
                    AlmacenDestino = AlmacenDestinoSeleccionado.CodigoAlmacen,
                    UbicacionDestino = UbicacionDestinoSeleccionada.Ubicacion, // Puede ser ""
                    CodigoEstado = "PENDIENTE_ERP"
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
    }
} 