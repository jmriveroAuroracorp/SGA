using System.Net.Http.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System.Threading.Tasks;
using System.Net.Http;
using System.Windows;
using SGA_Desktop.Dialog;
using System.Linq;

namespace SGA_Desktop.Services
{
	public class PrintQueueService : ApiService
	{
		/// <summary>
		/// POST /api/Impresion/log
		/// Inserta un registro en log_impresiones.
		/// </summary>
		public async Task InsertarRegistroImpresionAsync(LogImpresionDto dto)
		{
			// Si la aplicación se está cerrando, no intentar insertar logs
			if (SessionManager.IsClosing)
				return;

			dto.Usuario = SessionManager.NombreOperario;
			dto.Dispositivo = Environment.MachineName;
			dto.Copias ??= 1;

			HttpResponseMessage response;
			try
			{
				response = await _httpClient.PostAsJsonAsync("Impresion/log", dto);
			}
			catch (HttpRequestException ex)
			{
				// Solo mostrar el diálogo si la aplicación no se está cerrando
				if (!SessionManager.IsClosing)
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						var errorDialog = new WarningDialog(
							"Error HTTP",
							$"Error de red al llamar al servicio:\n{ex.Message}",
							"\uE783" // Icono de error
						)
						{
							Owner = Application.Current.Windows.OfType<Window>()
								.FirstOrDefault(w => w.IsActive)
								?? Application.Current.MainWindow
						};
						errorDialog.ShowDialog();
					});
				}
				// Lanzar excepción para que el código que llama sepa que falló
				throw new HttpRequestException($"Error de red al llamar al servicio: {ex.Message}", ex);
			}

			string body = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				string mensajeError = body;
				
				// Limpiar comillas si el mensaje viene entre comillas
				if (mensajeError.StartsWith("\"") && mensajeError.EndsWith("\""))
				{
					mensajeError = mensajeError.Substring(1, mensajeError.Length - 2);
				}
				
				// Solo mostrar el diálogo si la aplicación no se está cerrando
				if (!SessionManager.IsClosing)
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						string mensaje = mensajeError;
						string titulo = "Error en API";
						string icono = "\uE783"; // Icono de error por defecto
						
						// Si es un BadRequest (400), mostrar el mensaje del backend directamente
						if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
						{
							titulo = "Error de validación";
							icono = "\uE814"; // Icono de advertencia
						}
						else
						{
							mensaje = $"La API respondió con {(int)response.StatusCode} {response.ReasonPhrase}:\n{body}";
						}
						
						var errorDialog = new WarningDialog(titulo, mensaje, icono)
						{
							Owner = Application.Current.Windows.OfType<Window>()
								.FirstOrDefault(w => w.IsActive)
								?? Application.Current.MainWindow
						};
						errorDialog.ShowDialog();
					});
				}
				
				// Lanzar excepción para que el código que llama sepa que falló
				throw new HttpRequestException($"Error en API: {(int)response.StatusCode} {response.ReasonPhrase}. {mensajeError}");
			}

			// Si llega aquí, todo ok - MessageBox eliminado
		}

		/// <summary>
		/// GET /api/Impresion/impresoras
		/// Obtiene la lista de impresoras disponibles desde la API.
		/// </summary>
		public async Task<List<ImpresoraDto>> ObtenerImpresorasAsync()
		{
			// Si la aplicación se está cerrando, no intentar cargar impresoras
			if (SessionManager.IsClosing)
			{
				return new List<ImpresoraDto>();
			}

			
			try
			{
				var lista = await _httpClient
					.GetFromJsonAsync<List<ImpresoraDto>>("Impresion/impresoras");

				return lista ?? new List<ImpresoraDto>();
			}
			catch (HttpRequestException ex)
			{
				
				// Solo mostrar el diálogo si la aplicación no se está cerrando
				if (!SessionManager.IsClosing)
				{
					MessageBox.Show(
						$"Error de red al obtener impresoras:\n{ex.Message}",
						"Error HTTP",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
				return new List<ImpresoraDto>();
			}
			catch (NotSupportedException)
			{
				
				// Solo mostrar el diálogo si la aplicación no se está cerrando
				if (!SessionManager.IsClosing)
				{
					MessageBox.Show(
						"El contenido de la respuesta no está en formato JSON.",
						"Error de formato",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
				return new List<ImpresoraDto>();
			}
		

		}
	}
}
