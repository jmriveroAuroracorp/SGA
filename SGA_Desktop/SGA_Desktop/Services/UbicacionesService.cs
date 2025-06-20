using Newtonsoft.Json;
using SGA_Desktop.Models;
using System.Net.Http.Json;
using System.Net.Http;
using System.Windows;
using System.Text;

namespace SGA_Desktop.Services
{
	public class UbicacionesService : ApiService
	{
		public UbicacionesService() : base() { }

		///// <summary>
		///// GET /api/Ubicaciones?codigoEmpresa=X&codigoAlmacen=Y
		///// </summary>
		//public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesDetalladasAsync(
		//	short codigoEmpresa,
		//	string codigoAlmacen)
		//{
		//	try
		//	{
		//		// Llama al endpoint GET que ya tienes:
		//		var url = $"Ubicaciones?codigoEmpresa={codigoEmpresa}"
		//				+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
		//		var lista = await _httpClient
		//			.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);
		//		return lista ?? new List<UbicacionDetalladaDto>();
		//	}
		//	catch (HttpRequestException ex)
		//	{
		//		MessageBox.Show(
		//			$"Error al obtener ubicaciones:\n{ex.Message}",
		//			"Error HTTP",
		//			MessageBoxButton.OK,
		//			MessageBoxImage.Error);
		//		return new List<UbicacionDetalladaDto>();
		//	}
		//	catch (NotSupportedException)
		//	{
		//		MessageBox.Show(
		//			"Respuesta no está en formato JSON.",
		//			"Error de formato",
		//			MessageBoxButton.OK,
		//			MessageBoxImage.Error);
		//		return new List<UbicacionDetalladaDto>();
		//	}
		//}
		// 1) Si en algún otro sitio usas UbicacionDto (solo código+empresa+ubicación)
		public async Task<List<UbicacionDto>> ObtenerUbicacionesAsync(
			string codigoAlmacen,
			short codigoEmpresa,
			bool soloConStock = false)
		{
			var url = new StringBuilder($"Almacen/Ubicaciones?codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}")
				.Append($"&codigoEmpresa={codigoEmpresa}");
			if (soloConStock) url.Append("&soloConStock=true");

			var resp = await _httpClient.GetAsync(url.ToString());
			resp.EnsureSuccessStatusCode();
			return JsonConvert
				.DeserializeObject<List<UbicacionDto>>(await resp.Content.ReadAsStringAsync())
				?? new List<UbicacionDto>();
		}

		// 2) Para el detalle completo, que devuelve UbicacionDetalladaDto
		public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesDetalladasAsync(
			short codigoEmpresa,
			string codigoAlmacen)
		{
			// Este endpoint está en otro controller: GET /api/Ubicaciones?codigoEmpresa=X&codigoAlmacen=Y
			var url = $"Ubicaciones?codigoEmpresa={codigoEmpresa}"
					+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
			var lista = await _httpClient
				.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);
			return lista ?? new List<UbicacionDetalladaDto>();
		}

	}
}

