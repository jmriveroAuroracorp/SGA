using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.Services
{
	public class StockService : ApiService
	{
		/// <summary>
		/// Llama a GET /api/Stock/consulta-stock?codigoEmpresa=…&otros filtros
		/// usando el mismo HttpClient configurado en ApiService.
		/// </summary>
		public Task<string> ConsultaStockRawAsync(
			int codigoEmpresa,
			string codigoUbicacion = "",
			string codigoAlmacen = "",
			string codigoArticulo = "",
			string codigoCentro = "",
			string almacen = "",
			string partida = "")
		{
			var qs = $"?codigoEmpresa={codigoEmpresa}"
				   + $"&codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}"
				   + $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}"
				   + $"&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}"
				   + $"&codigoCentro={Uri.EscapeDataString(codigoCentro)}"
				   + $"&almacen={Uri.EscapeDataString(almacen)}"
				   + $"&partida={Uri.EscapeDataString(partida)}";

			return GetStringAsync($"Stock/consulta-stock{qs}");
		}

		/// <summary>
		/// GET /api/Almacen?codigoCentro=…
		/// Devuelve sólo la lista de códigos de almacén.
		/// </summary>
		public async Task<List<string>> ObtenerAlmacenesAsync(string codigoCentro)
		{
			var resp = await _httpClient.GetAsync($"Almacen?codigoCentro={Uri.EscapeDataString(codigoCentro)}");
			resp.EnsureSuccessStatusCode();
			// Deserializamos directamente a List<string>
			return JsonConvert.DeserializeObject<List<string>>(await resp.Content.ReadAsStringAsync())
				   ?? new List<string>();
		}
	}
}
