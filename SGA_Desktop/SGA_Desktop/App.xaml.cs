using System.Configuration;
using System.Data;
using System.Windows;
using System;
using Microsoft.Win32;
using SGA_Desktop.Services;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SGA_Desktop
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			AppDomain.CurrentDomain.UnhandledException += (s, e) =>
			{
				MessageBox.Show(e.ExceptionObject.ToString(), "Error global");
			};
			this.DispatcherUnhandledException += (s, e) =>
			{
				MessageBox.Show(e.Exception.ToString(), "Error de UI");
				e.Handled = true;
			};

			// Suscribirse a eventos del sistema para cerrar la aplicación por seguridad
			SystemEvents.PowerModeChanged += OnPowerModeChanged;
			SystemEvents.SessionSwitch += OnSessionSwitch;

			// NotificacionesManager se inicializará después del login
		}

		/// <summary>
		/// Detecta cuando el PC se suspende
		/// </summary>
		private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (e.Mode == PowerModes.Suspend)
			{
				// Ejecutar logout y cerrar la aplicación cuando se suspende el PC
				Dispatcher.Invoke(async () =>
				{
					await EjecutarLogoutSeguro();
					Current?.Shutdown();
				});
			}
		}

		/// <summary>
		/// Detecta cuando se bloquea la sesión o se cambia de usuario
		/// </summary>
		private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
		{
			if (e.Reason == SessionSwitchReason.SessionLock ||
				e.Reason == SessionSwitchReason.SessionLogoff ||
				e.Reason == SessionSwitchReason.ConsoleDisconnect ||
				e.Reason == SessionSwitchReason.RemoteDisconnect)
			{
				// Ejecutar logout y cerrar la aplicación por seguridad
				Dispatcher.Invoke(async () =>
				{
					await EjecutarLogoutSeguro();
					Current?.Shutdown();
				});
			}
		}

		/// <summary>
		/// Ejecuta el logout de forma segura sin bloquear la aplicación
		/// </summary>
		private async Task EjecutarLogoutSeguro()
		{
			// Marcar que la aplicación se está cerrando
			Helpers.SessionManager.IsClosing = true;
			
			try
			{
				if (!string.IsNullOrWhiteSpace(Helpers.SessionManager.Token))
				{
					// Ejecutar logout en background sin bloquear
					_ = Task.Run(async () =>
					{
						try
						{
							using var http = new HttpClient();
							http.Timeout = TimeSpan.FromSeconds(2);
							http.DefaultRequestHeaders.Authorization =
								new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Helpers.SessionManager.Token);

							var evento = new
							{
								fecha = DateTime.Now,
								idUsuario = Helpers.SessionManager.UsuarioActual?.operario ?? 0,
								tipo = "LOGOUT",
								origen = "Sistema",
								descripcion = "Sesión Cerrada por Suspensión/Bloqueo",
								detalle = "La aplicación se cerró automáticamente por suspensión del PC o bloqueo de sesión",
								idDispositivo = Environment.MachineName
							};

							var json = Newtonsoft.Json.JsonConvert.SerializeObject(evento);
							var content = new StringContent(json, Encoding.UTF8, "application/json");
							await http.PostAsync("http://10.0.0.175:5234/api/LogEvento/crear", content);

							// NO desactivar dispositivo durante suspensión para evitar conflictos con HttpClient
							// El dispositivo se desactivará automáticamente por timeout en el servidor
						}
						catch
						{
							// Ignora errores, no bloquea el cierre
						}
					});
				}
			}
			catch
			{
				// Ignora errores, no bloquea el cierre
			}
		}

		private string GetTipoSO()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
			return "Desconocido";
		}

		protected override async void OnExit(ExitEventArgs e)
		{
			try
			{
				// Marcar que la aplicación se está cerrando
				Helpers.SessionManager.IsClosing = true;
				
				// Desconectar servicio de notificaciones
				await NotificacionesManager.DesconectarAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error al desconectar notificaciones: {ex.Message}");
			}
			
			// Desuscribirse de los eventos al cerrar
			SystemEvents.PowerModeChanged -= OnPowerModeChanged;
			SystemEvents.SessionSwitch -= OnSessionSwitch;
			base.OnExit(e);
		}
	}

}
