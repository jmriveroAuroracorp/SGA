using Newtonsoft.Json;
using SGA_Desktop.Models;
using System.Net.Http.Json;
using System.Net.Http;
using System.Windows;

namespace SGA_Desktop.Services
{
	public class UbicacionesService : ApiService
	{
		public UbicacionesService() : base() { }

		/// <summary>
		/// GET /api/Ubicaciones?codigoEmpresa=X&codigoAlmacen=Y
		/// </summary>
		public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesDetalladasAsync(
			short codigoEmpresa,
			string codigoAlmacen)
		{
			try
			{
				// Llama al endpoint GET que ya tienes:
				var url = $"Ubicaciones?codigoEmpresa={codigoEmpresa}"
						+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
				var lista = await _httpClient
					.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);
				return lista ?? new List<UbicacionDetalladaDto>();
			}
			catch (HttpRequestException ex)
			{
				MessageBox.Show(
					$"Error al obtener ubicaciones:\n{ex.Message}",
					"Error HTTP",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return new List<UbicacionDetalladaDto>();
			}
			catch (NotSupportedException)
			{
				MessageBox.Show(
					"Respuesta no está en formato JSON.",
					"Error de formato",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return new List<UbicacionDetalladaDto>();
			}
		}
	}
}

