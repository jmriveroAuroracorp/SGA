using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;
using System.Linq;
using System;
using SGA_Desktop.Dialog;
using System.Net.Http;

namespace SGA_Desktop.ViewModels
{
	public partial class LoginViewModel : ObservableObject
	{
	[ObservableProperty]
	private string usuario = string.Empty;

	[ObservableProperty]
	private string contraseña = string.Empty;

		[ObservableProperty]
		private string idDispositivo;

		[RelayCommand]
		public async Task IniciarSesion()
		{
			// 0) Validación usuario numérico
			if (!int.TryParse(Usuario, out int operario))
			{
				MostrarAdvertencia("Login", "El campo usuario debe ser numérico.", "\uE814");
				Usuario = string.Empty;
				Contraseña = string.Empty;
				SetFocusUsuario();
				return;
			}

			// 1) Validación contraseña
			if (string.IsNullOrWhiteSpace(Contraseña))
			{
				MostrarAdvertencia("Login", "Introduce la contraseña.", "\uE814");
				Contraseña = string.Empty;
				SetFocusUsuario();
				return;
			}

			// 🔧 Crear ID único de dispositivo: {MachineName}_{Usuario}
			string idDispositivo = $"{Environment.MachineName}_{operario}";
			string tipo;
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				tipo = "Windows";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				tipo = "Linux";
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				tipo = "macOS";
			else
				tipo = "Desconocido";

			var loginService = new LoginService();

			try
			{
				var respuesta = await loginService.LoginAsync(new LoginRequest
				{
					operario = operario,
					contraseña = Contraseña,
					idDispositivo = idDispositivo,
					tipoDispositivo = tipo
				});

				if (respuesta == null)
				{
					// ⚠️ Caso 1: API respondió pero usuario/pass incorrectos
					MostrarAdvertencia("Login", "Usuario o contraseña incorrectos.", "\uE814");
					Contraseña = string.Empty;
					SetFocusUsuario();
					return;
				}

				// ✅ Caso 2: Login OK
				SessionManager.UsuarioActual = respuesta;

				// Inicializar notificaciones SignalR después del login exitoso
				_ = Task.Run(async () =>
				{
					try
					{
						// Verificar si la aplicación se está cerrando antes de continuar
						if (SessionManager.IsClosing)
							return;
							
						await NotificacionesManager.InicializarAsync();
					}
					catch (Exception ex)
					{
					}
				});

				try
				{
					await loginService.RegistrarLogEventoAsync(new LogEvento
					{
						Fecha = DateTime.Now,
						IdUsuario = operario,
						Tipo = "LOGIN",
						Origen = "PantallaLogin",
						Descripcion = "Inicio de sesión correcto",
						Detalle = $"El usuario accedió desde dispositivo {tipo.ToLower()}",
						IdDispositivo = idDispositivo
					});
				}
				catch (Exception ex)
				{
					MostrarAdvertencia("Login", $"Error registrando log de evento: {ex.Message}", "\uE814");
				}

				Application.Current.Dispatcher.Invoke(() =>
				{
					var main = new MainWindow();
					main.Show();
					foreach (Window window in Application.Current.Windows)
					{
						if (window is Login) window.Close();
					}
				});
			}
			catch (TaskCanceledException)
			{
				// ⏱️ Timeout
				MostrarAdvertencia("Servidor no responde", "El servidor tardó demasiado en responder. Inténtalo de nuevo.", "\uE814");
				Contraseña = string.Empty;
				SetFocusUsuario();
			}
			catch (HttpRequestException ex)
			{
				// 🌐 Error de conexión
				MostrarAdvertencia("Error de conexión", $"No se pudo conectar con el servidor: {ex.Message}", "\uE814");
				Contraseña = string.Empty;
				SetFocusUsuario();
			}
			catch (Exception ex)
			{
				// 🚨 Error inesperado
				MostrarAdvertencia("Error inesperado", $"Se produjo un error: {ex.Message}", "\uE814");
				Usuario = string.Empty;
				Contraseña = string.Empty;
				SetFocusUsuario();
			}
		}

		private void MostrarAdvertencia(string titulo, string mensaje, string icono)
		{
			var advertencia = new WarningDialog(titulo, mensaje, icono);
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
						 ?? Application.Current.MainWindow;
			if (owner != null && owner != advertencia)
				advertencia.Owner = owner;
			advertencia.ShowDialog();
		}

		private void SetFocusUsuario()
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				var loginWin = Application.Current.Windows.OfType<Login>().FirstOrDefault();
				if (loginWin != null)
				{
					var usuarioBox = loginWin.FindName("UsuarioTextBox") as System.Windows.Controls.TextBox;
					usuarioBox?.Focus();
					usuarioBox?.SelectAll();
				}
			});
		}
	}
}
