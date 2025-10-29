using Newtonsoft.Json;
using SGA_Desktop.Models;
using System.Net.Http.Json;
using System.Net.Http;
using System.Windows;
using System.Text;
using System.Diagnostics;
using System.Linq;

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
			try
			{
				// TEMPORAL: Volver al endpoint original para que funcione
				var url = new StringBuilder($"Almacen/Ubicaciones?codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}")
					.Append($"&codigoEmpresa={codigoEmpresa}");
				if (soloConStock) url.Append("&soloConStock=true");

				System.Diagnostics.Debug.WriteLine($"🔍 ObtenerUbicacionesAsync - URL: {_httpClient.BaseAddress}{url}");
				
				var resp = await _httpClient.GetAsync(url.ToString());
				System.Diagnostics.Debug.WriteLine($"🔍 Response Status: {resp.StatusCode}");
				
				if (!resp.IsSuccessStatusCode)
				{
					var errorContent = await resp.Content.ReadAsStringAsync();
					System.Diagnostics.Debug.WriteLine($"🔍 Error Response: {errorContent}");
					return new List<UbicacionDto>();
				}
				
				var jsonContent = await resp.Content.ReadAsStringAsync();
				System.Diagnostics.Debug.WriteLine($"🔍 JSON Response: {jsonContent}");
				
				var resultado = JsonConvert
					.DeserializeObject<List<UbicacionDto>>(jsonContent)
					?? new List<UbicacionDto>();
				
				System.Diagnostics.Debug.WriteLine($"🔍 Ubicaciones encontradas: {resultado.Count}");
				return resultado;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"🔍 Error en ObtenerUbicacionesAsync: {ex.Message}");
				return new List<UbicacionDto>();
			}
		}

		// NUEVO: Método que obtiene ubicaciones desde AuroraSga (donde se crean las nuevas)
		public async Task<List<UbicacionDto>> ObtenerUbicacionesAuroraAsync(
			string codigoAlmacen,
			short codigoEmpresa,
			bool soloConStock = false)
		{
			try
			{
				// Usar el endpoint basica que funciona correctamente
				var url = $"ubicaciones/basica?codigoEmpresa={codigoEmpresa}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
				
				System.Diagnostics.Debug.WriteLine($"🔍 ObtenerUbicacionesAuroraAsync - URL: {_httpClient.BaseAddress}{url}");
				
				var resp = await _httpClient.GetAsync(url);
				System.Diagnostics.Debug.WriteLine($"🔍 Response Status: {resp.StatusCode}");
				
				if (!resp.IsSuccessStatusCode)
				{
					var errorContent = await resp.Content.ReadAsStringAsync();
					System.Diagnostics.Debug.WriteLine($"🔍 Error Response: {errorContent}");
					return new List<UbicacionDto>();
				}
				
				var jsonContent = await resp.Content.ReadAsStringAsync();
				System.Diagnostics.Debug.WriteLine($"🔍 JSON Response: {jsonContent}");
				
				// El endpoint basica devuelve un objeto anónimo, necesitamos mapearlo
				var ubicacionesBasicas = JsonConvert.DeserializeObject<List<dynamic>>(jsonContent) ?? new List<dynamic>();
				
				System.Diagnostics.Debug.WriteLine($"🔍 Ubicaciones Aurora encontradas: {ubicacionesBasicas.Count}");
				
				// Mapear a UbicacionDto
				var resultado = ubicacionesBasicas.Select(u => 
				{
					var codigoAlmacen = u.codigoAlmacen?.ToString() ?? "";
					var ubicacion = u.ubicacion?.ToString() ?? "";
					
					System.Diagnostics.Debug.WriteLine($"🔍 Mapeando: Almacen='{codigoAlmacen}', Ubicacion='{ubicacion}'");
					
					return new UbicacionDto
					{
						CodigoAlmacen = codigoAlmacen,
						Ubicacion = ubicacion
					};
				}).ToList();
				
				System.Diagnostics.Debug.WriteLine($"🔍 Ubicaciones Aurora mapeadas: {resultado.Count}");
				return resultado;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"🔍 Error en ObtenerUbicacionesAuroraAsync: {ex.Message}");
				return new List<UbicacionDto>();
			}
		}

		// 2) Para el detalle completo, que devuelve UbicacionDetalladaDto
		public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesDetalladasAsync(
			short codigoEmpresa,
			string codigoAlmacen)
		{
			// Este endpoint está en otro controller: GET /api/ubicaciones?codigoEmpresa=X&codigoAlmacen=Y
			var url = $"ubicaciones?codigoEmpresa={codigoEmpresa}"
					+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
			var lista = await _httpClient
				.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);
			return lista ?? new List<UbicacionDetalladaDto>();
		}

		// 2.2) Nuevo: carga solo lo "básico" sin alérgenos ni riesgo
		public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesBasicoAsync(
	short codigoEmpresa,
	string codigoAlmacen)
		{
			// ¡ojo al nombre!
			var url = $"ubicaciones/basica"
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
			var url = $"ubicaciones/alergenos/presentes"
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
			var url = $"ubicaciones/alergenos/permitidos"
					+ $"?codigoEmpresa={codigoEmpresa}"
					+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}"
					+ $"&ubicacion={Uri.EscapeDataString(ubicacion)}";

			return await _httpClient.GetFromJsonAsync<List<AlergenoDto>>(url)
				   ?? new List<AlergenoDto>();
		}

		public async Task<bool> CrearUbicacionDetalladaAsync(CrearUbicacionDetalladaDto dto)
		{
			var resp = await _httpClient.PostAsJsonAsync(
				"ubicaciones", dto);
			return resp.IsSuccessStatusCode;
		}

		// PUT (actualizar)
		public async Task<(bool Success, string? ErrorMessage)> ActualizarUbicacionDetalladaAsync(CrearUbicacionDetalladaDto dto)
		{
			string url;
			
			// Si el código está vacío o es "SIN UBICAR", usar el endpoint especial
			if (string.IsNullOrEmpty(dto.CodigoUbicacion) || dto.CodigoUbicacion == "SIN UBICAR")
			{
				url = "ubicaciones/sin-ubicar";
			}
			else
			{
				// Para ubicaciones normales (incluyendo "SinUbicar2"), usar el endpoint estándar
				url = $"ubicaciones/{dto.CodigoEmpresa}/{Uri.EscapeDataString(dto.CodigoAlmacen)}/{Uri.EscapeDataString(dto.CodigoUbicacion)}";
			}
			
			// Debug: Log de la URL que se está llamando
			System.Diagnostics.Debug.WriteLine($"PUT URL: {_httpClient.BaseAddress}{url}");
			System.Diagnostics.Debug.WriteLine($"DTO: Empresa={dto.CodigoEmpresa}, Almacen={dto.CodigoAlmacen}, Ubicacion={dto.CodigoUbicacion}");
			
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
			var url = $"ubicaciones/tipos";
			var lista = await _httpClient.GetFromJsonAsync<List<TipoUbicacionDto>>(url);
			return lista ?? new List<TipoUbicacionDto>();
		}
		public async Task<List<AlergenoDto>> ObtenerAlergenosMaestrosAsync()
		{
			var lista = await _httpClient
				.GetFromJsonAsync<List<AlergenoDto>>("Alergenos/maestros");
			return lista ?? new List<AlergenoDto>();
		}

		/// <summary>
		/// POST /api/ubicaciones/masivo
		/// Envía un lote de ubicaciones para crear en bloque.
		/// </summary>
		public async Task<bool> CrearUbicacionesMasivoAsync(
			List<CrearUbicacionDetalladaDto> dtos)
		{
			if (dtos == null || dtos.Count == 0)
				return false;

			// Serializamos y lanzamos el POST al endpoint "ubicaciones/masivo"
			var resp = await _httpClient.PostAsJsonAsync("ubicaciones/masivo", dtos);
			return resp.IsSuccessStatusCode;
		}


		//	public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesVaciasOEspAsync(
		//short codigoEmpresa, string codigoAlmacen)
		//	{
		//		var url = $"ubicaciones/vacias-o-especiales"
		//				+ $"?codigoEmpresa={codigoEmpresa}"
		//				+ $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";

		//		var lista = await _httpClient
		//			.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);

		//		return lista ?? new List<UbicacionDetalladaDto>();
		//	}
		public async Task<List<UbicacionDetalladaDto>> ObtenerUbicacionesVaciasOEspAsync(
		short codigoEmpresa, string codigoAlmacen, List<string>? ubicacionesActuales = null)
		{
			var url = $"ubicaciones/vacias-o-especiales?codigoEmpresa={codigoEmpresa}&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";

			if (ubicacionesActuales != null && ubicacionesActuales.Any())
			{
				foreach (var ubic in ubicacionesActuales)
				{
					url += $"&ubicacionesActuales={Uri.EscapeDataString(ubic)}";
				}
			}

			var lista = await _httpClient
				.GetFromJsonAsync<List<UbicacionDetalladaDto>>(url);

			return lista ?? new List<UbicacionDetalladaDto>();
		}

	}
}

