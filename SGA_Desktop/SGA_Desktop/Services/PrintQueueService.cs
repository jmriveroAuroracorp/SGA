using System.Net.Http.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using System.Threading.Tasks;
using System.Net.Http;
using System.Windows;

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

			dto.Usuario = SessionManager.Operario.ToString();
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
					MessageBox.Show(
						$"Error de red al llamar al servicio:\n{ex.Message}",
						"Error HTTP",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
				return;
			}

			string body = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				// Solo mostrar el diálogo si la aplicación no se está cerrando
				if (!SessionManager.IsClosing)
				{
					MessageBox.Show(
						$"La API respondió con {(int)response.StatusCode} {response.ReasonPhrase}:\n{body}",
						"Error en API",
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}
				return;
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
