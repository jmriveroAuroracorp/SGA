using System;
using System.Windows;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using SGA_Desktop.Helpers;


namespace SGA_Desktop.ViewModels
{
	public partial class PaletizacionViewModel : ObservableObject
	{
		#region Fields & Services
		private readonly PaletService _paletService;
		#endregion

		#region Observable Properties
		[ObservableProperty]
		private string? errorMessage;

		public ObservableCollection<PaletDto> PaletsView { get; } = new();

		[ObservableProperty]
		private PaletDto? paletSeleccionado;

		public ObservableCollection<LineaPaletDto> LineasPalet { get; } = new();
		#endregion

		#region Commands
		public IAsyncRelayCommand LoadPaletsCommand { get; }
		public IRelayCommand AbrirFiltrosCommand { get; }
		public IAsyncRelayCommand LoadLineasCommand { get; }
		public IRelayCommand CrearPaletCommand { get; }
		public IRelayCommand ImprimirEtiquetaCommand { get; }
		#endregion

		#region Constructor
		public PaletizacionViewModel(PaletService paletService)
		{
			_paletService = paletService;

			
			AbrirFiltrosCommand = new RelayCommand(OpenFiltros);
			CrearPaletCommand = new RelayCommand(AbrirPaletCrearDialog);
			LoadLineasCommand = new AsyncRelayCommand(LoadLineasPaletAsync);
			//ImprimirEtiquetaCommand = new RelayCommand(PrintEtiqueta, () => PaletSeleccionado != null);

			this.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(PaletSeleccionado))
				{
		
					//ImprimirEtiquetaCommand.NotifyCanExecuteChanged();
				}
			};

		}

		// Sobrecarga para design-time
		public PaletizacionViewModel() : this(new PaletService()) { }
		#endregion

		#region Data Loading Methods
		
		private async Task LoadPaletsAsync()
		{
			try
			{
				var lista = await _paletService.ObtenerPaletsAsync(
					// pasa aquí los parámetros por defecto o vacíos
					codigoEmpresa: SessionManager.EmpresaSeleccionada!.Value
				);
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
				foreach (var l in lineas) LineasPalet.Add(l);
				ErrorMessage = null;
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
			}
		}
		#endregion

		#region UI Actions
		private async void OpenFiltros()
		{
			var dlg = new PaletFilterDialog { Owner = Application.Current.MainWindow };
			if (dlg.ShowDialog() != true) return;

			var f = (PaletFilterDialogViewModel)dlg.DataContext;
			var empresa = SessionManager.EmpresaSeleccionada!.Value;

			var lista = await _paletService.ObtenerPaletsAsync(
				codigoEmpresa: empresa,
				codigo: f.Codigo,
				estado: f.EstadoSeleccionado?.CodigoEstado,
				tipoPaletCodigo: f.TipoPaletSeleccionado?.CodigoPalet,
				fechaApertura: f.FechaApertura,
				fechaCierre: f.FechaCierre,
				fechaDesde: f.FechaDesde,   // ← aquí antes usabas fechaAperturaDesde
				fechaHasta: f.FechaHasta,   // ← y aquí fechaCierreHasta
				usuarioApertura: f.UsuarioApertura,
				usuarioCierre: f.UsuarioCierre
				//sinCierre: f.SinCierre     // si lo usas
			);

			PaletsView.Clear();
			foreach (var p in lista)
				PaletsView.Add(p);
		}
		private void AbrirPaletCrearDialog()
		{
			var dlg = new PaletCrearDialog
			{
				Owner = Application.Current.MainWindow
			};

			// Si el usuario pulsa “Crear” (DialogResult = true)…
			if (dlg.ShowDialog() == true)
			{
				// 1) Releer todos los palets (o añadir solo el creado)
				_ = LoadPaletsAsync();
			}
		}

		#endregion
	}
}
