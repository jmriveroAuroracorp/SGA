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
		private async Task CerrarSesion()
		{
			// 1) Muestra tu diálogo personalizado
			var dialog = new ConfirmationDialog(
				"Confirmar salida",
				"¿Estás seguro de que quieres salir de la aplicación?")
			{
				Owner = Application.Current.MainWindow
			};
			if (dialog.ShowDialog() != true)
				return;  // si pulsa “No”, salimos sin cerrar

			// 2) Intentos de logout en segundo plano (no bloquea la salida)
			try
			{
				if (!string.IsNullOrWhiteSpace(SessionManager.Token))
				{
					using var http = new HttpClient();
					http.DefaultRequestHeaders.Authorization =
						new AuthenticationHeaderValue("Bearer", SessionManager.Token);

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

					var json = JsonConvert.SerializeObject(evento);
					var content = new StringContent(json, Encoding.UTF8, "application/json");
					await http.PostAsync("http://10.0.0.175:5234/api/LogEvento/crear", content);

					// Desactivar dispositivo en tu servicio
					await _login.DesactivarDispositivoAsync(
						evento.idDispositivo!, GetTipoSO(), evento.idUsuario);
				}
			}
			catch
			{
				// ignoramos errores en logout, vamos a cerrar de todas formas
			}

			// 3) Cierra la aplicación
			Application.Current.Shutdown();
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