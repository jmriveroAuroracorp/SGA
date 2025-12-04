using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.ViewModels
{
	public partial class TraspasosViewModel : ObservableObject
	{
		// Servicios
		private readonly PaletService _paletService;
		private readonly StockService _stockService;
		private readonly PrintQueueService _printService;
		private readonly UbicacionesService _ubicService;
		private readonly LoginService _loginService;
		private readonly TraspasosService _traspasosService;

		// Propiedades de navegación - Ya no necesarias
		
		// Propiedades de estado
		[ObservableProperty] private string mensaje = "Listo";
		[ObservableProperty] private bool cargando = false;
		[ObservableProperty] private string? errorMessage;
		[ObservableProperty] private bool mostrandoErrores;
		[ObservableProperty] private bool hayErroresErp;
		[ObservableProperty] private bool mostrandoPendientesVaciado;
		[ObservableProperty] private bool hayPaletsPendientesVaciado;

		// Propiedades de Paletización
		[ObservableProperty] private PaletDto? paletSeleccionado;
		[ObservableProperty] private LineaPaletDto? lineaSeleccionada;
		
		// Colecciones
		public ObservableCollection<PaletDto> PaletsView { get; } = new();
		public ObservableCollection<LineaPaletDto> LineasPalet { get; } = new();
		public ObservableCollection<ImpresoraDto> ImpresorasDisponibles { get; } = new();

		// Comandos de navegación - Ya no necesarios

        // Comandos de Paletización
        public IAsyncRelayCommand LoadPaletsCommand { get; }
        public IRelayCommand AbrirFiltrosCommand { get; }
        public IAsyncRelayCommand LoadLineasCommand { get; }
        public IAsyncRelayCommand MostrarTraspasosErrorCommand { get; }
        public IAsyncRelayCommand MostrarPaletsPendientesCommand { get; }
        public IAsyncRelayCommand CrearPaletCommand { get; }
        public IRelayCommand AbrirPaletLineasCommand { get; }
        public IRelayCommand<PaletDto> SeleccionarPaletCommand { get; }
        public IRelayCommand CerrarContenidoCommand { get; }
        public IRelayCommand VerPaletSeleccionadoCommand { get; }
        public IRelayCommand ImprimirPaletSeleccionadoCommand { get; }
        public IRelayCommand EliminarLineaSeleccionadaCommand { get; }
        public IAsyncRelayCommand FinalizarTraspasoCommand { get; }
        public IAsyncRelayCommand TraspasarPaletCommand { get; }
        public IAsyncRelayCommand<PaletDto?> VaciarPaletPendienteCommand { get; }
        // Los comandos CerrarPaletCommand, ReabrirPaletCommand e ImprimirPaletCommand se generan automáticamente por [RelayCommand]

	public TraspasosViewModel()
	{
		
		// Inicializar servicios
		_paletService = new PaletService();
		_stockService = new StockService();
		_printService = new PrintQueueService();
		_ubicService = new UbicacionesService();
		_loginService = new LoginService();
		_traspasosService = new TraspasosService();

		// Comandos de navegación - Ya no necesarios

        // Comandos de Paletización
        LoadPaletsCommand = new AsyncRelayCommand(LoadPaletsAsync);
        AbrirFiltrosCommand = new RelayCommand(OpenFiltros);
        CrearPaletCommand = new AsyncRelayCommand(AbrirPaletCrearDialog);
        LoadLineasCommand = new AsyncRelayCommand(LoadLineasPaletAsync);
        MostrarTraspasosErrorCommand = new AsyncRelayCommand(ToggleErroresAsync, () => MostrandoErrores || HayErroresErp);
        MostrarPaletsPendientesCommand = new AsyncRelayCommand(TogglePendientesVaciadoAsync, () => MostrandoPendientesVaciado || HayPaletsPendientesVaciado);
        VaciarPaletPendienteCommand = new AsyncRelayCommand<PaletDto?>(VaciarPaletPendienteAsync, CanVaciarPaletPendiente);
        AbrirPaletLineasCommand = new RelayCommand(AbrirPaletLineas, PuedeAbrirPaletLineas);
        SeleccionarPaletCommand = new RelayCommand<PaletDto>(SeleccionarPalet);
        CerrarContenidoCommand = new RelayCommand(CerrarContenido);
        VerPaletSeleccionadoCommand = new RelayCommand(VerPaletSeleccionado, PuedeVerPalet);
        ImprimirPaletSeleccionadoCommand = new RelayCommand(ImprimirPaletSeleccionado, PuedeImprimirPalet);
        EliminarLineaSeleccionadaCommand = new RelayCommand(EliminarLineaSeleccionada, PuedeEliminarLinea);
        FinalizarTraspasoCommand = new AsyncRelayCommand(FinalizarTraspasoAsync);
        TraspasarPaletCommand = new AsyncRelayCommand(TraspasarPaletAsync);
        // Los comandos CerrarPaletCommand, ReabrirPaletCommand e ImprimirPaletCommand se inicializan automáticamente por [RelayCommand]

		// Inicialización
		_ = InitializeAsync();
		
		// Solo cargar impresoras si la aplicación no se está cerrando y no estamos en modo de diseño
		if (!SessionManager.IsClosing && !System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
		{
			_ = LoadImpresorasAsync();
		}
		
		_ = LoadPaletsAsync(); // Cargar palets automáticamente

	}

	// Métodos de navegación eliminados - Ya no se necesitan

		// Métodos de Paletización
		private async Task InitializeAsync()
		{
			SessionManager.EmpresaCambiada += (s, e) => PaletsView.Clear();
			await Task.CompletedTask;
		}

		public string TextoBotonErrores => MostrandoErrores ? "← Ver palets" : "⚠️ Errores ERP";
		public Visibility BotonErroresVisibility => HayErroresErp ? Visibility.Visible : Visibility.Collapsed;
		public string TextoBotonPendientes => MostrandoPendientesVaciado ? "← Ver palets" : "⬛ Pendientes vaciar";
		public Visibility BotonPendientesVisibility => HayPaletsPendientesVaciado ? Visibility.Visible : Visibility.Collapsed;

		partial void OnMostrandoErroresChanged(bool value)
		{
			OnPropertyChanged(nameof(TextoBotonErrores));
			RelanzarTraspasoErrorCommand.NotifyCanExecuteChanged();
			MostrarTraspasosErrorCommand.NotifyCanExecuteChanged();
			MostrarPaletsPendientesCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(BotonErroresVisibility));
		}

		partial void OnHayErroresErpChanged(bool value)
		{
			MostrarTraspasosErrorCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(BotonErroresVisibility));
		}

		partial void OnMostrandoPendientesVaciadoChanged(bool value)
		{
			OnPropertyChanged(nameof(TextoBotonPendientes));
			MostrarPaletsPendientesCommand.NotifyCanExecuteChanged();
			MostrarTraspasosErrorCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(BotonPendientesVisibility));
		}

		partial void OnHayPaletsPendientesVaciadoChanged(bool value)
		{
			MostrarPaletsPendientesCommand.NotifyCanExecuteChanged();
			OnPropertyChanged(nameof(BotonPendientesVisibility));
		}

		private async Task LoadPaletsAsync()
		{
			if (MostrandoErrores)
			{
				await LoadTraspasosErrorAsync();
				return;
			}

			if (MostrandoPendientesVaciado)
			{
				await LoadPaletsPendientesVaciadoAsync();
				return;
			}

			try
			{
				Cargando = true;
				Mensaje = "Cargando palets...";
				
				var lista = await _paletService.ObtenerPaletsAsync(
					codigoEmpresa: SessionManager.EmpresaSeleccionada!.Value);
				
				// Obtener información de traspaso para todos los palets (cerrados y abiertos)
				var paletsConTraspaso = await _traspasosService.ObtenerPaletsConUbicacionAsync();
				
				// 🔒 FILTRO DE SEGURIDAD: Obtener almacenes permitidos del usuario
				var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();
				
				PaletsView.Clear();
				foreach (var p in lista)
				{
					p.ErrorErpMensaje = null;
					p.TraspasoErrorId = null;
					// Buscar información de traspaso para cualquier palet (cerrado o abierto)
					// Solo los palets recién creados no tendrán esta información
					var paletConTraspaso = paletsConTraspaso.FirstOrDefault(pt => pt.Id == p.Id);
					if (paletConTraspaso != null)
					{
						p.AlmacenOrigen = paletConTraspaso.AlmacenOrigen;
						p.UbicacionOrigen = paletConTraspaso.UbicacionOrigen;
						p.FechaUltimoTraspaso = paletConTraspaso.FechaUltimoTraspaso;
						p.UsuarioUltimoTraspaso = paletConTraspaso.UsuarioUltimoTraspaso;
					}
					
					// 🔒 APLICAR FILTRO DE SEGURIDAD: Solo mostrar palets de almacenes permitidos
					// (después de obtener la información de ubicación)
					// Si el palet no tiene ubicación (recién creado), permitirlo si el usuario tiene acceso general
					bool puedeVerPalet = string.IsNullOrEmpty(p.AlmacenOrigen) || 
										almacenesPermitidos.Contains(p.AlmacenOrigen);
					
					if (puedeVerPalet)
					{
						PaletsView.Add(p);
					}
				}
				
				Debug.WriteLine($"Palets totales: {lista.Count}, Palets permitidos: {PaletsView.Count}");
				
				// Actualizar usuarios disponibles para los filtros
				ActualizarUsuariosDisponibles(PaletsView.ToList());
				
				Mensaje = $"Se cargaron {PaletsView.Count} palets correctamente";
				ErrorMessage = null;

				await ActualizarIndicadorPendientesAsync();
				await ActualizarIndicadorErroresAsync();
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al cargar palets";
			}
			finally
			{
				Cargando = false;
				RelanzarTraspasoErrorCommand.NotifyCanExecuteChanged();
				VaciarPaletPendienteCommand.NotifyCanExecuteChanged();
			}
		}

		private async Task ActualizarIndicadorPendientesAsync()
		{
			if (!SessionManager.EmpresaSeleccionada.HasValue)
			{
				HayPaletsPendientesVaciado = false;
				return;
			}

			try
			{
				var empresa = SessionManager.EmpresaSeleccionada.Value;
				var pendientes = await _paletService.ObtenerPaletsPendientesVaciadoAsync(empresa);
				HayPaletsPendientesVaciado = pendientes.Any();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error comprobando palets pendientes de vaciar: {ex.Message}");
				HayPaletsPendientesVaciado = false;
			}
		}

	private async Task ActualizarIndicadorErroresAsync()
	{
		if (!SessionManager.EmpresaSeleccionada.HasValue)
		{
			HayErroresErp = false;
			return;
		}

		try
		{
			var empresa = SessionManager.EmpresaSeleccionada.Value;
			var errores = await _paletService.ObtenerTraspasosErrorErpAsync(empresa);
			
		if (!errores.Any())
		{
			HayErroresErp = false;
			return;
		}
		
		// 🔷 OPTIMIZADO: Hacer consultas en paralelo para mejorar rendimiento
		var todosTraspasosTask = _traspasosService.ObtenerTraspasosAsync();
		var almacenesPermitidosTask = ObtenerAlmacenesPermitidosAsync();
		var paletsConUbicacionTask = _traspasosService.ObtenerPaletsConUbicacionAsync();
		
		await Task.WhenAll(todosTraspasosTask, almacenesPermitidosTask, paletsConUbicacionTask);
		
		var todosTraspasos = await todosTraspasosTask;
		var almacenesPermitidos = await almacenesPermitidosTask;
		var paletsConUbicacion = await paletsConUbicacionTask;
		
		// 🔷 OPTIMIZADO: Crear diccionario de traspasos por PaletId para búsqueda rápida
		var traspasosPorPaletId = todosTraspasos
			.Where(t => t.PaletId != Guid.Empty)
			.GroupBy(t => t.PaletId)
			.ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.FechaInicio).ToList());
		
		// 🔷 OPTIMIZADO: Crear diccionario de ubicaciones por PaletId
		var ubicacionesPorPaletId = paletsConUbicacion
			.ToDictionary(pt => pt.Id, pt => pt);
		
		var vistos = new HashSet<Guid>();
		
		foreach (var error in errores)
		{
			if (vistos.Contains(error.PaletId))
				continue;
			
			// Excluir palets de prueba con prefijo PAL25-000088
			if (error.CodigoPalet?.StartsWith("PAL25-000088", StringComparison.OrdinalIgnoreCase) == true)
				continue;
			
			// 🔷 OPTIMIZADO: Usar diccionario en lugar de Any() para búsqueda rápida
			if (traspasosPorPaletId.TryGetValue(error.PaletId, out var traspasosDelPalet))
			{
				var tieneIntentoPosterior = traspasosDelPalet.Any(t =>
					t.Id != error.TraspasoId &&
					t.FechaInicio >= error.FechaInicio &&
					!string.Equals(t.CodigoEstado, "ERROR_ERP", StringComparison.OrdinalIgnoreCase));
				
				if (tieneIntentoPosterior)
					continue;
			}
			
			// 🔷 OPTIMIZADO: Verificar almacén antes de cargar el palet completo
			string? almacenOrigen = error.AlmacenOrigen;
			if (string.IsNullOrEmpty(almacenOrigen) && 
				ubicacionesPorPaletId.TryGetValue(error.PaletId, out var paletConUbicacion))
			{
				almacenOrigen = paletConUbicacion.AlmacenOrigen;
			}
			
			// 🔒 APLICAR FILTRO DE SEGURIDAD: Verificar antes de cargar el palet
			if (!string.IsNullOrEmpty(almacenOrigen) && 
				!almacenesPermitidos.Contains(almacenOrigen))
				continue;
			
			// Solo cargar el palet si pasó los filtros anteriores
			var palet = await _paletService.ObtenerPaletPorIdAsync(error.PaletId);
			if (palet == null)
				continue;
			if (palet.Estado.Equals("Vaciado", StringComparison.OrdinalIgnoreCase) || palet.IsVaciado)
				continue;
			
			// Si llegamos aquí, hay al menos un error visible
			HayErroresErp = true;
			return; // Salir temprano, ya sabemos que hay errores visibles
		}
			
			// Si llegamos aquí, no hay errores visibles después de aplicar todos los filtros
			HayErroresErp = false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error comprobando palets con ERROR ERP: {ex.Message}");
			// 🔷 CORREGIDO: Forzar a false cuando hay error para evitar mostrar botón incorrectamente
			HayErroresErp = false;
		}
	}

		private async void OpenFiltros()
		{
			try
			{
				var empresa = SessionManager.EmpresaSeleccionada!.Value;
				var dlgVm = new PaletFilterDialogViewModel(_paletService);
				await dlgVm.InitializeAsync();
				
				// Actualizar usuarios disponibles con los palets actuales
				dlgVm.ActualizarUsuariosDisponibles(PaletsView.ToList());

				var dlg = new PaletFilterDialog
				{
					DataContext = dlgVm
				};
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
						 ?? Application.Current.MainWindow;
				if (owner != null && owner != dlg)
					dlg.Owner = owner;
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
					usuarioCierre: f.UsuarioCierreSeleccionado?.UsuarioId == 0 ? null : f.UsuarioCierreSeleccionado?.UsuarioId,
					almacen: f.Almacen,
					tipoUltimaActividad: f.TipoUltimaActividadFiltro,
					usuarioUltimaActividad: f.UsuarioUltimaActividadSeleccionado?.UsuarioId == 0 ? null : f.UsuarioUltimaActividadSeleccionado?.UsuarioId);

				// 🔒 FILTRO DE SEGURIDAD: Obtener almacenes permitidos del usuario
				var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();

				// Limpiar la lista actual
				PaletsView.Clear();

				// Obtener información de ubicación para los palets filtrados
				var paletsConUbicacion = await _traspasosService.ObtenerPaletsConUbicacionAsync();

				// Crear un diccionario para búsqueda rápida de información de ubicación
				var ubicacionPorPalet = paletsConUbicacion.ToDictionary(p => p.Id, p => p);

				// Agregar los palets filtrados con su información de ubicación Y filtro de seguridad
				foreach (var p in filtrados)
				{
					// Buscar información de ubicación si existe
					if (ubicacionPorPalet.TryGetValue(p.Id, out var paletConUbicacion))
					{
						p.AlmacenOrigen = paletConUbicacion.AlmacenOrigen;
						p.UbicacionOrigen = paletConUbicacion.UbicacionOrigen;
						p.FechaUltimoTraspaso = paletConUbicacion.FechaUltimoTraspaso;
						p.UsuarioUltimoTraspaso = paletConUbicacion.UsuarioUltimoTraspaso;
					}
					
					// 🔒 APLICAR FILTRO DE SEGURIDAD: Solo mostrar palets de almacenes permitidos
					// (después de obtener la información de ubicación)
					// Si el palet no tiene ubicación (recién creado), permitirlo si el usuario tiene acceso general
					bool puedeVerPalet = string.IsNullOrEmpty(p.AlmacenOrigen) || 
										almacenesPermitidos.Contains(p.AlmacenOrigen);
					
					if (puedeVerPalet)
					{
						PaletsView.Add(p);
					}
				}

				// Actualizar usuarios disponibles para los filtros
				ActualizarUsuariosDisponibles(PaletsView.ToList());
				
				Mensaje = $"Se encontraron {PaletsView.Count} palets con los filtros aplicados";
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al aplicar filtros";
			}
		}

		private async Task AbrirPaletCrearDialog()
		{
			try
			{
				var dlgVm = new PaletCrearDialogViewModel(_paletService);
				var dlg = new PaletCrearDialog { DataContext = dlgVm };
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
						 ?? Application.Current.MainWindow;
				if (owner != null && owner != dlg)
					dlg.Owner = owner;
				if (dlg.ShowDialog() == true && dlgVm.CreatedPalet != null)
				{
					// Refrescar la lista completa para obtener el palet con toda la información actualizada
					await LoadPaletsAsync();
					Mensaje = $"Palet {dlgVm.CreatedPalet.Codigo} creado correctamente";
				}
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al crear palet";
			}
		}

		private async void AbrirPaletLineas()
		{
			if (PaletSeleccionado is null) return;

			try
			{
				var dlgVm = new PaletLineasDialogViewModel(
					PaletSeleccionado.Id,
					PaletSeleccionado.Codigo,
					PaletSeleccionado.TipoPaletCodigo,
					PaletSeleccionado.Estado,
					_paletService,
					_stockService);

				var dlg = new PaletLineasDialog
				{
					DataContext = dlgVm
				};
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
						 ?? Application.Current.MainWindow;
				if (owner != null && owner != dlg)
					dlg.Owner = owner;
				dlg.ShowDialog();

				// Recargar las líneas después de cerrar el diálogo
				await LoadLineasPaletAsync();
				Mensaje = $"Líneas del palet {PaletSeleccionado.Codigo} actualizadas";
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al abrir líneas del palet";
			}
		}

		private bool PuedeAbrirPaletLineas()
		{
			return PaletSeleccionado != null;
		}

		private void SeleccionarPalet(PaletDto? palet)
		{
			if (palet != null)
			{
				PaletSeleccionado = palet;
				Mensaje = $"Palet {palet.Codigo} seleccionado";
			}
		}

		private async Task ToggleErroresAsync()
		{
			if (MostrandoErrores)
			{
				MostrandoErrores = false;
				await LoadPaletsAsync();
			}
			else
			{
				MostrandoPendientesVaciado = false;
				await LoadTraspasosErrorAsync();
				if (HayErroresErp)
				{
					MostrandoErrores = true;
				}
			}
		}

		private async Task TogglePendientesVaciadoAsync()
		{
			if (MostrandoPendientesVaciado)
			{
				MostrandoPendientesVaciado = false;
				await LoadPaletsAsync();
			}
			else
			{
				MostrandoErrores = false;
				await LoadPaletsPendientesVaciadoAsync();
				if (HayPaletsPendientesVaciado)
				{
					MostrandoPendientesVaciado = true;
				}
			}
		}

		private bool CanVaciarPaletPendiente(PaletDto? palet)
			=> palet?.EsPendienteVaciado == true;

		private async Task VaciarPaletPendienteAsync(PaletDto? palet)
		{
			if (palet == null || !palet.EsPendienteVaciado)
				return;

			var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
			if (usuarioId <= 0)
			{
				new WarningDialog("Operario no identificado", "No se ha encontrado el operario actual para registrar el vaciado.").ShowDialog();
				return;
			}

			var confirm = new ConfirmationDialog(
				"Vaciar palet",
				$"Se eliminarán las líneas definitivas del palet {palet.Codigo} y se marcará como vaciado.\n\n¿Quieres continuar?",
				"\uE74D");

			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				?? Application.Current.MainWindow;
			if (owner != null && owner != confirm)
				confirm.Owner = owner;

			if (confirm.ShowDialog() != true)
				return;

			var (exito, mensaje) = await _paletService.VaciarPaletPendienteAsync(palet.Id, usuarioId);
			if (!exito)
			{
				new WarningDialog("Error al vaciar", mensaje ?? "No se pudo vaciar el palet.").ShowDialog();
				return;
			}

			new WarningDialog("Palet vaciado", $"El palet {palet.Codigo} se ha marcado como vaciado.", "\uE73E").ShowDialog();

			ActualizarPaletComoVaciado(palet);

			if (MostrandoPendientesVaciado)
			{
				PaletsView.Remove(palet);
				if (!PaletsView.Any())
				{
					HayPaletsPendientesVaciado = false;
					MostrandoPendientesVaciado = false;
					Mensaje = "No quedan palets pendientes de vaciar.";
				}
				else
				{
					Mensaje = $"Palet {palet.Codigo} vaciado. Quedan {PaletsView.Count} pendientes.";
				}
			}
			else
			{
				palet.EsPendienteVaciado = false;
				palet.LineasPendientesVaciado.Clear();
				palet.MensajePendienteVaciado = null;
				Mensaje = $"Palet {palet.Codigo} vaciado correctamente.";
			}

			await ActualizarIndicadorPendientesAsync();
			VaciarPaletPendienteCommand.NotifyCanExecuteChanged();
			MostrarPaletsPendientesCommand.NotifyCanExecuteChanged();
			MostrarTraspasosErrorCommand.NotifyCanExecuteChanged();
		}

		private static void ActualizarPaletComoVaciado(PaletDto palet)
		{
			palet.Estado = "Vaciado";
			palet.IsVaciado = true;
			palet.FechaVaciado = DateTime.Now;
		}

		private async Task LoadPaletsPendientesVaciadoAsync()
		{
			if (!SessionManager.EmpresaSeleccionada.HasValue)
			{
				ErrorMessage = "Selecciona una empresa para consultar palets pendientes.";
				return;
			}

			try
			{
				ErrorMessage = null;
				Cargando = true;
				Mensaje = "Cargando palets pendientes de vaciar...";

				var empresa = SessionManager.EmpresaSeleccionada.Value;
				var pendientes = await _paletService.ObtenerPaletsPendientesVaciadoAsync(empresa);

				PaletsView.Clear();
				PaletSeleccionado = null;

				if (!pendientes.Any())
				{
					HayPaletsPendientesVaciado = false;
					Mensaje = "No hay palets pendientes de vaciar.";
					return;
				}

				foreach (var pendiente in pendientes)
				{
					var palet = await _paletService.ObtenerPaletPorIdAsync(pendiente.PaletId);
					if (palet == null)
						continue;

					if (palet.Estado.Equals("Vaciado", StringComparison.OrdinalIgnoreCase) || palet.IsVaciado)
						continue;

					palet.EsPendienteVaciado = true;
					palet.MensajePendienteVaciado = pendiente.Observacion ?? "Stock no encontrado en la ubicación registrada.";
					palet.LineasPendientesVaciado = pendiente.Lineas ?? new List<LineaPendienteVaciadoDto>();
					palet.TipoUltimaActividad ??= "PENDIENTE VACIADO";
					if (!palet.FechaUltimaActividad.HasValue)
						palet.FechaUltimaActividad = palet.FechaCierre ?? palet.FechaApertura;
					if (string.IsNullOrWhiteSpace(palet.UsuarioUltimaActividadNombre))
						palet.UsuarioUltimaActividadNombre = palet.UsuarioCierreNombre ?? palet.UsuarioAperturaNombre;
					palet.DescripcionUltimaActividad = palet.MensajePendienteVaciado;

					PaletsView.Add(palet);
				}

				HayPaletsPendientesVaciado = PaletsView.Any();
				Mensaje = HayPaletsPendientesVaciado
					? $"Se encontraron {PaletsView.Count} palets pendientes de vaciar."
					: "No hay palets pendientes de vaciar.";

				VaciarPaletPendienteCommand.NotifyCanExecuteChanged();
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al cargar palets pendientes de vaciar";
				HayPaletsPendientesVaciado = false;
			}
			finally
			{
				Cargando = false;
				MostrarPaletsPendientesCommand.NotifyCanExecuteChanged();
				VaciarPaletPendienteCommand.NotifyCanExecuteChanged();
			}
		}

		private async Task LoadTraspasosErrorAsync()
		{
			if (!SessionManager.EmpresaSeleccionada.HasValue)
			{
				ErrorMessage = "Selecciona una empresa para consultar traspasos.";
				return;
			}

			try
			{
				ErrorMessage = null;
				Cargando = true;
				Mensaje = "Cargando palets con ERROR ERP...";

				var empresa = SessionManager.EmpresaSeleccionada.Value;
				var errores = await _paletService.ObtenerTraspasosErrorErpAsync(empresa);
				var todosTraspasos = await _traspasosService.ObtenerTraspasosAsync();

				// Obtener información de ubicación para los palets
				var paletsConUbicacion = await _traspasosService.ObtenerPaletsConUbicacionAsync();
				
				// 🔒 FILTRO DE SEGURIDAD: Obtener almacenes permitidos del usuario
				var almacenesPermitidos = await ObtenerAlmacenesPermitidosAsync();

				PaletsView.Clear();
				PaletSeleccionado = null;

				if (!errores.Any())
				{
					HayErroresErp = false;
					Mensaje = "No hay palets con traspasos en ERROR ERP.";
					RelanzarTraspasoErrorCommand.NotifyCanExecuteChanged();
					return;
				}

				var vistos = new HashSet<Guid>();

				foreach (var error in errores)
				{
					if (vistos.Contains(error.PaletId))
						continue;

					// Excluir palets de prueba con prefijo PAL25-000088
					if (error.CodigoPalet?.StartsWith("PAL25-000088", StringComparison.OrdinalIgnoreCase) == true)
						continue;

					// Si existe un traspaso más reciente (con estado distinto a ERROR_ERP), el error ya fue relanzado/solucionado.
					var tieneIntentoPosterior = todosTraspasos.Any(t =>
						t.PaletId == error.PaletId &&
						t.Id != error.TraspasoId &&
						t.FechaInicio >= error.FechaInicio &&
						!string.Equals(t.CodigoEstado, "ERROR_ERP", StringComparison.OrdinalIgnoreCase));

					if (tieneIntentoPosterior)
						continue;

					var palet = await _paletService.ObtenerPaletPorIdAsync(error.PaletId);
					if (palet == null)
						continue;
					if (palet.Estado.Equals("Vaciado", StringComparison.OrdinalIgnoreCase) || palet.IsVaciado)
						continue;

					// Buscar información de ubicación si existe
					var paletConUbicacion = paletsConUbicacion.FirstOrDefault(pt => pt.Id == palet.Id);
					if (paletConUbicacion != null)
					{
						palet.AlmacenOrigen = paletConUbicacion.AlmacenOrigen;
						palet.UbicacionOrigen = paletConUbicacion.UbicacionOrigen;
						palet.FechaUltimoTraspaso = paletConUbicacion.FechaUltimoTraspaso;
						palet.UsuarioUltimoTraspaso = paletConUbicacion.UsuarioUltimoTraspaso;
					}

					// 🔒 APLICAR FILTRO DE SEGURIDAD: Solo mostrar palets de almacenes permitidos
					// (después de obtener la información de ubicación)
					// Si el palet no tiene ubicación (recién creado), permitirlo si el usuario tiene acceso general
					bool puedeVerPalet = string.IsNullOrEmpty(palet.AlmacenOrigen) || 
										almacenesPermitidos.Contains(palet.AlmacenOrigen);
					
					if (!puedeVerPalet)
						continue;

					palet.ErrorErpMensaje = error.EstadoErp ?? error.Comentario ?? "Error reportado por ERP.";
					palet.FechaUltimaActividad = error.FechaFinalizacion ?? error.FechaInicio;
					palet.TipoUltimaActividad = "ERROR ERP";
					palet.UsuarioUltimaActividadId = error.UsuarioFinalizacionId ?? error.UsuarioInicioId;
					palet.UsuarioUltimaActividadNombre = error.UsuarioFinalizacionNombre ?? error.UsuarioInicioNombre;
					palet.DescripcionUltimaActividad = error.EstadoErp ?? error.Comentario;
					palet.TraspasoErrorId = error.TraspasoId;

					PaletsView.Add(palet);
					vistos.Add(palet.Id);
				}

				Mensaje = $"Se encontraron {PaletsView.Count} palets con traspasos en ERROR ERP.";
				HayErroresErp = PaletsView.Any();
				ErrorMessage = null;
				RelanzarTraspasoErrorCommand.NotifyCanExecuteChanged();

				await ActualizarIndicadorPendientesAsync();
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al cargar traspasos en ERROR ERP";
			}
			finally
			{
				Cargando = false;
				MostrarTraspasosErrorCommand.NotifyCanExecuteChanged();
				MostrarPaletsPendientesCommand.NotifyCanExecuteChanged();
			}
		}

		[RelayCommand(CanExecute = nameof(CanRelanzarTraspasoError))]
		private async Task RelanzarTraspasoErrorAsync(PaletDto? palet)
		{
			if (palet?.TraspasoErrorId == null)
				return;

			var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
			if (usuarioId <= 0)
			{
				new WarningDialog("Usuario no válido", "No se encontró el operario actual para relanzar el traspaso.").ShowDialog();
				return;
			}

			var confirm = new ConfirmationDialog(
				"Relanzar traspaso",
				$"Se relanzará el traspaso del palet {palet.Codigo}.\n\n¿Quieres continuar?");
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				?? Application.Current.MainWindow;
			if (owner != null && owner != confirm)
				confirm.Owner = owner;
			if (confirm.ShowDialog() != true) return;

			var request = new RelanzarTraspasoRequest
			{
				UsuarioId = usuarioId,
				Comentario = string.IsNullOrWhiteSpace(palet.ErrorErpMensaje) ? "Relanzado desde escritorio" : palet.ErrorErpMensaje
			};

			var (exito, mensaje) = await _paletService.RelanzarTraspasoAsync(palet.TraspasoErrorId.Value, request);
			if (!exito)
			{
				new WarningDialog("Error al relanzar", mensaje ?? "No se pudo relanzar el traspaso.").ShowDialog();
				return;
			}

			new WarningDialog("Traspaso relanzado", "El traspaso se ha relanzado y volverá a procesarse.", "\uE930").ShowDialog();
			Mensaje = $"Traspaso del palet {palet.Codigo} relanzado correctamente.";
			await LoadTraspasosErrorAsync();
		}

		private bool CanRelanzarTraspasoError(PaletDto? palet)
			=> MostrandoErrores && palet?.TraspasoErrorId != null;

		private void CerrarContenido()
		{
			PaletSeleccionado = null;
			Mensaje = "Contenido cerrado";
		}

		private void VerPaletSeleccionado()
		{
			if (PaletSeleccionado != null)
			{
				Mensaje = $"Mostrando contenido del palet {PaletSeleccionado.Codigo}";
			}
		}

		private bool PuedeVerPalet()
		{
			return PaletSeleccionado != null;
		}

        private void ImprimirPaletSeleccionado()
        {
            if (PaletSeleccionado != null)
            {
                ImprimirPaletCommand.ExecuteAsync(null);
            }
        }

		private bool PuedeImprimirPalet()
		{
			return PaletSeleccionado != null;
		}

		private void EliminarLineaSeleccionada()
		{
			// TODO: Implementar eliminación de línea
			Mensaje = "Eliminar línea no implementado aún";
		}

		private bool PuedeEliminarLinea()
		{
			return LineaSeleccionada != null && PaletSeleccionado?.Estado == "Abierto";
		}

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
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                     ?? Application.Current.MainWindow
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

            // 🔷 Llama al servicio para cerrar, pasando destino, comentario, altura y peso
            var ok = await _paletService.CerrarPaletAsync(
                PaletSeleccionado.Id,
                SessionManager.UsuarioActual.operario,
                almacenOrigen,
                almacenDestino.CodigoAlmacen,
                ubicacionElegida.Ubicacion,
                dlg.VM.Comentario, // Comentario
                dlg.VM.Altura,     // Altura
                dlg.VM.Peso        // Peso
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
                $"¿Estás seguro de reabrir el palet {PaletSeleccionado.Codigo}?\n\nAl reabrir podrás añadir líneas al palet.\n\n¿Deseas continuar?",
                "\uE7BA"
            );
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
            if (owner != null && owner != confirm)
                confirm.Owner = owner;
            if (confirm.ShowDialog() != true) return;

            // Llama al servicio para reabrir
            var ok = await _paletService.ReabrirPaletAsync(PaletSeleccionado.Id, SessionManager.UsuarioActual.operario);
            if (!ok)
            {
                ErrorMessage = "No se pudo reabrir el palet.";
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

        [RelayCommand(CanExecute = nameof(CanImprimir))]
        private async Task ImprimirPaletAsync()
        {
            if (PaletSeleccionado is null) return;

            // Abrimos diálogo de impresión
            // usa el nombre preferido que tengas (sesión o BD). Si no, el primero.
            string? preNombre = SessionManager.PreferredPrinter
    ?? ImpresorasDisponibles.FirstOrDefault()?.Nombre;

            var dlgVm = new ConfirmarImpresionDialogViewModel(
                ImpresorasDisponibles,
                preNombre,
                _loginService ?? new LoginService()
            );

            var dlg = new ConfirmarImpresionDialog
            {
                DataContext = dlgVm,
                Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current.MainWindow
            };

            if (dlg.ShowDialog() != true) return;

            // ya está guardado en BD y en SessionManager por el propio diálogo
            var seleccionada = dlgVm.ImpresoraSeleccionada;

            try
            {
                var dto = new LogImpresionDto
                {
                    Usuario = SessionManager.NombreOperario,
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

        private async Task FinalizarTraspasoAsync()
        {
            if (PaletSeleccionado == null) return;

            try
            {
                // Buscar el traspaso pendiente para este palet
                var traspasos = await _traspasosService.ObtenerTraspasosAsync();
                var traspasoPendiente = traspasos
                    .FirstOrDefault(t => t.PaletId == PaletSeleccionado.Id && 
                                       t.CodigoEstado?.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) == true);

                if (traspasoPendiente == null)
                {
                    Mensaje = "No se encontró traspaso pendiente para este palet";
                    return;
                }

                var confirm = new ConfirmationDialog(
                    "Finalizar traspaso",
                    $"¿Estás seguro de finalizar el traspaso del palet {PaletSeleccionado.Codigo}?");
                if (confirm.ShowDialog() != true) return;

                var dto = new FinalizarTraspasoDto
                {
                    UbicacionDestino = traspasoPendiente.UbicacionDestino,
                    UsuarioFinalizacionId = SessionManager.UsuarioActual?.operario ?? 0,
                    FechaFinalizacion = DateTime.Now
                };

                await _traspasosService.FinalizarTraspasoAsync(traspasoPendiente.Id, dto);

                // Recargar el palet para actualizar su estado
                await LoadPaletsAsync();

                Mensaje = $"Traspaso del palet {PaletSeleccionado.Codigo} finalizado correctamente";
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                Mensaje = "Error al finalizar traspaso";
            }
        }

        private async Task TraspasarPaletAsync()
        {
            if (PaletSeleccionado == null) return;

            try
            {
                // Abrir el diálogo de traspaso de palets pasando el palet seleccionado
                var dlg = new TraspasoPaletDialog(PaletSeleccionado);
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != dlg)
                    dlg.Owner = owner;
                
                dlg.ShowDialog();

                // Recargar los palets después de cerrar el diálogo
                await LoadPaletsAsync();
                Mensaje = $"Gestión de traspaso completada para el palet {PaletSeleccionado.Codigo}";
                ErrorMessage = null;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                Mensaje = "Error al abrir diálogo de traspaso";
            }
        }

        private bool CanCerrar() => PaletSeleccionado?.Estado == "Abierto";
        private bool CanReabrir() => PaletSeleccionado?.Estado == "Cerrado";
        private bool CanImprimir() => PaletSeleccionado != null;

        public bool PuedeCerrarPalet => PaletSeleccionado?.Estado == "Abierto";
        public bool PuedeReabrirPalet => PaletSeleccionado?.Estado == "Cerrado";

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

		private async Task LoadImpresorasAsync()
		{
			
			// Si la aplicación se está cerrando, no cargar impresoras
			if (SessionManager.IsClosing)
			{
				return;
			}

			try
			{
				var impresoras = await _printService.ObtenerImpresorasAsync();
				ImpresorasDisponibles.Clear();
				foreach (var imp in impresoras)
					ImpresorasDisponibles.Add(imp);
			}
			catch (Exception ex)
			{
				// Solo mostrar error si la aplicación no se está cerrando
				if (!SessionManager.IsClosing)
				{
					ErrorMessage = ex.Message;
				}
			}
		}

        // Métodos parciales para notificar cambios
        partial void OnPaletSeleccionadoChanged(PaletDto? value)
        {
            AbrirPaletLineasCommand.NotifyCanExecuteChanged();
            // Los comandos CerrarPaletCommand, ReabrirPaletCommand e ImprimirPaletCommand se actualizan automáticamente por [RelayCommand]
            VerPaletSeleccionadoCommand.NotifyCanExecuteChanged();
            ImprimirPaletSeleccionadoCommand.NotifyCanExecuteChanged();

            OnPropertyChanged(nameof(PuedeCerrarPalet));
            OnPropertyChanged(nameof(PuedeReabrirPalet));

            _ = LoadLineasPaletAsync();
        }

		partial void OnLineaSeleccionadaChanged(LineaPaletDto? value)
		{
			EliminarLineaSeleccionadaCommand.NotifyCanExecuteChanged();
		}

		public void ActualizarUsuariosDisponibles(IEnumerable<PaletDto> palets)
		{
			// Este método se llama desde el diálogo de filtros
			// Los usuarios se actualizan en el PaletFilterDialogViewModel
		}

		// 🔒 MÉTODO DE SEGURIDAD: Obtener almacenes permitidos del usuario
		private async Task<List<string>> ObtenerAlmacenesPermitidosAsync()
		{
			try
			{
				var empresa = SessionManager.EmpresaSeleccionada!.Value;
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
				
				if (!permisos.Any())
				{
					permisos = await _stockService.ObtenerAlmacenesAsync(centro);
				}

				var almacenesAutorizados = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
				
				// Retornar solo los códigos de almacén permitidos
				return almacenesAutorizados.Select(a => a.CodigoAlmacen).ToList();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error obteniendo almacenes permitidos: {ex.Message}");
				// En caso de error, retornar lista vacía para máxima seguridad
				return new List<string>();
			}
		}

	}
}
