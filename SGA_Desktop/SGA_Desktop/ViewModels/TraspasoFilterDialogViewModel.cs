using System;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;

namespace SGA_Desktop.ViewModels
{
	public partial class TraspasoFilterDialogViewModel : ObservableObject
	{
		private readonly TraspasosService _traspasoService;
		public List<TraspasoDto> Filtrados { get; private set; } = new();

		// ▶️ Colección de estados
		public ObservableCollection<EstadoTraspasoDto> EstadosDisponibles { get; }
			= new ObservableCollection<EstadoTraspasoDto>();

		[ObservableProperty] private EstadoTraspasoDto? _estadoSeleccionado;

		// ▶️ Comando para “Aplicar”
		public IRelayCommand AplicarFiltrosCommand { get; }

		public TraspasoFilterDialogViewModel(TraspasosService traspasoService)
		{
			_traspasoService = traspasoService;

			AplicarFiltrosCommand = new AsyncRelayCommand(async () =>
			{
				// Llama a la API con el estado seleccionado
				var estado = EstadoSeleccionado?.CodigoEstado;
				Filtrados = await _traspasoService.ObtenerTraspasosFiltradosAsync(estado);

				// Cierra el diálogo
				var dlg = Application.Current.Windows
					.OfType<Window>()
					.FirstOrDefault(w => w.DataContext == this);
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
			var estados = await _traspasoService.ObtenerEstadosAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				EstadosDisponibles.Clear();
				// Inserta primero un “sin filtro”
				EstadosDisponibles.Add(new EstadoTraspasoDto
				{
					CodigoEstado = null!,
					Descripcion = "-- Todos los estados --"
				});
				foreach (var e in estados)
					EstadosDisponibles.Add(e);

				EstadoSeleccionado = EstadosDisponibles[0];
			});
		}
	}
}
