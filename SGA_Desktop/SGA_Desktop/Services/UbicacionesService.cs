using Newtonsoft.Json;
using SGA_Desktop.Models;
using System.Net.Http.Json;
using System.Net.Http;
using System.Windows;
using System.Text;
using System.Diagnostics;

namespace SGA_Desktop.Services
{
	public class UbicacionesService : ApiService
	{
		public UbicacionesService() : base() { }

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

		// 2.2) Nuevo: carga solo lo “básico” sin alérgenos ni riesgo
		public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesBasicoAsync(
	short codigoEmpresa,
	string codigoAlmacen)
		{
			// ¡ojo al nombre!
			var url = $"Ubicaciones/basica"
					+ $"?codigoEmpresa={codigoEmpresa}"
					+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";

			var lista = await _httpClient
				.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);
			return lista ?? new List<UbicacionDetalladaDto>();
		}

		/// <summary>
		/// GET api/ubicaciones/alergenos/presentes
		/// </summary>
		/// <summary>GET /api/ubicaciones/alergenos/presentes?codigoEmpresa=X&codigoAlmacen=Y&ubicacion=Z</summary>
		public async Task<List<AlergenoDto>> ObtenerAlergenosPresentesAsync(
			short codigoEmpresa, string codigoAlmacen, string ubicacion)
		{
			var url = $"Ubicaciones/alergenos/presentes"
					+ $"?codigoEmpresa={codigoEmpresa}"
					+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}"
					+ $"&ubicacion={Uri.EscapeDataString(ubicacion)}";

			return await _httpClient.GetFromJsonAsync<List<AlergenoDto>>(url)
				   ?? new List<AlergenoDto>();
		}

		/// <summary>GET /api/ubicaciones/alergenos/permitidos?codigoEmpresa=X&codigoAlmacen=Y&ubicacion=Z</summary>
		public async Task<List<AlergenoDto>> ObtenerAlergenosPermitidosAsync(
			short codigoEmpresa, string codigoAlmacen, string ubicacion)
		{
			var url = $"Ubicaciones/alergenos/permitidos"
					+ $"?codigoEmpresa={codigoEmpresa}"
					+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}"
					+ $"&ubicacion={Uri.EscapeDataString(ubicacion)}";

			return await _httpClient.GetFromJsonAsync<List<AlergenoDto>>(url)
				   ?? new List<AlergenoDto>();
		}

		public async Task<bool> CrearUbicacionDetalladaAsync(CrearUbicacionDetalladaDto dto)
		{
			var resp = await _httpClient.PostAsJsonAsync(
				"Ubicaciones", dto);
			return resp.IsSuccessStatusCode;
		}

		// PUT (actualizar)
		public async Task<(bool Success, string? ErrorMessage)> ActualizarUbicacionDetalladaAsync(CrearUbicacionDetalladaDto dto)
		{
			var url = $"Ubicaciones/{dto.CodigoEmpresa}/{Uri.EscapeDataString(dto.CodigoAlmacen)}/{Uri.EscapeDataString(dto.CodigoUbicacion)}";
			var resp = await _httpClient.PutAsJsonAsync(url, dto);
			if (resp.IsSuccessStatusCode)
				return (true, null);

			// Lee el mensaje de error de la API
			var content = await resp.Content.ReadAsStringAsync();
			return (false, $"{(int)resp.StatusCode} {resp.ReasonPhrase}: {content}");
		}



		public async Task<List<TipoUbicacionDto>> ObtenerTiposUbicacionAsync()
		{
			// GET /api/ubicaciones/tipos?codigoEmpresa=1
			var url = $"Ubicaciones/tipos";
			var lista = await _httpClient.GetFromJsonAsync<List<TipoUbicacionDto>>(url);
			return lista ?? new List<TipoUbicacionDto>();
		}
		public async Task<List<AlergenoDto>> ObtenerAlergenosMaestrosAsync()
		{
			var lista = await _httpClient
				.GetFromJsonAsync<List<AlergenoDto>>("Alergenos/maestros");
			return lista ?? new List<AlergenoDto>();
		}


	}
}

