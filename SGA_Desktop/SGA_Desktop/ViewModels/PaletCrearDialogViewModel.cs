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

		public PaletCrearDialogViewModel(PaletService paletService)
		{
			_paletService = paletService;
			// cargar catálogos
			TiposPaletDisponibles = new ObservableCollection<TipoPaletDto>();
			CrearCommand = new AsyncRelayCommand(CrearAsync);

			// initializer
			_ = InitializeAsync();
		}
		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }

		[ObservableProperty] private string? codigo;
		[ObservableProperty] private TipoPaletDto? tipoPaletSeleccionado;
		[ObservableProperty] private decimal altura;
		[ObservableProperty] private decimal peso;

		public IAsyncRelayCommand CrearCommand { get; }

		private async Task InitializeAsync()
		{
			// 1) Tipos de palet
			var tipos = await _paletService.ObtenerTiposPaletAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				TiposPaletDisponibles.Clear();
				foreach (var t in tipos) TiposPaletDisponibles.Add(t);
			});

			
		}

		private async Task CrearAsync()
		{
			// Aquí rellenamos el DTO con SessionManager
			var dto = new PaletCrearDto
			{
				CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
				UsuarioAperturaId = SessionManager.Operario,
				Codigo = this.Codigo!,
				TipoPaletCodigo = this.TipoPaletSeleccionado!.CodigoPalet,
				Altura = this.Altura,
				Peso = this.Peso
			};

			// Llamada al API
			var creado = await _paletService.PaletCrearAsync(dto);

			// Cerramos el diálogo
			var window = Application.Current.Windows
								.OfType<Window>()
								.FirstOrDefault(w => w.DataContext == this);
			if (window != null)
				window.DialogResult = true;
		}
	}
}
