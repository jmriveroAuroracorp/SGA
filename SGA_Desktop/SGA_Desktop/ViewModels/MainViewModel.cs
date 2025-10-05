using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Services;
using SGA_Desktop.Models;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using SGA_Desktop.Dialog;
using System.Windows.Input;
using System;
using System.Threading;
using System.Linq;

namespace SGA_Desktop.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		private readonly LoginService _login;

		public MainViewModel(LoginService loginService)
		{
			_login = loginService;

			CurrentHeader = "BIENVENIDO";
			EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
			nombreOperario = SessionManager.NombreOperario;
			SessionManager.EmpresaCambiada += (_, __) =>
			{
				EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
			};

			// Suscribirse a solicitudes de cambio de header
			NavigationStore.HeaderChangeRequested += (sender, newHeader) =>
			{
				CurrentHeader = newHeader;
			};

			_ = InicializarEmpresaPreferidaAsync();
			
			// Configurar sistema de notificaciones con campanita
			ConfigurarSistemaNotificaciones();
		}

		[ObservableProperty]
		private string currentHeader;

		[ObservableProperty]
		private string empresaNombre;

		[ObservableProperty]
		private string nombreOperario = "";

		[ObservableProperty]
		private int contadorNotificaciones = 0;

		[ObservableProperty]
		private int contadorNotificacionesPositivas = 0;

		[ObservableProperty]
		private int contadorNotificacionesNegativas = 0;

		private async Task InicializarEmpresaPreferidaAsync()
		{
			var idUsuario = SessionManager.UsuarioActual?.operario ?? 0;
			var (ok, idEmpresa) = await _login.ObtenerEmpresaPreferidaAsync(idUsuario);

			if (ok && idEmpresa.GetValueOrDefault() > 0)
			{
				SessionManager.SetEmpresa(idEmpresa.Value);
				EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
			}
		}

		[RelayCommand]
		public void IrAWelcome()
		{
			NavigationStore.Navigate("Welcome");
			CurrentHeader = "BIENVENIDO";
		}

		[RelayCommand]
		public void IrAConsultaStock()
		{
			NavigationStore.Navigate("ConsultaStock");
			CurrentHeader = "CONSULTA DE STOCK";
		}

		[RelayCommand]
		public void IrAUbicaciones()
		{
			NavigationStore.Navigate("Ubicaciones");
			CurrentHeader = "GESTIÓN DE UBICACIONES";
		}

		[RelayCommand]
		public void IrATraspasos()
		{
			NavigationStore.Navigate("Traspasos");
			CurrentHeader = "TRASPASOS";
		}

		[RelayCommand]
		public void IrAInventario()
		{
			NavigationStore.Navigate("Inventario");
			CurrentHeader = "INVENTARIO";
		}

		[RelayCommand]
		public void IrAOrdenTrabajo()
		{
			NavigationStore.Navigate("OrdenTraspaso");
			CurrentHeader = "ÓRDENES DE TRASPASO";
		}

		[RelayCommand]
		public void IrAControlesRotativos()
		{
			NavigationStore.Navigate("ControlesRotativos");
			CurrentHeader = "CONTROLES ROTATIVOS";
		}



		[RelayCommand]
		public void IrAEtiquetas()
		{
			NavigationStore.Navigate("Etiquetas");
			CurrentHeader = "IMPRESIÓN DE ETIQUETAS";
		}

	[RelayCommand]
	public void IrASeleccionEmpresa()
	{
		NavigationStore.Navigate("SeleccionEmpresa");
		CurrentHeader = "SELECCIÓN DE EMPRESA";
	}

	[RelayCommand]
	public void IrAConfiguracionOperarios()
	{
		NavigationStore.Navigate("ConfiguracionOperarios");
		CurrentHeader = "CONFIGURACIÓN DE OPERARIOS";
	}

	[RelayCommand]
	public void IrATraspasosStock()
	{
		NavigationStore.Navigate("TraspasosStock");
		CurrentHeader = "TRASPASOS DE STOCK";
	}

		[RelayCommand]
		private async Task CerrarSesion()
		{
			// Marcar que la aplicación se está cerrando INMEDIATAMENTE
			SessionManager.IsClosing = true;
			
			var dialog = new ConfirmationDialog(
				"Confirmar salida",
				"¿Estás seguro de que quieres salir de la aplicación?");
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dialog)
				dialog.Owner = owner;
			if (dialog.ShowDialog() != true)
			{
				// Si el usuario cancela, resetear el flag
				SessionManager.IsClosing = false;
				return;
			}
			
			// Si el usuario confirma, mantener el flag en true

			var logoutTask = Task.Run(async () =>
			{
				try
				{
					// Verificar si la aplicación se está cerrando antes de continuar
					if (SessionManager.IsClosing)
						return;

					if (!string.IsNullOrWhiteSpace(SessionManager.Token))
					{
						using var http = new HttpClient();
						http.Timeout = TimeSpan.FromSeconds(2); // Timeout de 2 segundos
						http.DefaultRequestHeaders.Authorization =
							new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionManager.Token);

						var evento = new
						{
							fecha = DateTime.Now,
							idUsuario = SessionManager.UsuarioActual?.operario ?? 0,
							tipo = "LOGOUT",
							origen = "MainWindow",
							descripcion = "Sesión Cerrada",
							detalle = $"El usuario cerró sesión.",
							idDispositivo = Environment.MachineName
						};

						var json = Newtonsoft.Json.JsonConvert.SerializeObject(evento);
						var content = new StringContent(json, Encoding.UTF8, "application/json");
						await http.PostAsync("http://10.0.0.175:5234/api/LogEvento/crear", content);

						// Desactivar dispositivo en tu servicio
						var login = new Services.LoginService();
						await login.DesactivarDispositivoAsync(
							evento.idDispositivo!, GetTipoSO(), evento.idUsuario);
					}
				}
				catch
				{
					// Ignora errores, no bloquea el cierre
				}
			});

			// Espera hasta 2 segundos a que termine el logout, pero no bloquees más
			await Task.WhenAny(logoutTask, Task.Delay(2000));

			System.Windows.Application.Current.Shutdown();
		}
		private string GetTipoSO()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
			return "Desconocido";
		}

		private void OnEmpresaCambiada(object? sender, EventArgs e)
		{
			// Actualizo la propiedad ligada al header
			EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
		}

		#region Sistema de Notificaciones con Campanita

		/// <summary>
		/// Configura el sistema de notificaciones con campanita
		/// </summary>
		private void ConfigurarSistemaNotificaciones()
		{
			try
			{
				// Suscribirse a eventos del NotificacionesManager
				NotificacionesManager.OnNotificacionAgregada += OnNotificacionRecibida;
				NotificacionesManager.OnContadorCambiado += OnContadorCambiado;

				// Inicializar contadores
				ContadorNotificaciones = NotificacionesManager.ContadorPendientes;
				
				// Calcular contadores separados
				var notificaciones = NotificacionesManager.ObtenerNotificacionesPendientes();
				ContadorNotificacionesPositivas = notificaciones.Count(n => n.EsPositiva);
				ContadorNotificacionesNegativas = notificaciones.Count(n => n.EsNegativa);

				System.Diagnostics.Debug.WriteLine("✅ Sistema de notificaciones con campanita configurado");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"❌ Error al configurar sistema de notificaciones: {ex.Message}");
			}
		}

		/// <summary>
		/// Maneja las notificaciones recibidas de SignalR (convertidas a NotificacionDto)
		/// </summary>
		private void OnNotificacionRecibida(NotificacionDto notificacion)
		{
			try
			{
				System.Diagnostics.Debug.WriteLine($"🔔 Notificación recibida: {notificacion.Titulo} - {notificacion.Mensaje}");
				
				// La notificación ya está agregada al NotificacionesManager
				// El contador se actualizará automáticamente via OnContadorCambiado
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"❌ Error al procesar notificación: {ex.Message}");
			}
		}

		/// <summary>
		/// Maneja cambios en el contador de notificaciones
		/// </summary>
		private void OnContadorCambiado(int nuevoContador)
		{
			ContadorNotificaciones = nuevoContador;
			
			// Calcular contadores separados por tipo
			var notificaciones = NotificacionesManager.ObtenerNotificacionesPendientes();
			ContadorNotificacionesPositivas = notificaciones.Count(n => n.EsPositiva);
			ContadorNotificacionesNegativas = notificaciones.Count(n => n.EsNegativa);
			
			System.Diagnostics.Debug.WriteLine($"🔔 Contador total: {nuevoContador}, Positivas: {ContadorNotificacionesPositivas}, Negativas: {ContadorNotificacionesNegativas}");
		}

		/// <summary>
		/// Comando para abrir el modal de notificaciones
		/// </summary>
		[RelayCommand]
		private void AbrirNotificaciones()
		{
			try
			{
				System.Diagnostics.Debug.WriteLine("🔔 Abriendo modal de notificaciones");
				
				var modal = new NotificacionesModal();
				var viewModel = new NotificacionesModalViewModel();
				modal.DataContext = viewModel;
				
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
						   ?? Application.Current.MainWindow;
				if (owner != null && owner != modal)
					modal.Owner = owner;
				
				modal.ShowDialog();
				
				System.Diagnostics.Debug.WriteLine("✅ Modal de notificaciones mostrado");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"❌ Error al abrir notificaciones: {ex.Message}");
			}
		}

		#endregion

	}
}