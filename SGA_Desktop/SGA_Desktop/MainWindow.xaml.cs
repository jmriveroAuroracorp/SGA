using System.Windows;
using SGA_Desktop.Helpers;
using System.Windows.Input;
using System.Threading.Tasks;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;

namespace SGA_Desktop
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			// ← Aquí asignas el Frame definido en el XAML
			NavigationStore.MainFrame = MainFrame;

			// Luego ajustas el DataContext de tu VM
			DataContext = new ViewModels.MainViewModel(new Services.LoginService());

			// Navega inmediatamente a la vista de bienvenida
			NavigationStore.Navigate("Welcome");
			
			// Pre-inicializar vistas críticas en segundo plano
			_ = PreInicializarVistasCriticasAsync();
			
			// Suscribirse al evento Closing para marcar el flag cuando se cierre la ventana
			this.Closing += MainWindow_Closing;

			// Configurar notificaciones globales
			ConfigurarNotificacionesGlobales();
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private async void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			// Marcar que la aplicación se está cerrando INMEDIATAMENTE
			SessionManager.IsClosing = true;
			
			// Obtener el ViewModel y ejecutar el comando de cerrar sesión
			if (DataContext is ViewModels.MainViewModel mainViewModel)
			{
				await mainViewModel.CerrarSesionCommand.ExecuteAsync(null);
			}
			else
			{
				// Fallback si no hay ViewModel disponible
				Close();
			}
		}

	private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}


	private void LogoBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		// Navegar a la pantalla de bienvenida al hacer clic en el logo
		if (DataContext is ViewModels.MainViewModel mainViewModel)
		{
			mainViewModel.IrAWelcomeCommand.Execute(null);
		}
	}

	private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
	{
		// Marcar que la aplicación se está cerrando cuando se cierre la ventana principal
		SessionManager.IsClosing = true;
		
		// Desconectar SignalR antes de cerrar la aplicación
		try
		{
			await NotificacionesManager.DesconectarAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error al desconectar SignalR durante cierre: {ex.Message}");
			// No bloquear el cierre por errores de SignalR
		}
	}

	/// <summary>
	/// Pre-inicializa vistas críticas en segundo plano para mejorar la experiencia de usuario
	/// </summary>
	private async Task PreInicializarVistasCriticasAsync()
	{
		try
		{
			// Esperar un poco para que la aplicación termine de cargar
			await Task.Delay(2000);

			System.Diagnostics.Debug.WriteLine("Iniciando pre-carga de vistas críticas...");

			// Pre-cargar vistas críticas EN EL HILO PRINCIPAL DE LA UI
			// Usar Dispatcher para asegurar que se ejecute en el hilo correcto
			await Dispatcher.InvokeAsync(() =>
			{
				try
				{
					// Pre-cargar la vista de órdenes de traspaso (la más crítica)
					System.Diagnostics.Debug.WriteLine("Pre-cargando OrdenTraspasoView...");
					NavigationStore.PreloadPage("OrdenTraspaso");
					
					// Pre-cargar otras vistas importantes
					System.Diagnostics.Debug.WriteLine("Pre-cargando vistas adicionales...");
					NavigationStore.PreloadPage("ConsultaStock");
					NavigationStore.PreloadPage("Traspasos");
					
					System.Diagnostics.Debug.WriteLine("Vistas pre-cargadas exitosamente");
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Error pre-cargando vistas: {ex.Message}");
				}
			});

			System.Diagnostics.Debug.WriteLine("Pre-carga de vistas críticas completada");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error en pre-carga de vistas: {ex.Message}");
		}
	}

		/// <summary>
		/// Configura las notificaciones globales para mostrar popups desde cualquier vista
		/// </summary>
		private void ConfigurarNotificacionesGlobales()
		{
			try
			{
				// Suscribirse a las notificaciones del NotificacionesManager
				if (NotificacionesManager.Instancia != null)
				{
					NotificacionesManager.Instancia.NotificacionRecibida += OnNotificacionGlobalRecibida;
					NotificacionesManager.Instancia.EstadoConexionCambiado += OnEstadoConexionCambiado;
				}
				else
				{
					// Si el servicio no está disponible, intentar de nuevo en unos segundos
					_ = Task.Run(async () =>
					{
						await Task.Delay(3000);
						
						// Verificar si la aplicación se está cerrando antes de continuar
						if (SessionManager.IsClosing)
							return;
							
						Application.Current.Dispatcher.Invoke(() =>
						{
							ConfigurarNotificacionesGlobales();
						});
					});
				}
			}
			catch (Exception ex)
			{
			}
		}

	/// <summary>
	/// Maneja las notificaciones recibidas globalmente
	/// </summary>
	private void OnNotificacionGlobalRecibida(object? sender, NotificacionUsuarioEventArgs e)
	{
		try
		{
			System.Diagnostics.Debug.WriteLine($"🔔 MainWindow: Notificación global recibida: {e.Titulo} - {e.Mensaje}");

			// Convertir notificación SignalR a NotificacionDto
			var notificacionDto = ConvertirANotificacionDto(e);
			
			// Agregar al NotificacionesManager (esto actualizará automáticamente el contador)
			NotificacionesManager.AgregarNotificacion(notificacionDto);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"❌ MainWindow: Error al procesar notificación global: {ex.Message}");
		}
	}

	/// <summary>
	/// Convierte una notificación SignalR a NotificacionDto
	/// </summary>
	private NotificacionDto ConvertirANotificacionDto(NotificacionUsuarioEventArgs e)
	{
		var notificacion = new NotificacionDto
		{
			Titulo = e.Titulo ?? "Notificación",
			Mensaje = e.Mensaje ?? "Sin mensaje",
			Tipo = e.TipoPopup ?? "info",
			UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
			FechaCreacion = DateTime.Now
		};

		// Extraer información adicional del mensaje si es posible
		ExtraerInformacionAdicional(notificacion, e.Mensaje);

		return notificacion;
	}

	/// <summary>
	/// Extrae información adicional del mensaje de notificación
	/// </summary>
	private void ExtraerInformacionAdicional(NotificacionDto notificacion, string? mensaje)
	{
		if (string.IsNullOrEmpty(mensaje)) return;

		try
		{
			// Detectar tipo de traspaso y código
			if (mensaje.Contains("palet"))
			{
				notificacion.TipoTraspaso = "PALET";
				// Extraer código de palet del mensaje
				var match = System.Text.RegularExpressions.Regex.Match(mensaje, @"palet\s+([A-Z0-9-]+)");
				if (match.Success)
				{
					notificacion.CodigoPalet = match.Groups[1].Value;
				}
			}
			else if (mensaje.Contains("artículo"))
			{
				notificacion.TipoTraspaso = "ARTICULO";
				// Extraer código de artículo del mensaje
				var match = System.Text.RegularExpressions.Regex.Match(mensaje, @"artículo\s+([A-Z0-9-]+)");
				if (match.Success)
				{
					notificacion.CodigoArticulo = match.Groups[1].Value;
				}
			}

                    // Extraer información de ubicación origen y destino del mensaje
                    // Formato: ALM01-UBIC001 → ALM02-UBIC002 o solo UBICACION → DESTINO o ALM → DESTINO
                    var ubicacionMatch = System.Text.RegularExpressions.Regex.Match(mensaje, @"([A-Z0-9-]+)\s*→\s*([A-Z0-9-]+)");
                    if (ubicacionMatch.Success)
                    {
                        var origenCompleto = ubicacionMatch.Groups[1].Value.Trim();
                        var destinoCompleto = ubicacionMatch.Groups[2].Value.Trim();
                        
                        // Procesar origen
                        var origenParts = origenCompleto.Split('-');
                        if (origenParts.Length == 2)
                        {
                            notificacion.AlmacenOrigen = origenParts[0];
                            notificacion.UbicacionOrigen = origenParts[1];
                        }
                        else
                        {
                            // Solo ubicación o solo almacén
                            if (origenCompleto.Length <= 3) // Probablemente almacén (PR, ALM, etc.)
                            {
                                notificacion.AlmacenOrigen = origenCompleto;
                            }
                            else // Probablemente ubicación
                            {
                                notificacion.UbicacionOrigen = origenCompleto;
                            }
                        }
                        
                        // Procesar destino
                        var destinoParts = destinoCompleto.Split('-');
                        if (destinoParts.Length == 2)
                        {
                            notificacion.AlmacenDestino = destinoParts[0];
                            notificacion.UbicacionDestino = destinoParts[1];
                        }
                        else if (destinoCompleto.Trim().Equals("Sin ubicar", StringComparison.OrdinalIgnoreCase))
                        {
                            // Caso especial: "Sin ubicar"
                            notificacion.UbicacionDestino = "Sin ubicar";
                        }
                        else
                        {
                            // Solo ubicación o solo almacén
                            if (destinoCompleto.Length <= 3) // Probablemente almacén
                            {
                                notificacion.AlmacenDestino = destinoCompleto;
                            }
                            else // Probablemente ubicación
                            {
                                notificacion.UbicacionDestino = destinoCompleto;
                            }
                        }
                    }

			// Extraer cantidad del mensaje
			// Formato: Cantidad: 10.50 UD
			var cantidadMatch = System.Text.RegularExpressions.Regex.Match(mensaje, @"Cantidad:\s*([\d.]+)\s*([A-Z]+)");
			if (cantidadMatch.Success)
			{
				if (decimal.TryParse(cantidadMatch.Groups[1].Value, out decimal cantidad))
				{
					notificacion.Cantidad = cantidad;
					notificacion.Unidad = cantidadMatch.Groups[2].Value;
				}
			}

			// Extraer descripción del artículo del mensaje
			// Formato: Artículo: Descripción del artículo
			var articuloMatch = System.Text.RegularExpressions.Regex.Match(mensaje, @"Artículo:\s*(.+?)(?:\s*$|$)");
			if (articuloMatch.Success)
			{
				notificacion.DescripcionArticulo = articuloMatch.Groups[1].Value.Trim();
			}

			// Detectar estado
			if (mensaje.Contains("completado"))
			{
				notificacion.EstadoActual = "COMPLETADO";
			}
			else if (mensaje.Contains("procesándose"))
			{
				notificacion.EstadoActual = "PENDIENTE_ERP";
			}
			else if (mensaje.Contains("falló"))
			{
				notificacion.EstadoActual = "ERROR_ERP";
			}

			System.Diagnostics.Debug.WriteLine($"✅ Información extraída: Origen={notificacion.AlmacenOrigen}-{notificacion.UbicacionOrigen}, Destino={notificacion.AlmacenDestino}-{notificacion.UbicacionDestino}, Cantidad={notificacion.Cantidad}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"⚠️ Error al extraer información adicional: {ex.Message}");
		}
	}

	/// <summary>
	/// Enriquece la información de la notificación con datos del traspaso
	/// </summary>
	private async Task EnriquecerInformacionArticulo(NotificacionDto notificacion)
	{
		try
		{
			if (string.IsNullOrEmpty(notificacion.CodigoArticulo)) return;

			// Obtener información básica del artículo desde la API
			var stockService = new Services.StockService();
			var empresa = SessionManager.EmpresaSeleccionada ?? 0;
			
			// Obtener stock del artículo solo para la descripción
			var stock = await stockService.ObtenerPorArticuloAsync(empresa, notificacion.CodigoArticulo);
			
			if (stock?.Any() == true)
			{
				var primerStock = stock.First();
				
				// Solo agregar la descripción del artículo
				notificacion.DescripcionArticulo = primerStock.DescripcionArticulo;

				System.Diagnostics.Debug.WriteLine($"✅ Descripción obtenida para {notificacion.CodigoArticulo}: {notificacion.DescripcionArticulo}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"⚠️ Error al enriquecer información del artículo {notificacion.CodigoArticulo}: {ex.Message}");
		}
	}

	/// <summary>
	/// Maneja cambios en el estado de conexión SignalR
	/// </summary>
	private void OnEstadoConexionCambiado(object? sender, string estado)
	{
		System.Diagnostics.Debug.WriteLine($"🔌 MainWindow: SignalR Estado: {estado}");
	}
}
}
