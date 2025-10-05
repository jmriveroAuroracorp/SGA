using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.Generic;

namespace SGA_Desktop.ViewModels
{
	public partial class GestionTraspasosViewModel : ObservableObject
	{
		private readonly TraspasosService _traspasosService;

		[ObservableProperty] private string? errorMessage;

		public ObservableCollection<TraspasoDto> Traspasos { get; } = new();

		[ObservableProperty] private TraspasoDto? traspasoSeleccionado;

		//partial void OnTraspasoSeleccionadoChanged(TraspasoDto? value)
		//{
		//	FinalizarTraspasoCommand.NotifyCanExecuteChanged();
		//	CancelarTraspasoCommand.NotifyCanExecuteChanged();
		//}

		public IAsyncRelayCommand LoadTraspasosCommand { get; }
		public IRelayCommand AbrirFiltrosCommand { get; }
		public IRelayCommand NuevoTraspasoCommand { get; }
		public IAsyncRelayCommand FinalizarTraspasoCommand { get; }
		public IAsyncRelayCommand CancelarTraspasoCommand { get; }

		public GestionTraspasosViewModel(TraspasosService traspasosService)
		{
			_traspasosService = traspasosService;

			LoadTraspasosCommand = new AsyncRelayCommand(LoadTraspasosAsync);
			AbrirFiltrosCommand = new RelayCommand(AbrirFiltros);
			NuevoTraspasoCommand = new RelayCommand(AbrirTraspasoPaletDialog);
			FinalizarTraspasoCommand = new AsyncRelayCommand(FinalizarTraspasoAsync, PuedeFinalizarTraspaso);
			CancelarTraspasoCommand = new AsyncRelayCommand(CancelarTraspasoAsync, PuedeCancelarTraspaso);

			_ = InitializeAsync();
		}

		public GestionTraspasosViewModel() : this(new TraspasosService()) { }

		private async Task InitializeAsync()
		{
			SessionManager.EmpresaCambiada += (s, e) => Traspasos.Clear();
			await Task.CompletedTask;
		}

		private async Task LoadTraspasosAsync()
		{
			try
			{
				var lista = await _traspasosService.ObtenerTraspasosAsync();
				Traspasos.Clear();

				// Agrupa por movimiento de palet y toma solo el primero de cada grupo
				var unicos = lista
					.Where(x => x.TipoTraspaso == "PALET")
					.GroupBy(x => new {
						x.PaletId,
						x.CodigoEstado,
						x.AlmacenDestino,
						x.UbicacionDestino
					})
					.Select(g => g.First())
					.ToList();

				foreach (var t in unicos)
					Traspasos.Add(t);

				ErrorMessage = null;
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private void AbrirFiltros()
		{
			var dlgVm = new TraspasoFilterDialogViewModel(_traspasosService);
			var dlg = new TraspasoFilterDialog
			{
				DataContext = dlgVm
			};
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dlg)
				dlg.Owner = owner;

			// Inicializa los estados en segundo plano
			dlg.Loaded += async (s, e) => await dlgVm.InitializeAsync();

			if (dlg.ShowDialog() == true)
			{
				_ = AplicarFiltrosAsync(
					dlgVm.EstadoSeleccionado?.CodigoEstado,
					dlgVm.CodigoPalet,
					dlgVm.AlmacenOrigen,
					dlgVm.AlmacenDestino,
					dlgVm.FechaInicioDesde,
					dlgVm.FechaInicioHasta
				);
			}
		}

		private async Task AplicarFiltrosAsync(string? estado, string? codigoPalet, string? almacenOrigen, string? almacenDestino, DateTime? fechaInicioDesde, DateTime? fechaInicioHasta)
		{
			var filtrados = await _traspasosService.ObtenerTraspasosFiltradosAsync(
				estado,
				codigoPalet,
				almacenOrigen,
				almacenDestino,
				fechaInicioDesde,
				fechaInicioHasta);

			Traspasos.Clear();
			var unicos = filtrados
				.Where(x => x.TipoTraspaso == "PALET")
				.GroupBy(x => new {
					x.PaletId,
					x.CodigoEstado,
					x.AlmacenDestino,
					x.UbicacionDestino
				})
				.Select(g => g.First())
				.ToList();

			foreach (var t in unicos)
				Traspasos.Add(t);
		}



		private void AbrirTraspasoPaletDialog()
		{
			var dlgVm = new TraspasoPaletDialogViewModel(); // Añade servicios si es necesario
			var dlg = new TraspasoPaletDialog
			{
				DataContext = dlgVm
			};
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dlg)
				dlg.Owner = owner;
			dlg.ShowDialog();
			// Si quieres refrescar la lista tras cerrar el diálogo, hazlo aquí
		}

		private bool PuedeFinalizarTraspaso()
		{
			return TraspasoSeleccionado != null &&
				   TraspasoSeleccionado.CodigoEstado?.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) == true;
		}

		private async Task FinalizarTraspasoAsync()
		{
			if (TraspasoSeleccionado is null) return;

			var confirm = new ConfirmationDialog(
				"Finalizar traspaso",
				$"¿Estás seguro de finalizar el traspaso del palet {TraspasoSeleccionado.PaletId}?");
			if (confirm.ShowDialog() != true) return;

			try
			{
				var dto = new FinalizarTraspasoDto
				{
					UbicacionDestino = TraspasoSeleccionado.UbicacionDestino,
					UsuarioFinalizacionId = SessionManager.UsuarioActual?.operario ?? 0,
					FechaFinalizacion = DateTime.Now
				};

				await _traspasosService.FinalizarTraspasoAsync(TraspasoSeleccionado.Id, dto);

				await ActualizarTraspasoEnLista(TraspasoSeleccionado.Id);

				ErrorMessage = null;
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private bool PuedeCancelarTraspaso()
		{
			return TraspasoSeleccionado != null &&
				   TraspasoSeleccionado.CodigoEstado?.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) == true;
		}

		private async Task CancelarTraspasoAsync()
		{
			if (TraspasoSeleccionado is null) return;

			var confirm = new ConfirmationDialog(
				"Cancelar traspaso",
				$"¿Estás seguro de cancelar el traspaso del palet {TraspasoSeleccionado.PaletId}?");
			if (confirm.ShowDialog() != true) return;

			try
			{
				var dto = new FinalizarTraspasoDto
				{
					UbicacionDestino = "CANCELADO",
					UsuarioFinalizacionId = SessionManager.UsuarioActual?.operario ?? 0,
					FechaFinalizacion = DateTime.Now
				};

				await _traspasosService.FinalizarTraspasoAsync(TraspasoSeleccionado.Id, dto);

				await ActualizarTraspasoEnLista(TraspasoSeleccionado.Id);

				ErrorMessage = null;
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private async Task ActualizarTraspasoEnLista(Guid traspasoId)
		{
			var actualizado = await _traspasosService.ObtenerTraspasoPorIdAsync(traspasoId);

			if (actualizado != null)
			{
				var idx = Traspasos.IndexOf(Traspasos.First(t => t.Id == actualizado.Id));
				if (idx >= 0)
					Traspasos[idx] = actualizado;

				TraspasoSeleccionado = actualizado;
				await CargarLineasPaletAsync(actualizado); // Recarga las líneas tras actualizar
			}
		}

		partial void OnTraspasoSeleccionadoChanged(TraspasoDto? value)
		{
			if (value != null)
			{
				_ = CargarLineasPaletAsync(value);
			}
		}

		private async Task CargarLineasPaletAsync(TraspasoDto value)
		{
			if (value.TipoTraspaso == "PALET" && value.PaletId != Guid.Empty)
			{
				var paletService = new Services.PaletService();
				var lineas = await paletService.ObtenerLineasAsync(value.PaletId);
				value.LineasPalet = lineas;
			}
			else
			{
				value.LineasPalet = new List<LineaPaletDto>();
			}

			if (ReferenceEquals(value, TraspasoSeleccionado))
			{
				OnPropertyChanged(nameof(TraspasoSeleccionado));
			}
		}
	}
}
