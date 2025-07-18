using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;

namespace SGA_Desktop.Services
{
	public class StockService : ApiService
	{

		public async Task<List<StockDto>> ObtenerPorArticuloAsync(
		int codigoEmpresa,
		string? codigoArticulo,
		string? partida = null,
		string? codigoAlmacen = null,
		string? codigoUbicacion = null,
		string? descripcion = null) // Nuevo parámetro
		{
			if (string.IsNullOrWhiteSpace(codigoArticulo) && string.IsNullOrWhiteSpace(descripcion))
				throw new ArgumentException("Se debe proporcionar codigoArticulo o descripcion.", nameof(codigoArticulo));

			var qs = $"?codigoEmpresa={codigoEmpresa}";

			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				qs += $"&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}";

			if (!string.IsNullOrWhiteSpace(partida))
				qs += $"&partida={Uri.EscapeDataString(partida!)}";

			if (!string.IsNullOrWhiteSpace(codigoAlmacen))
				qs += $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen!)}";

			if (codigoUbicacion != null)
				qs += $"&codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}";

			// Agregar el nuevo parámetro de descripción si no se proporciona código de artículo
			if (!string.IsNullOrWhiteSpace(descripcion))
				qs += $"&descripcion={Uri.EscapeDataString(descripcion)}";

			return await GetAsync<List<StockDto>>($"Stock/articulo{qs}");
		}



		/// <summary>
		/// GET /api/Stock/ubicacion
		/// Búsqueda por almacén + ubicación (-> "" para Sin ubicación)
		/// </summary>
		public async Task<List<StockDto>> ObtenerPorUbicacionAsync(
			int codigoEmpresa,
			string codigoAlmacen,
			string codigoUbicacion)
		{
			if (string.IsNullOrWhiteSpace(codigoAlmacen))
				throw new ArgumentException("codigoAlmacen es obligatorio.", nameof(codigoAlmacen));
			if (codigoUbicacion == null)
				throw new ArgumentNullException(nameof(codigoUbicacion));

			var qs = $"?codigoEmpresa={codigoEmpresa}"
				   + $"&codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}"
				   // Siempre incluimos codigoUbicacion, aunque sea cadena vacía:
				   + $"&codigoUbicacion={Uri.EscapeDataString(codigoUbicacion)}";

			try
			{
				return await GetAsync<List<StockDto>>($"Stock/ubicacion{qs}");
			}
			catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
			{
				// No hay nada en esa ubicación → lista vacía
				return new List<StockDto>();
			}
		}

		public async Task<string> ObtenerAlergenosArticuloAsync(int codigoEmpresa, string codigoArticulo)
		{
			var url = $"Stock/articulo/alergenos?codigoEmpresa={codigoEmpresa}&codigoArticulo={Uri.EscapeDataString(codigoArticulo)}";
			var response = await _httpClient.GetAsync(url);
			var jsonRaw = await response.Content.ReadAsStringAsync();

			// 1) Muéstralo en pantalla para inspeccionarlo:
			MessageBox.Show(jsonRaw, "JSON crudo de alérgenos");

			if (!response.IsSuccessStatusCode)
				return string.Empty;

			// Resto de deserialización…
			var wrapper = JsonConvert.DeserializeObject<AlergenosWrapper>(jsonRaw);
			return wrapper?.Alergenos ?? string.Empty;
		}

		/// <summary>
		/// Helper genérico para GET + deserializar JSON
		/// </summary>
		private async Task<T> GetAsync<T>(string relativeUrl)
		{
			var response = await _httpClient.GetAsync(relativeUrl);
			response.EnsureSuccessStatusCode();
			var json = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<T>(json)!;
		}

		// ----------------------
		// Métodos existentes para /api/Almacen
		// ----------------------

		/// <summary>
		/// GET /api/Almacen?codigoCentro=…
		/// Devuelve solo la lista de códigos de almacén.
		/// </summary>
		public async Task<List<string>> ObtenerAlmacenesAsync(string codigoCentro)
		{
			var resp = await _httpClient.GetAsync($"Almacen?codigoCentro={Uri.EscapeDataString(codigoCentro)}");
			resp.EnsureSuccessStatusCode();
			return JsonConvert.DeserializeObject<List<string>>(await resp.Content.ReadAsStringAsync())
				   ?? new List<string>();
		}

		/// <summary>
		/// GET /api/Almacen/Ubicaciones?codigoAlmacen=...
		/// Devuelve la lista de códigos de ubicación para el almacén dado.
		/// </summary>
		/// 
		public async Task<List<UbicacionDto>> ObtenerUbicacionesAsync(string codigoAlmacen, short codigoEmpresa, bool soloConStock = false)
		{
			var url = $"Almacen/Ubicaciones?codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}" +
					  $"&codigoEmpresa={codigoEmpresa}" +
					  $"&soloConStock={soloConStock.ToString().ToLower()}";

			using var resp = await _httpClient.GetAsync(url);
			resp.EnsureSuccessStatusCode();
			var json = await resp.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<List<UbicacionDto>>(json) ?? new();
		}




		// VERSION QUE NO FILTRA UBICACIONES POR SI TIENEN O NO STOCK

		//public async Task<List<string>> ObtenerUbicacionesAsync(string codigoAlmacen)
		//{
		//	var url = $"Almacen/Ubicaciones?codigoAlmacen={Uri.EscapeDataString(codigoAlmacen)}";
		//	using var resp = await _httpClient.GetAsync(url);
		//	resp.EnsureSuccessStatusCode();
		//	var json = await resp.Content.ReadAsStringAsync();
		//	var dtoList = JsonConvert
		//		.DeserializeObject<List<UbicacionDto>>(json)
		//		?? new List<UbicacionDto>();
		//	return dtoList.Select(x => x.Ubicacion).ToList();
		//}

		public async Task<List<AlmacenDto>> ObtenerAlmacenesAutorizadosAsync(short empresa, string centro, List<string> codigos)
		{
			var request = new AlmacenesAutorizadosRequest
			{
				CodigoEmpresa = empresa,
				CodigoCentro = centro,
				CodigosAlmacen = codigos
			};

			var resp = await _httpClient.PostAsJsonAsync("Almacen/Combos/Autorizados", request);
			resp.EnsureSuccessStatusCode();

			var json = await resp.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<List<AlmacenDto>>(json) ?? new List<AlmacenDto>();
		}

		private class AlergenosWrapper
		{
			[JsonProperty("alergenos")]       // coincide con la clave JSON
			public string Alergenos { get; set; }
		}

	

		/// <summary>
		/// Obtiene el stock disponible (con Reservado y Disponible) por artículo y/o descripción.
		/// Llama a /api/stock/articulo/disponible
		/// </summary>
		public async Task<List<StockDisponibleDto>> ObtenerStockDisponibleAsync(string codigoArticulo, string descripcion)
		{
			var queryParams = new Dictionary<string, string>();

			if (!string.IsNullOrWhiteSpace(codigoArticulo))
				queryParams["codigoArticulo"] = codigoArticulo;

			if (!string.IsNullOrWhiteSpace(descripcion))
				queryParams["descripcion"] = descripcion;

			// Añade empresa actual
			queryParams["codigoEmpresa"] = SessionManager.EmpresaSeleccionada.ToString();

			var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
			var url = $"/api/stock/articulo/disponible?{queryString}";

			var response = await _httpClient.GetAsync(url);
			response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			return JsonConvert.DeserializeObject<List<StockDisponibleDto>>(json) ?? new List<StockDisponibleDto>();
		}
		// ... existing code ...
	}
}
