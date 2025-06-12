using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Services;
using SGA_Desktop.Views;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using System.Net.Http.Headers;


namespace SGA_Desktop.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{

		private readonly LoginService _login;

		public MainViewModel(LoginService loginService)
		{
			_login = loginService;
			// Cuando entras a la MainWindow, asignas aquí la empresa preferida:
			_ = InicializarEmpresaPreferidaAsync();


			// Aquí ves al arrancar qué tiene tu SessionManager
			MessageBox.Show(
				$"ID usuario: {SessionManager.UsuarioActual?.operario}\n" +
				$"EmpresaSeleccionada: {SessionManager.EmpresaSeleccionada}");

			_ = InitializeAsync();
		}

		private async Task InitializeAsync()
		{
			var idUsuario = SessionManager.UsuarioActual?.operario ?? 0;
			var (ok, idEmpresa) = await _login.ObtenerEmpresaPreferidaAsync(idUsuario);

			// Y aquí ves lo que devuelve el endpoint
			MessageBox.Show(
				$"Obtuve del endpoint Usuarios/{idUsuario} → " +
				$"ok={ok}, idEmpresa={idEmpresa}");

			// … resto de tu lógica
		}
		[RelayCommand]
		public void IrAConsultaStock()
		{
			NavigationStore.MainFrame.Navigate(new ConsultaStockView());
		}

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

		// MainViewModel.cs
		[RelayCommand]
		public void IrASeleccionEmpresa()
		{
			NavigationStore.MainFrame.Navigate(new EmpresaView());
		}


		[RelayCommand]
		public async Task CerrarSesion()
		{
			string idDispositivo = Environment.MachineName;
			string tipo = GetTipoSO();
			int idUsuario = SessionManager.UsuarioActual?.operario ?? 0;

			try
			{
				// 1. Registrar evento de logout ANTES de invalidar token
				using var http = new HttpClient();

				if (string.IsNullOrWhiteSpace(SessionManager.Token))
				{
					MessageBox.Show("Token no disponible.");
					return;
				}

				http.DefaultRequestHeaders.Authorization =
					new AuthenticationHeaderValue("Bearer", SessionManager.Token);

				var evento = new
				{
					fecha = DateTime.Now,
					idUsuario = idUsuario,
					tipo = "LOGOUT",
					origen = "MainWindow",
					descripcion = "Sesión Cerrada",
					detalle = $"El usuario cerró sesión.",
					idDispositivo = idDispositivo
				};

				var json = JsonConvert.SerializeObject(evento);
				var content = new StringContent(json, Encoding.UTF8, "application/json");
				var response = await http.PostAsync("http://10.0.0.175:5234/api/LogEvento/crear", content);

				if (!response.IsSuccessStatusCode)
				{
					var errorText = await response.Content.ReadAsStringAsync();
					MessageBox.Show($"Error al registrar evento de logout:\n{response.StatusCode}\n{errorText}");
				}

				// 2. Desactivar el dispositivo (esto borra el token)
				var loginService = new LoginService();
				await loginService.DesactivarDispositivoAsync(idDispositivo, tipo, idUsuario);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error al cerrar sesión: {ex.Message}");
				return;
			}

			// 3. Limpiar sesión y cerrar
			SessionManager.UsuarioActual = null;
			Application.Current.Shutdown();
		}

		private string GetTipoSO()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";
			return "Desconocido";
		}

		private async Task InicializarEmpresaPreferidaAsync()
		{
			var idUsuario = SessionManager.UsuarioActual?.operario ?? 0;
			var (ok, idEmpresa) = await _login.ObtenerEmpresaPreferidaAsync(idUsuario);
			if (ok && idEmpresa.GetValueOrDefault() > 0)
			{
				// Esto guarda la empresa real de la BD en SessionManager
				SessionManager.SetEmpresa(idEmpresa.Value);
			}
		}
	}
}
