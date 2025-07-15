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
		private readonly StockService _stockService;
		private readonly PrintQueueService _printService;  // <- al igual que en impresiones
		private readonly UbicacionesService _ubicService;

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
			ImprimirPaletCommand.NotifyCanExecuteChanged();


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

		public ObservableCollection<ImpresoraDto> ImpresorasDisponibles { get; }

		public PaletizacionViewModel(PaletService paletService)
		{
			_paletService = paletService;
			_stockService = new StockService();
			_printService = new PrintQueueService();
			_ubicService = new UbicacionesService();
			// Inicializa comandos
			LoadPaletsCommand = new AsyncRelayCommand(LoadPaletsAsync);
			AbrirFiltrosCommand = new RelayCommand(OpenFiltros);
			CrearPaletCommand = new RelayCommand(AbrirPaletCrearDialog);
			LoadLineasCommand = new AsyncRelayCommand(LoadLineasPaletAsync);
			AbrirPaletLineasCommand = new RelayCommand(AbrirPaletLineas, PuedeAbrirPaletLineas);
			ImpresorasDisponibles = new ObservableCollection<ImpresoraDto>();

			// Inicialización común
			_ = LoadImpresorasAsync();
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
				_paletService,
				_stockService);

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

			var ok = await _paletService.EliminarLineaPaletAsync(lineaSeleccionada.Id, SessionManager.UsuarioActual.operario);
			if (ok)
				LineasPalet.Remove(lineaSeleccionada);
			else
				ErrorMessage = "No se pudo eliminar la línea";
		}

		private bool CanEliminarLinea() => lineaSeleccionada != null;

		//[RelayCommand(CanExecute = nameof(CanCerrar))]
		//private async Task CerrarPaletAsync()
		//{
		//	if (PaletSeleccionado == null) return;

		//	var confirm = new ConfirmationDialog(
		//		"Cerrar palet",
		//		$"¿Estás seguro de cerrar el palet {PaletSeleccionado.Codigo}?\nNo se podrán añadir más líneas.");
		//	if (confirm.ShowDialog() != true) return;

		//	var ok = await _paletService.CerrarPaletAsync(PaletSeleccionado.Id, SessionManager.UsuarioActual.operario);
		//	if (!ok)
		//	{
		//		ErrorMessage = "No se pudo cerrar el palet.";
		//		return;
		//	}

		//	// 🔷 Trae el palet completo actualizado
		//	var actualizado = await _paletService.ObtenerPaletPorIdAsync(PaletSeleccionado.Id);
		//	if (actualizado != null)
		//	{
		//		// Reemplaza el seleccionado
		//		PaletSeleccionado = actualizado;

		//		// Y actualiza la lista
		//		var idx = PaletsView.IndexOf(PaletsView.First(p => p.Id == actualizado.Id));
		//		if (idx >= 0)
		//			PaletsView[idx] = actualizado;
		//	}

		//	ErrorMessage = null;
		//}
		//[RelayCommand(CanExecute = nameof(CanCerrar))]
		//private async Task CerrarPaletAsync()
		//{
		//	if (PaletSeleccionado == null) return;

		//	// 🔷 Cargar las líneas del palet
		//	var lineas = await _paletService.ObtenerLineasAsync(PaletSeleccionado.Id);

		//	if (lineas.Count == 0)
		//	{
		//		var warningDlg = new WarningDialog(
		//			"Palet vacío",
		//			"El palet no contiene ninguna línea y no se puede cerrar.\n\nPor favor, añade artículos antes de intentar cerrarlo.",
		//			"\uE7BA" // ícono de advertencia
		//		)
		//		{ Owner = Application.Current.MainWindow };

		//		warningDlg.ShowDialog();
		//		return;
		//	}



		//	// 🔷 Mostrar diálogo con las líneas
		//	var dlg = new ConfirmationWithListDialog(lineas)
		//	{
		//		Owner = Application.Current.MainWindow
		//	};

		//	if (dlg.ShowDialog() != true) return;

		//	// 🔷 Llama al servicio para cerrar
		//	var ok = await _paletService.CerrarPaletAsync(PaletSeleccionado.Id, SessionManager.UsuarioActual.operario);
		//	if (!ok)
		//	{
		//		ErrorMessage = "No se pudo cerrar el palet.";
		//		return;
		//	}

		//	// 🔷 Trae el palet actualizado
		//	var actualizado = await _paletService.ObtenerPaletPorIdAsync(PaletSeleccionado.Id);
		//	if (actualizado != null)
		//	{
		//		PaletSeleccionado = actualizado;

		//		var idx = PaletsView.IndexOf(PaletsView.First(p => p.Id == actualizado.Id));
		//		if (idx >= 0)
		//			PaletsView[idx] = actualizado;
		//	}

		//	ErrorMessage = null;
		//}


		[RelayCommand(CanExecute = nameof(CanCerrar))]
		private async Task CerrarPaletAsync()
		{
			var empresa = SessionManager.EmpresaSeleccionada!.Value;
			var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
			var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

			if (PaletSeleccionado == null) return;

			// 🔷 Cargar las líneas del palet
			var lineas = await _paletService.ObtenerLineasAsync(PaletSeleccionado.Id);

			if (lineas.Count == 0)
			{
				new WarningDialog(
					"Palet vacío",
					"El palet no contiene ninguna línea y no se puede cerrar.\n\nPor favor, añade artículos antes de intentar cerrarlo.",
					"\uE7BA"
				)
				{ Owner = Application.Current.MainWindow }.ShowDialog();
				return;
			}

			// 🔷 Obtener almacén origen
			var almacenOrigen = lineas.FirstOrDefault()?.CodigoAlmacen;
			if (string.IsNullOrWhiteSpace(almacenOrigen))
			{
				ErrorMessage = "No se pudo determinar el almacén de origen del palet.";
				return;
			}

			// 🔷 Cargar los almacenes disponibles
			var almacenes = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

			// 🔷 Mostrar diálogo con líneas + almacenes
			var dlg = new ConfirmationWithListDialog(
				lineas,
				almacenes,
				_ubicService) // <-- aquí
			{
				Owner = Application.Current.MainWindow
			};

			if (dlg.ShowDialog() != true) return;

			// 🔷 Obtener la ubicación y almacén destino elegidos
			var ubicacionElegida = dlg.UbicacionSeleccionada;
			var almacenDestino = dlg.VM.AlmacenDestinoSeleccionado;

			if (ubicacionElegida == null || almacenDestino == null)
			{
				ErrorMessage = "Debes seleccionar una ubicación y un almacén destino para cerrar el palet.";
				return;
			}

			// 🔷 Llama al servicio para cerrar, pasando destino
			var ok = await _paletService.CerrarPaletAsync(
				PaletSeleccionado.Id,
				SessionManager.UsuarioActual.operario,
				almacenOrigen,
				almacenDestino.CodigoAlmacen,
				ubicacionElegida.Ubicacion
			);

			if (!ok)
			{
				ErrorMessage = "No se pudo cerrar el palet.";
				return;
			}

			// 🔷 Trae el palet actualizado
			var actualizado = await _paletService.ObtenerPaletPorIdAsync(PaletSeleccionado.Id);
			if (actualizado != null)
			{
				PaletSeleccionado = actualizado;

				var idx = PaletsView.IndexOf(PaletsView.First(p => p.Id == actualizado.Id));
				if (idx >= 0)
					PaletsView[idx] = actualizado;
			}

			ErrorMessage = null;
		}





		[RelayCommand(CanExecute = nameof(CanReabrir))]
		private async Task ReabrirPaletAsync()
		{
			if (PaletSeleccionado == null) return;

			var confirm = new ConfirmationDialog(
				"Reabrir palet",
				$"""
		¿Estás seguro de reabrir el palet {PaletSeleccionado.Codigo}?

		Al reabrir:
		• El traspaso pendiente asociado quedará CANCELADO.
		• Podrás añadir, modificar o eliminar líneas del palet.
		• Cuando lo cierres de nuevo, se generará un nuevo traspaso.

		¿Deseas continuar?
		""",
				"\uE7BA" // icono de advertencia/reapertura
			)
			{ Owner = Application.Current.MainWindow };

			if (confirm.ShowDialog() != true) return;

			// Llama al servicio para reabrir
			var ok = await _paletService.ReabrirPaletAsync(PaletSeleccionado.Id, SessionManager.UsuarioActual.operario);
			if (!ok)
			{
				ErrorMessage = "No se pudo reabrir el palet.";
				return;
			}

			// 🔷 Trae el palet completo actualizado
			var actualizado = await _paletService.ObtenerPaletPorIdAsync(PaletSeleccionado.Id);
			if (actualizado != null)
			{
				// Reemplaza el seleccionado
				PaletSeleccionado = actualizado;

				// Y actualiza la lista
				var idx = PaletsView.IndexOf(PaletsView.First(p => p.Id == actualizado.Id));
				if (idx >= 0)
					PaletsView[idx] = actualizado;
			}

			ErrorMessage = null;
		}

		[RelayCommand(CanExecute = nameof(CanImprimir))]
		private async Task ImprimirPaletAsync()
		{
			if (PaletSeleccionado is null) return;

			// Abrimos diálogo de impresión
			var dlgVm = new ConfirmarImpresionDialogViewModel(
				ImpresorasDisponibles,  // tienes que tener esta ObservableCollection en tu VM o pasarla
				ImpresorasDisponibles.FirstOrDefault());  // o la preferida si tienes

			var dlg = new ConfirmarImpresionDialog
			{
				Owner = Application.Current.MainWindow,
				DataContext = dlgVm
			};

			if (dlg.ShowDialog() != true) return;

			try
			{
				var dto = new LogImpresionDto
				{
					Usuario = SessionManager.Operario.ToString(),
					Dispositivo = Environment.MachineName,
					IdImpresora = dlgVm.ImpresoraSeleccionada?.Id ?? 0,
					EtiquetaImpresa = 0,
					Copias = dlgVm.NumeroCopias,
					CodigoArticulo = null,
					DescripcionArticulo = null,
					CodigoAlternativo = null,
					FechaCaducidad = null,
					Partida = null,
					Alergenos = null,
					PathEtiqueta = @"\\Sage200\mrh\Servicios\PrintCenter\ETIQUETAS\PALET.nlbl",
					TipoEtiqueta = 2,
					CodigoGS1 = PaletSeleccionado.CodigoGS1,
					CodigoPalet = PaletSeleccionado.Codigo
				};

				var printService = new PrintQueueService();
				await printService.InsertarRegistroImpresionAsync(dto);

				MessageBox.Show(
					$"Etiqueta del palet {dto.CodigoPalet} enviada a impresión.",
					"Impresión correcta",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					ex.Message,
					"Error al imprimir",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}

		private async Task LoadImpresorasAsync()
		{
			try
			{
				var lista = await _printService.ObtenerImpresorasAsync();

				ImpresorasDisponibles.Clear();
				foreach (var imp in lista.OrderBy(x => x.Nombre))
					ImpresorasDisponibles.Add(imp);
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					$"Error al cargar impresoras: {ex.Message}",
					"Error de impresoras",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}



		private bool CanImprimir() => PaletSeleccionado != null;




		private bool CanCerrar() => PaletSeleccionado?.Estado == "Abierto";
		private bool CanReabrir() => PaletSeleccionado?.Estado == "Cerrado";

		public bool PuedeCerrarPalet => PaletSeleccionado?.Estado == "Abierto";
		public bool PuedeReabrirPalet => PaletSeleccionado?.Estado == "Cerrado";



	}
}
