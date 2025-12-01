using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using SGA_Desktop.Models;

namespace SGA_Desktop.Services
{
    public class RendimientosService : ApiService
    {
        public RendimientosService() : base() { }

        public async Task<List<RendimientoOperarioDto>> ObtenerRendimientoOperariosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var queryParams = new List<string>();
                
                if (filtros.FechaDesde.HasValue)
                    queryParams.Add($"fechaDesde={Uri.EscapeDataString(filtros.FechaDesde.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.FechaHasta.HasValue)
                    queryParams.Add($"fechaHasta={Uri.EscapeDataString(filtros.FechaHasta.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.OperarioId.HasValue)
                    queryParams.Add($"operarioId={filtros.OperarioId.Value}");
                if (!string.IsNullOrEmpty(filtros.TipoProceso))
                    queryParams.Add($"tipoProceso={Uri.EscapeDataString(filtros.TipoProceso)}");
                if (filtros.CodigoEmpresa.HasValue)
                    queryParams.Add($"codigoEmpresa={filtros.CodigoEmpresa.Value}");
                if (!string.IsNullOrEmpty(filtros.CodigoAlmacen))
                    queryParams.Add($"codigoAlmacen={Uri.EscapeDataString(filtros.CodigoAlmacen)}");

                var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var resp = await _httpClient.GetAsync($"rendimientos/operarios{queryString}");
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = await resp.Content.ReadAsStringAsync();
                    throw new ApplicationException($"Error al obtener rendimiento de operarios: {errorText}");
                }

                var text = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<RendimientoOperarioDto>>(text,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<RendimientoOperarioDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerRendimientoOperariosAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<List<RendimientoProcesoDto>> ObtenerRendimientoProcesosAsync(FiltroRendimientosDto filtros)
        {
            try
            {
                var queryParams = new List<string>();
                
                if (filtros.FechaDesde.HasValue)
                    queryParams.Add($"fechaDesde={Uri.EscapeDataString(filtros.FechaDesde.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.FechaHasta.HasValue)
                    queryParams.Add($"fechaHasta={Uri.EscapeDataString(filtros.FechaHasta.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.OperarioId.HasValue)
                    queryParams.Add($"operarioId={filtros.OperarioId.Value}");
                if (!string.IsNullOrEmpty(filtros.TipoProceso))
                    queryParams.Add($"tipoProceso={Uri.EscapeDataString(filtros.TipoProceso)}");
                if (filtros.CodigoEmpresa.HasValue)
                    queryParams.Add($"codigoEmpresa={filtros.CodigoEmpresa.Value}");
                if (!string.IsNullOrEmpty(filtros.CodigoAlmacen))
                    queryParams.Add($"codigoAlmacen={Uri.EscapeDataString(filtros.CodigoAlmacen)}");

                var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var resp = await _httpClient.GetAsync($"rendimientos/procesos{queryString}");
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = await resp.Content.ReadAsStringAsync();
                    throw new ApplicationException($"Error al obtener rendimiento de procesos: {errorText}");
                }

                var text = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<RendimientoProcesoDto>>(text,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<RendimientoProcesoDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerRendimientoProcesosAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<ComparativaRendimientoDto> ObtenerComparativaAsync(FiltroRendimientosDto filtros, string tipoComparativa = "OPERARIOS")
        {
            try
            {
                var queryParams = new List<string>();
                
                if (filtros.FechaDesde.HasValue)
                    queryParams.Add($"fechaDesde={Uri.EscapeDataString(filtros.FechaDesde.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.FechaHasta.HasValue)
                    queryParams.Add($"fechaHasta={Uri.EscapeDataString(filtros.FechaHasta.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.OperarioId.HasValue)
                    queryParams.Add($"operarioId={filtros.OperarioId.Value}");
                if (!string.IsNullOrEmpty(filtros.TipoProceso))
                    queryParams.Add($"tipoProceso={Uri.EscapeDataString(filtros.TipoProceso)}");
                if (filtros.CodigoEmpresa.HasValue)
                    queryParams.Add($"codigoEmpresa={filtros.CodigoEmpresa.Value}");
                if (!string.IsNullOrEmpty(filtros.CodigoAlmacen))
                    queryParams.Add($"codigoAlmacen={Uri.EscapeDataString(filtros.CodigoAlmacen)}");
                
                queryParams.Add($"tipoComparativa={Uri.EscapeDataString(tipoComparativa)}");

                var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var resp = await _httpClient.GetAsync($"rendimientos/comparativa{queryString}");
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = await resp.Content.ReadAsStringAsync();
                    throw new ApplicationException($"Error al obtener comparativa: {errorText}");
                }

                var text = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ComparativaRendimientoDto>(text,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new ComparativaRendimientoDto();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerComparativaAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TendenciaRendimientoDto>> ObtenerTendenciasAsync(FiltroRendimientosDto filtros, string tipoMetrica = "PRODUCTIVIDAD")
        {
            try
            {
                var queryParams = new List<string>();
                
                if (filtros.FechaDesde.HasValue)
                    queryParams.Add($"fechaDesde={Uri.EscapeDataString(filtros.FechaDesde.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.FechaHasta.HasValue)
                    queryParams.Add($"fechaHasta={Uri.EscapeDataString(filtros.FechaHasta.Value.ToString("yyyy-MM-dd"))}");
                if (filtros.OperarioId.HasValue)
                    queryParams.Add($"operarioId={filtros.OperarioId.Value}");
                if (!string.IsNullOrEmpty(filtros.TipoProceso))
                    queryParams.Add($"tipoProceso={Uri.EscapeDataString(filtros.TipoProceso)}");
                if (filtros.CodigoEmpresa.HasValue)
                    queryParams.Add($"codigoEmpresa={filtros.CodigoEmpresa.Value}");
                if (!string.IsNullOrEmpty(filtros.CodigoAlmacen))
                    queryParams.Add($"codigoAlmacen={Uri.EscapeDataString(filtros.CodigoAlmacen)}");
                
                queryParams.Add($"tipoMetrica={Uri.EscapeDataString(tipoMetrica)}");

                var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var resp = await _httpClient.GetAsync($"rendimientos/tendencias{queryString}");
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = await resp.Content.ReadAsStringAsync();
                    throw new ApplicationException($"Error al obtener tendencias: {errorText}");
                }

                var text = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<TendenciaRendimientoDto>>(text,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<TendenciaRendimientoDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerTendenciasAsync: {ex.Message}");
                throw;
            }
        }
    }
}

