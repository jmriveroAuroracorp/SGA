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
		public ObservableCollection<PaletDto> PaletsView { get; } = new();
		[ObservableProperty] private PaletDto? paletSeleccionado;
		public ObservableCollection<LineaPaletDto> LineasPalet { get; } = new();

		public IAsyncRelayCommand LoadPaletsCommand { get; }
		public IRelayCommand AbrirFiltrosCommand { get; }
		public IAsyncRelayCommand LoadLineasCommand { get; }
		public IRelayCommand CrearPaletCommand { get; }

		public PaletizacionViewModel(PaletService paletService)
		{
			_paletService = paletService;

			// Inicializa comandos
			LoadPaletsCommand = new AsyncRelayCommand(LoadPaletsAsync);
			AbrirFiltrosCommand = new RelayCommand(OpenFiltros);
			CrearPaletCommand = new RelayCommand(AbrirPaletCrearDialog);
			LoadLineasCommand = new AsyncRelayCommand(LoadLineasPaletAsync);

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
	}
}
