using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Services;
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

			CurrentHeader = string.Empty;
			EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
			nombreOperario = SessionManager.NombreOperario;
			SessionManager.EmpresaCambiada += (_, __) =>
			{
				EmpresaNombre = SessionManager.EmpresaSeleccionadaNombre;
			};

			_ = InicializarEmpresaPreferidaAsync();
		}

		[ObservableProperty]
		private string currentHeader;

		[ObservableProperty]
		private string empresaNombre;

		[ObservableProperty]
		private string nombreOperario = "";

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
			NavigationStore.Navigate("OrdenTrabajo");
			CurrentHeader = "ÓRDENES DE TRABAJO";
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
		public void IrATraspasosStock()
		{
			NavigationStore.Navigate("TraspasosStock");
			CurrentHeader = "TRASPASOS DE STOCK";
		}

		[RelayCommand]
		private async Task CerrarSesion()
		{
			var dialog = new ConfirmationDialog(
				"Confirmar salida",
				"¿Estás seguro de que quieres salir de la aplicación?");
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			if (owner != null && owner != dialog)
				dialog.Owner = owner;
			if (dialog.ShowDialog() != true)
				return;

			var logoutTask = Task.Run(async () =>
			{
				try
				{
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


	}
}