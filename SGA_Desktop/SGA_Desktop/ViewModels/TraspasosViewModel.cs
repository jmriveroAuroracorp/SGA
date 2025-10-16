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
        public IAsyncRelayCommand CrearPaletCommand { get; }
        public IRelayCommand AbrirPaletLineasCommand { get; }
        public IRelayCommand<PaletDto> SeleccionarPaletCommand { get; }
        public IRelayCommand CerrarContenidoCommand { get; }
        public IRelayCommand VerPaletSeleccionadoCommand { get; }
        public IRelayCommand ImprimirPaletSeleccionadoCommand { get; }
        public IRelayCommand EliminarLineaSeleccionadaCommand { get; }
        public IAsyncRelayCommand FinalizarTraspasoCommand { get; }
        public IAsyncRelayCommand TraspasarPaletCommand { get; }
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

		private async Task LoadPaletsAsync()
		{
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
			}
			catch (Exception ex)
			{
				ErrorMessage = ex.Message;
				Mensaje = "Error al cargar palets";
			}
			finally
			{
				Cargando = false;
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
					almacen: f.Almacen);

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
