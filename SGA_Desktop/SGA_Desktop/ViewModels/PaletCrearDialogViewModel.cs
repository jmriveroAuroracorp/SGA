using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Windows;
using System.Collections.ObjectModel;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;


namespace SGA_Desktop.ViewModels
{
    public partial class PaletCrearDialogViewModel : ObservableObject
    {
        private readonly PaletService _paletService;

        // Para devolver el resultado al padre
        public PaletDto? CreatedPalet { get; private set; }

        public PaletCrearDialogViewModel(PaletService paletService)
        {
            _paletService = paletService;
            TiposPaletDisponibles = new ObservableCollection<TipoPaletDto>();
            CrearCommand = new AsyncRelayCommand(CrearAsync);

            _ = InitializeAsync();
        }

        public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }

        // La orden de trabajo (antes no existía)
        [ObservableProperty]
        private string? ordenTrabajoId;

        [ObservableProperty]
        private TipoPaletDto? tipoPaletSeleccionado;

        public IAsyncRelayCommand CrearCommand { get; }

        private async Task InitializeAsync()
        {
            var tipos = await _paletService.ObtenerTiposPaletAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TiposPaletDisponibles.Clear();
                foreach (var t in tipos)
                    TiposPaletDisponibles.Add(t);
            });
        }

        private async Task CrearAsync()
        {
            // 1) Construye el DTO mínimo que espera la API
            var dto = new PaletCrearDto
            {
                CodigoEmpresa     = SessionManager.EmpresaSeleccionada!.Value,
                UsuarioAperturaId = SessionManager.Operario,
                TipoPaletCodigo   = TipoPaletSeleccionado!.CodigoPalet,
                OrdenTrabajoId    = string.IsNullOrWhiteSpace(OrdenTrabajoId)
                                        ? null
                                        : OrdenTrabajoId
            };

            try
            {
                // 2) Llamada al API y recogida del Palet completo
                CreatedPalet = await _paletService.PaletCrearAsync(dto);
            }
            catch (Exception ex)
            {
                // Muestra el error en la UI
                // (puedes añadir un ErrorMessage con ObservableProperty si quieres)
                MessageBox.Show($"Error creando palet:\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3) Cerramos el diálogo con éxito
            var window = Application.Current.Windows
                                .OfType<Window>()
                                .FirstOrDefault(w => w.DataContext == this);
            if (window != null)
                window.DialogResult = true;
        }
    }
}