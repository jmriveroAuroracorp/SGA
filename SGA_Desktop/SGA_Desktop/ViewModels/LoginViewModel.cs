using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using System.Windows;
using System.Runtime.InteropServices;

namespace SGA_Desktop.ViewModels
{
	public partial class LoginViewModel : ObservableObject
	{
		[ObservableProperty]
		private string usuario;

		[ObservableProperty]
		private string contraseña;

		[ObservableProperty]
		private string idDispositivo;

		[RelayCommand]
		public async Task IniciarSesion()
		{
			if (!int.TryParse(Usuario, out int operario))
			{
				MessageBox.Show("El campo usuario debe ser numérico.");
				return;
			}

			if (string.IsNullOrWhiteSpace(Contraseña))
			{
				MessageBox.Show("Introduce la contraseña.");
				return;
			}

			string idDispositivo = Environment.MachineName;
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

			// Intentar login primero
			var respuesta = await loginService.LoginAsync(new LoginRequest
			{
				operario = operario,
				contraseña = Contraseña,
				idDispositivo = idDispositivo
			});
			if (respuesta != null)
			{
				SessionManager.UsuarioActual = respuesta;

				// Detectar tipo de SO
				string tipoDispositivo;
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
					tipoDispositivo = "Windows";
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					tipoDispositivo = "Linux";
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					tipoDispositivo = "macOS";
				else
					tipoDispositivo = "Desconocido";

				// Registrar dispositivo (si no se ha hecho antes)
				try
				{
					await loginService.RegistrarDispositivoAsync(new Dispositivo
					{
						Id = idDispositivo,
						Tipo = tipoDispositivo
					});
				}
				catch { /* Ignorar errores si ya está registrado */ }

				// Registrar evento de login
				try
				{
					await loginService.RegistrarLogEventoAsync(new LogEvento
					{
						fecha = DateTime.Now,
						idUsuario = operario, // Asegúrate de que LoginResponse incluya Id
						tipo = "LOGIN",
						origen = "PantallaLogin",
						descripcion = "Inicio de sesión correcto",
						detalle = $"El usuario accedió desde dispositivo {tipoDispositivo.ToLower()}",
						idDispositivo = idDispositivo
					});
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Error registrando log de evento: {ex.Message}");
				}

				// Mostrar ventana principal
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
		}
	}
}