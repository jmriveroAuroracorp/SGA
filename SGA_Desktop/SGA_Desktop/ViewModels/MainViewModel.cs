using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Services;
using SGA_Desktop.Views;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace SGA_Desktop.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		[RelayCommand]
		public void IrATraspasos()
		{
			NavigationStore.MainFrame.Navigate(new TraspasosView());
		}

		[RelayCommand]
		public void IrAInventario()
		{
			// Implementar en el futuro
		}
		[RelayCommand]
		public async Task CerrarSesion()
		{
			var loginService = new LoginService();
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

			int idUsuario = SessionManager.UsuarioActual?.operario ?? 0;

			try
			{
				await loginService.DesactivarDispositivoAsync(idDispositivo, tipo, idUsuario);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error al cerrar sesión: {ex.Message}");
			}

			// Limpieza de sesión local
			SessionManager.UsuarioActual = null;

			// Salir completamente de la aplicación
			Application.Current.Shutdown();

		}
	}
}
