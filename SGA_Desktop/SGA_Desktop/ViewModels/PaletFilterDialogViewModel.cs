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
using System.Windows.Data;
using System.Collections.Generic;
using System.ComponentModel;
using SGA_Desktop.Helpers;

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
		// Colecciones separadas para cada ComboBox
		public ObservableCollection<UsuarioConNombre> UsuariosDisponibles { get; }
			= new ObservableCollection<UsuarioConNombre>();
		public ObservableCollection<UsuarioConNombre> UsuariosAperturaDisponibles { get; }
			= new ObservableCollection<UsuarioConNombre>();
		public ObservableCollection<UsuarioConNombre> UsuariosCierreDisponibles { get; }
			= new ObservableCollection<UsuarioConNombre>();
		[ObservableProperty] private UsuarioConNombre? _usuarioAperturaSeleccionado;
		[ObservableProperty] private UsuarioConNombre? _usuarioCierreSeleccionado;
		
		// Propiedades para filtrado inteligente de usuarios
		[ObservableProperty] private string _filtroUsuarioApertura = "";
		[ObservableProperty] private string _filtroUsuarioCierre = "";
		[ObservableProperty] private bool _isDropDownOpenUsuarioApertura = false;
		[ObservableProperty] private bool _isDropDownOpenUsuarioCierre = false;
		public ICollectionView UsuariosAperturaView { get; private set; }
		public ICollectionView UsuariosCierreView { get; private set; }

		// ▶️ Otros filtros
		[ObservableProperty] private string? _codigo;
		[ObservableProperty] private string? _mensajeValidacionCodigo;
		[ObservableProperty] private DateTime? _fechaApertura;
		[ObservableProperty] private DateTime? _fechaCierre;
		[ObservableProperty] private DateTime? _fechaDesde;
		[ObservableProperty] private DateTime? _fechaHasta;
		[ObservableProperty] private int? _usuarioApertura;
		[ObservableProperty] private int? _usuarioCierre;
		[ObservableProperty] private string? _almacen;
		[ObservableProperty] private string _filtroAlmacenes = "";
		[ObservableProperty] private bool _isDropDownOpenAlmacenes = false;
		[ObservableProperty] private AlmacenDto? _almacenSeleccionado;
		public ObservableCollection<AlmacenDto> AlmacenesDisponibles { get; } = new();
		public ICollectionView AlmacenesView { get; private set; }

		// ▶️ Comando para "Aplicar"
		public IRelayCommand AplicarFiltrosCommand { get; }
		
		// ▶️ Comandos para manejo del dropdown de almacenes
		public IRelayCommand AbrirDropDownAlmacenesCommand { get; }
		public IRelayCommand CerrarDropDownAlmacenesCommand { get; }
		public IRelayCommand LimpiarSeleccionAlmacenesCommand { get; }
		
		// ▶️ Comandos para manejo del dropdown de usuarios
		public IRelayCommand AbrirDropDownUsuarioAperturaCommand { get; }
		public IRelayCommand CerrarDropDownUsuarioAperturaCommand { get; }
		public IRelayCommand LimpiarSeleccionUsuarioAperturaCommand { get; }
		public IRelayCommand AbrirDropDownUsuarioCierreCommand { get; }
		public IRelayCommand CerrarDropDownUsuarioCierreCommand { get; }
		public IRelayCommand LimpiarSeleccionUsuarioCierreCommand { get; }

		public PaletFilterDialogViewModel(PaletService paletService)
		{
			_paletService = paletService;

			AplicarFiltrosCommand = new RelayCommand(() =>
			{
				// Validar código antes de aplicar filtros
				if (!string.IsNullOrWhiteSpace(Codigo) && Codigo.Length < 3)
				{
					var warningDialog = new WarningDialog(
						"Código muy corto", 
						"El código debe tener al menos 3 caracteres para realizar la búsqueda."
					);
					ShowCenteredDialog(warningDialog);
					return;
				}
				
				var dlg = Application.Current.Windows
					.OfType<PaletFilterDialog>()
					.FirstOrDefault();
				if (dlg != null)
					dlg.DialogResult = true;
			});
			
			// Inicializar comandos para dropdown de almacenes
			AbrirDropDownAlmacenesCommand = new RelayCommand(() =>
			{
				FiltroAlmacenes = "";
				IsDropDownOpenAlmacenes = true;
			});
			
			CerrarDropDownAlmacenesCommand = new RelayCommand(() =>
			{
				IsDropDownOpenAlmacenes = false;
			});
			
			LimpiarSeleccionAlmacenesCommand = new RelayCommand(() =>
			{
				// No necesitamos limpiar selección aquí, solo actualizar el filtro
			});
			
			// Inicializar comandos para dropdown de usuario apertura
			AbrirDropDownUsuarioAperturaCommand = new RelayCommand(() =>
			{
				FiltroUsuarioApertura = "";
				IsDropDownOpenUsuarioApertura = true;
			});
			
			CerrarDropDownUsuarioAperturaCommand = new RelayCommand(() =>
			{
				IsDropDownOpenUsuarioApertura = false;
			});
			
			LimpiarSeleccionUsuarioAperturaCommand = new RelayCommand(() =>
			{
				// No necesitamos limpiar selección aquí, solo actualizar el filtro
			});
			
			// Inicializar comandos para dropdown de usuario cierre
			AbrirDropDownUsuarioCierreCommand = new RelayCommand(() =>
			{
				FiltroUsuarioCierre = "";
				IsDropDownOpenUsuarioCierre = true;
			});
			
			CerrarDropDownUsuarioCierreCommand = new RelayCommand(() =>
			{
				IsDropDownOpenUsuarioCierre = false;
			});
			
			LimpiarSeleccionUsuarioCierreCommand = new RelayCommand(() =>
			{
				// No necesitamos limpiar selección aquí, solo actualizar el filtro
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

		// 3) Carga Almacenes desde API
		await CargarAlmacenesAsync();
		
		// 4) Carga TODOS los usuarios del sistema (no solo los de palets filtrados)
		await CargarUsuariosCompletosAsync();
		}

	/// <summary>
	/// Carga TODOS los usuarios del sistema que tienen permisos específicos de traspasos
	/// (no los de conteos, sino los que pueden hacer traspasos)
	/// </summary>
	private async Task CargarUsuariosCompletosAsync()
	{
		try
		{
			// Usar el servicio específico para operarios con permisos de traspasos
			var loginService = new LoginService();
			var operarios = await loginService.ObtenerOperariosConAccesoTraspasosAsync();
			
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				// Limpiar todas las colecciones
				UsuariosDisponibles.Clear();
				UsuariosAperturaDisponibles.Clear();
				UsuariosCierreDisponibles.Clear();
				
				// Crear el elemento "sin filtro"
				var sinFiltro = new UsuarioConNombre
				{
					UsuarioId = 0,
					NombreOperario = "-- Todos los usuarios --"
				};
				
				// Agregar a todas las colecciones
				UsuariosDisponibles.Add(sinFiltro);
				UsuariosAperturaDisponibles.Add(sinFiltro);
				UsuariosCierreDisponibles.Add(sinFiltro);
				
				// Agregar todos los operarios con permisos de traspasos a todas las colecciones
				foreach (var operario in operarios.OrderBy(o => o.NombreOperario))
				{
					var usuario = new UsuarioConNombre
					{
						UsuarioId = operario.Operario,
						NombreOperario = operario.NombreOperario ?? $"Usuario {operario.Operario}"
					};
					
					UsuariosDisponibles.Add(usuario);
					UsuariosAperturaDisponibles.Add(usuario);
					UsuariosCierreDisponibles.Add(usuario);
				}
				
				System.Diagnostics.Debug.WriteLine($"🔷 Operarios con permisos de traspasos cargados: {operarios.Count}");
				
				// Inicializar vistas filtrables de usuarios DESPUÉS de cargar los datos
				UsuariosAperturaView = System.Windows.Data.CollectionViewSource.GetDefaultView(UsuariosAperturaDisponibles);
				UsuariosAperturaView.Filter = FiltraUsuariosApertura;
				OnPropertyChanged(nameof(UsuariosAperturaView));
				
				UsuariosCierreView = System.Windows.Data.CollectionViewSource.GetDefaultView(UsuariosCierreDisponibles);
				UsuariosCierreView.Filter = FiltraUsuariosCierre;
				OnPropertyChanged(nameof(UsuariosCierreView));
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error al cargar operarios con permisos de traspasos: {ex.Message}");
		}
	}

	public void ActualizarUsuariosDisponibles(IEnumerable<PaletDto> palets)
		{
			// Este método ya no es necesario porque ahora cargamos TODOS los usuarios del sistema
			// en lugar de solo los de los palets filtrados actualmente.
			// Los usuarios se cargan una sola vez en CargarUsuariosCompletosAsync()
			
			System.Diagnostics.Debug.WriteLine($"🔷 ActualizarUsuariosDisponibles llamado con {palets.Count()} palets, pero ya no es necesario sobrescribir la lista");
			
			// Solo refrescar las vistas filtrables para asegurar que estén actualizadas
			UsuariosAperturaView?.Refresh();
			UsuariosCierreView?.Refresh();
		}

		private async Task CargarAlmacenesAsync()
		{
			try
			{
				var empresa = SessionManager.EmpresaSeleccionada!.Value;
				var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
				var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
				
				if (!permisos.Any())
				{
					var stockService = new StockService();
					permisos = await stockService.ObtenerAlmacenesAsync(centro);
				}
				
				var almacenes = await new StockService().ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
				
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					AlmacenesDisponibles.Clear();
					foreach (var a in almacenes)
						AlmacenesDisponibles.Add(a);
					
					// Inicializar la vista filtrable
					AlmacenesView = System.Windows.Data.CollectionViewSource.GetDefaultView(AlmacenesDisponibles);
					AlmacenesView.Filter = FiltraAlmacenes;
					OnPropertyChanged(nameof(AlmacenesView));
					
					// Las vistas de usuarios se inicializarán después de cargar los usuarios
				});
			}
			catch (Exception ex)
			{
				// Manejar error si es necesario
				System.Diagnostics.Debug.WriteLine($"Error al cargar almacenes: {ex.Message}");
			}
		}

		// Método para filtrado de almacenes
		private bool FiltraAlmacenes(object obj)
		{
			if (obj is not AlmacenDto almacen) return false;
			if (string.IsNullOrEmpty(FiltroAlmacenes)) return true;
			
			return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
				.IndexOf(almacen.DescripcionCombo, FiltroAlmacenes, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
		}
		
		// Método para filtrado de usuarios apertura
		private bool FiltraUsuariosApertura(object obj)
		{
			if (obj is not UsuarioConNombre usuario) return false;
			if (string.IsNullOrEmpty(FiltroUsuarioApertura)) return true;
			
			return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
				.IndexOf(usuario.NombreOperario, FiltroUsuarioApertura, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
		}
		
		// Método para filtrado de usuarios cierre
		private bool FiltraUsuariosCierre(object obj)
		{
			if (obj is not UsuarioConNombre usuario) return false;
			if (string.IsNullOrEmpty(FiltroUsuarioCierre)) return true;
			
			return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
				.IndexOf(usuario.NombreOperario, FiltroUsuarioCierre, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
		}
		
		// Método para manejar cambios en el filtro de almacenes
		partial void OnFiltroAlmacenesChanged(string value)
		{
			AlmacenesView?.Refresh();
		}
		
		// Método para manejar cambios en el filtro de usuarios apertura
		partial void OnFiltroUsuarioAperturaChanged(string value)
		{
			UsuariosAperturaView?.Refresh();
		}
		
		// Método para manejar cambios en el filtro de usuarios cierre
		partial void OnFiltroUsuarioCierreChanged(string value)
		{
			UsuariosCierreView?.Refresh();
		}
		
		// Método para manejar cambios en el almacén seleccionado
		partial void OnAlmacenSeleccionadoChanged(AlmacenDto value)
		{
			Almacen = value?.CodigoAlmacen;
		}
		
		// Método para manejar cambios en el usuario apertura seleccionado
		partial void OnUsuarioAperturaSeleccionadoChanged(UsuarioConNombre value)
		{
			UsuarioApertura = value?.UsuarioId;
			IsDropDownOpenUsuarioApertura = false;
		}
		
		// Método para manejar cambios en el usuario cierre seleccionado
		partial void OnUsuarioCierreSeleccionadoChanged(UsuarioConNombre value)
		{
			UsuarioCierre = value?.UsuarioId;
			IsDropDownOpenUsuarioCierre = false;
		}
		
		// Método para validar el código cuando cambie
		partial void OnCodigoChanged(string value)
		{
			if (!string.IsNullOrWhiteSpace(value) && value.Length < 3)
			{
				MensajeValidacionCodigo = "⚠️ El código debe tener al menos 3 caracteres";
			}
			else
			{
				MensajeValidacionCodigo = null;
			}
		}

		// Método para centrar diálogos
		private void ShowCenteredDialog(WarningDialog dialog)
		{
			// Configurar el owner para centrar el diálogo
			var mainWindow = Application.Current.Windows.OfType<Window>()
				.FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
			
			if (mainWindow != null && mainWindow != dialog)
			{
				dialog.Owner = mainWindow;
				dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			else
			{
				// Si no hay ventana principal, centrar en pantalla
				dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}
				
			dialog.ShowDialog();
		}

	}
}
