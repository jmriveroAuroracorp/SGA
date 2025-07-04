using System;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models; // Aquí debe estar EstadoPaletDto y TipoPaletDto
using SGA_Desktop.Services;       // Para PaletService
using SGA_Desktop.Dialog;         // Para PaletFilterDialog

namespace SGA_Desktop.ViewModels
{
	public partial class PaletFilterDialogViewModel : ObservableObject
	{
		private readonly PaletService _paletService;

		// ▶️ Colecciones bindables
		public ObservableCollection<EstadoPaletDto> EstadosDisponibles { get; }
			= new ObservableCollection<EstadoPaletDto>();
		[ObservableProperty] private EstadoPaletDto? _estadoSeleccionado;

		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }
			= new ObservableCollection<TipoPaletDto>();
		[ObservableProperty] private TipoPaletDto? _tipoPaletSeleccionado;

		// ▶️ Otros filtros
		[ObservableProperty] private string? _codigo;
		[ObservableProperty] private DateTime? _fechaApertura;
		[ObservableProperty] private DateTime? _fechaCierre;
		[ObservableProperty] private DateTime? _fechaDesde;
		[ObservableProperty] private DateTime? _fechaHasta;
		[ObservableProperty] private int? _usuarioApertura;
		[ObservableProperty] private int? _usuarioCierre;

		// ▶️ Comando para “Aplicar”
		public IRelayCommand AplicarFiltrosCommand { get; }

		public PaletFilterDialogViewModel(PaletService paletService)
		{
			_paletService = paletService;

			AplicarFiltrosCommand = new RelayCommand(() =>
			{
				var dlg = Application.Current.Windows
					.OfType<PaletFilterDialog>()
					.FirstOrDefault();
				if (dlg != null)
					dlg.DialogResult = true;
			});
		}

		/// <summary>
		/// Llamar en Loaded del Window para no bloquear la UI.
		/// </summary>
		public async Task InitializeAsync()
		{
			// 1) Carga Estados desde API
			var estados = await _paletService.ObtenerEstadosAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				EstadosDisponibles.Clear();
				// Inserta primero un “sin filtro”
				EstadosDisponibles.Add(new EstadoPaletDto
				{
					CodigoEstado = null!,
					Descripcion = "-- Todos los estados --",
					Orden = 0
				});
				foreach (var e in estados)
					EstadosDisponibles.Add(e);

				// Al no forzar SelectedItem, queda en el “sin filtro”
				EstadoSeleccionado = EstadosDisponibles[0];
			});

			// 2) Carga TiposPalet desde API
			var tipos = await _paletService.ObtenerTiposPaletAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				TiposPaletDisponibles.Clear();
				// Inserta “sin filtro”
				TiposPaletDisponibles.Add(new TipoPaletDto
				{
					CodigoPalet = null!,
					Descripcion = "-- Todos los tipos --"
				});
				foreach (var t in tipos)
					TiposPaletDisponibles.Add(t);

				TipoPaletSeleccionado = TiposPaletDisponibles[0];
			});

		}
	}
}
