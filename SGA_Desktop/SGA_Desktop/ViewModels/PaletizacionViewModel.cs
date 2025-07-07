using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
	public partial class PaletizacionViewModel : ObservableObject
	{
		private readonly PaletService _paletService;

		[ObservableProperty] private string? errorMessage;
		[ObservableProperty]
		private LineaPaletDto? lineaSeleccionada;
		partial void OnLineaSeleccionadaChanged(LineaPaletDto? value)
		{
			EliminarLineaSeleccionadaCommand.NotifyCanExecuteChanged();
		}
		public ObservableCollection<PaletDto> PaletsView { get; } = new();
		[ObservableProperty] private PaletDto? paletSeleccionado;
		partial void OnPaletSeleccionadoChanged(PaletDto? value)
		{
			AbrirPaletLineasCommand.NotifyCanExecuteChanged();
			CerrarPaletCommand.NotifyCanExecuteChanged();
			ReabrirPaletCommand.NotifyCanExecuteChanged();

			OnPropertyChanged(nameof(PuedeCerrarPalet));
			OnPropertyChanged(nameof(PuedeReabrirPalet));

			_ = LoadLineasPaletAsync();
		}
		public ObservableCollection<LineaPaletDto> LineasPalet { get; } = new();

		public IAsyncRelayCommand LoadPaletsCommand { get; }
		public IRelayCommand AbrirFiltrosCommand { get; }
		public IAsyncRelayCommand LoadLineasCommand { get; }
		public IRelayCommand CrearPaletCommand { get; }
		public IRelayCommand AbrirPaletLineasCommand { get; }


		public PaletizacionViewModel(PaletService paletService)
		{
			_paletService = paletService;

			// Inicializa comandos
			LoadPaletsCommand = new AsyncRelayCommand(LoadPaletsAsync);
			AbrirFiltrosCommand = new RelayCommand(OpenFiltros);
			CrearPaletCommand = new RelayCommand(AbrirPaletCrearDialog);
			LoadLineasCommand = new AsyncRelayCommand(LoadLineasPaletAsync);
			AbrirPaletLineasCommand = new RelayCommand(AbrirPaletLineas, PuedeAbrirPaletLineas);


			// Inicialización común
			_ = InitializeAsync();
		}

		// Para diseño en XAML
		public PaletizacionViewModel() : this(new PaletService()) { }

		private async Task InitializeAsync()
		{
			// Limpia la grilla al cambiar de empresa
			SessionManager.EmpresaCambiada += (s, e) => PaletsView.Clear();

			// Espacio para precargar otros datos si hiciera falta
			await Task.CompletedTask;
		}

		private async Task LoadPaletsAsync()
		{
			try
			{
				var lista = await _paletService.ObtenerPaletsAsync(
					codigoEmpresa: SessionManager.EmpresaSeleccionada!.Value);
				PaletsView.Clear();
				foreach (var p in lista)
					PaletsView.Add(p);
				ErrorMessage = null;
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private async Task LoadLineasPaletAsync()
		{
			LineasPalet.Clear();
			if (PaletSeleccionado is null) return;

			try
			{
				var lineas = await _paletService.ObtenerLineasAsync(PaletSeleccionado.Id);
				foreach (var l in lineas)
					LineasPalet.Add(l);
				ErrorMessage = null;
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}

		private async void OpenFiltros()
		{
			var dlgVm = new PaletFilterDialogViewModel(_paletService);

			var empresa = SessionManager.EmpresaSeleccionada!.Value;

			// Trae palets para rellenar usuarios
			var lista = await _paletService.ObtenerPaletsAsync(
				codigoEmpresa: empresa
			);

			dlgVm.ActualizarUsuariosDisponibles(lista);

			// 🔷 IMPORTANTE: cargar estados y tipos de palet
			await dlgVm.InitializeAsync();

			var dlg = new PaletFilterDialog
			{
				Owner = Application.Current.MainWindow,
				DataContext = dlgVm
			};

			if (dlg.ShowDialog() != true) return;

			var f = (PaletFilterDialogViewModel)dlg.DataContext;

			var filtrados = await _paletService.ObtenerPaletsAsync(
				codigoEmpresa: empresa,
				codigo: f.Codigo,
				estado: f.EstadoSeleccionado?.CodigoEstado,
				tipoPaletCodigo: f.TipoPaletSeleccionado?.CodigoPalet,
				fechaApertura: f.FechaApertura,
				fechaCierre: f.FechaCierre,
				fechaDesde: f.FechaDesde,
				fechaHasta: f.FechaHasta,
				usuarioApertura: f.UsuarioAperturaSeleccionado?.UsuarioId == 0 ? null : f.UsuarioAperturaSeleccionado?.UsuarioId,
				usuarioCierre: f.UsuarioCierreSeleccionado?.UsuarioId == 0 ? null : f.UsuarioCierreSeleccionado?.UsuarioId);

			PaletsView.Clear();
			foreach (var p in filtrados)
				PaletsView.Add(p);
		}



		private async void AbrirPaletCrearDialog()
		{
			var dlgVm = new PaletCrearDialogViewModel(_paletService);
			var dlg = new PaletCrearDialog { DataContext = dlgVm, Owner = Application.Current.MainWindow };

			if (dlg.ShowDialog() == true && dlgVm.CreatedPalet != null)
				PaletsView.Add(dlgVm.CreatedPalet);
		}

		private bool PuedeAbrirPaletLineas()
		{
			return PaletSeleccionado != null;
		}

		private async void AbrirPaletLineas()
		{
			if (PaletSeleccionado is null) return;

			var dlgVm = new PaletLineasDialogViewModel(
				PaletSeleccionado.Id,
				PaletSeleccionado.Codigo,
				PaletSeleccionado.TipoPaletCodigo,
				PaletSeleccionado.Estado,
				_paletService);

			var dlg = new PaletLineasDialog
			{
				Owner = Application.Current.MainWindow,
				DataContext = dlgVm
			};

			dlg.ShowDialog();

			// 🔷 al cerrar el diálogo, recarga las líneas
			await LoadLineasPaletAsync();
		}

		[RelayCommand(CanExecute = nameof(CanEliminarLinea))]
		private async Task EliminarLineaSeleccionadaAsync()
		{
			if (lineaSeleccionada == null) return;

			string detalle =
				$"""
		Artículo: {lineaSeleccionada.DescripcionArticulo}
		Cantidad: {lineaSeleccionada.Cantidad}
		Ubicación: {lineaSeleccionada.Ubicacion}
		Lote: {lineaSeleccionada.Lote}
		""";

			var dlg = new ConfirmationDialog(
				"Confirmar eliminación",
				$"¿Estás seguro de que quieres eliminar esta línea?\n\n{detalle}",
				"\uE74D" // icono de papelera
			)
			{
				Owner = Application.Current.MainWindow
			};

			if (dlg.ShowDialog() != true) return;

			var ok = await _paletService.EliminarLineaPaletAsync(lineaSeleccionada.Id);
			if (ok)
				LineasPalet.Remove(lineaSeleccionada);
			else
				ErrorMessage = "No se pudo eliminar la línea";
		}

		private bool CanEliminarLinea() => lineaSeleccionada != null;

		[RelayCommand(CanExecute = nameof(CanCerrar))]
		private async Task CerrarPaletAsync()
		{
			if (PaletSeleccionado == null) return;

			var confirm = new ConfirmationDialog(
				"Cerrar palet",
				$"¿Estás seguro de cerrar el palet {PaletSeleccionado.Codigo}?\nNo se podrán añadir más líneas.");
			if (confirm.ShowDialog() != true) return;

			var ok = await _paletService.CerrarPaletAsync(PaletSeleccionado.Id, SessionManager.UsuarioActual.operario);
			if (ok)
			{
				PaletSeleccionado.Estado = "Cerrado";
				ErrorMessage = null;
			}
			else
			{
				ErrorMessage = "No se pudo cerrar el palet.";
			}
		}

		[RelayCommand(CanExecute = nameof(CanReabrir))]
		private async Task ReabrirPaletAsync()
		{
			if (PaletSeleccionado == null) return;

			var confirm = new ConfirmationDialog(
				"Reabrir palet",
				$"¿Estás seguro de reabrir el palet {PaletSeleccionado.Codigo}?\nPodrás añadir o eliminar líneas.");
			if (confirm.ShowDialog() != true) return;

			var ok = await _paletService.ReabrirPaletAsync(PaletSeleccionado.Id);
			if (ok)
			{
				PaletSeleccionado.Estado = "Abierto";
				ErrorMessage = null;
			}
			else
			{
				ErrorMessage = "No se pudo reabrir el palet.";
			}
		}

		private bool CanCerrar() => PaletSeleccionado?.Estado == "Abierto";
		private bool CanReabrir() => PaletSeleccionado?.Estado == "Cerrado";

		public bool PuedeCerrarPalet => PaletSeleccionado?.Estado == "Abierto";
		public bool PuedeReabrirPalet => PaletSeleccionado?.Estado == "Cerrado";
	}
}
