using System;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;    // Para TipoPaletDto
using SGA_Desktop.Services;       // Para PaletService
using SGA_Desktop.Dialog;         // Para PaletFilterDialog

namespace SGA_Desktop.ViewModels
{
	public partial class PaletFilterDialogViewModel : ObservableObject
	{
		private readonly PaletService _paletService;

		public PaletFilterDialogViewModel(PaletService paletService)
		{
			_paletService = paletService;

			// 1) Estados disponibles (puedes cargarlos de otro sitio)
			EstadosDisponibles = new ObservableCollection<string>();
			TiposPaletDisponibles = new ObservableCollection<TipoPaletDto>();

			// 3) Comando para cerrar el diálogo
			AplicarFiltrosCommand = new RelayCommand(() =>
			{
				var dlg = Application.Current.Windows
					.OfType<PaletFilterDialog>()
					.FirstOrDefault();
				if (dlg != null)
					dlg.DialogResult = true;
			});
		}

		// ▶️ Colecciones y propiedades bindables
		public ObservableCollection<string> EstadosDisponibles { get; }
		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }

		[ObservableProperty] private string? codigo;
		[ObservableProperty] private string? estadoSeleccionado;
		[ObservableProperty] private TipoPaletDto? tipoPaletSeleccionado;
		[ObservableProperty] private DateTime? fechaApertura;
		[ObservableProperty] private DateTime? fechaCierre;
		[ObservableProperty] private DateTime? fechaDesde;
		[ObservableProperty] private DateTime? fechaHasta;
		[ObservableProperty] private int? usuarioApertura;
		[ObservableProperty] private int? usuarioCierre;

		public IRelayCommand AplicarFiltrosCommand { get; }

		/// <summary>
		/// Debe llamarse desde el Loaded del Window para no bloquear UI.
		/// </summary>
		public async Task InitializeAsync()
		{
			// 1) Carga Estados desde API
			//var estados = await _paletService.ObtenerEstadosAsync();
			//await Application.Current.Dispatcher.InvokeAsync(() =>
			//{
			//	EstadosDisponibles.Clear();
			//	foreach (var e in estados) EstadosDisponibles.Add(e);
			//});

			// 2) Carga TiposPalet desde API
			var tipos = await _paletService.ObtenerTiposPaletAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				TiposPaletDisponibles.Clear();
				foreach (var t in tipos)
					TiposPaletDisponibles.Add(t);
			});
		}
	}
}

