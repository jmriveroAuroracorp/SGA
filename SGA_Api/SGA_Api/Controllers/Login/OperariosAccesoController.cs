using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Login;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class OperariosAccesoController : ControllerBase
    {
        private readonly SageDbContext _context;
        private readonly AuroraSgaDbContext _sgaContext;

        public OperariosAccesoController(SageDbContext context, AuroraSgaDbContext sgaContext)
        {
            _context = context;
            _sgaContext = sgaContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetOperariosConAcceso()
        {
            var resultado = await (from o in _context.Operarios
                                   join a in _context.AccesosOperarios
                                       on o.Id equals a.Operario
                                   where o.FechaBaja == null && a.MRH_CodigoAplicacion == 7
                                   select new OperariosAccesoDto
                                   {
                                       Operario = o.Id,
                                       NombreOperario = o.Nombre!,
                                       Contraseña = o.Contraseña,
                                       MRH_CodigoAplicacion = a.MRH_CodigoAplicacion
                                   }).ToListAsync();

            return Ok(resultado);
        }

        /// <summary>
        /// GET api/OperariosAcceso/limite-inventario/{operario}
        /// Obtiene el límite de inventario en euros para un operario específico
        /// </summary>
        [HttpGet("limite-inventario/{operario}")]
        public async Task<IActionResult> GetLimiteInventarioOperario(int operario)
        {
            try
            {
                var operarioData = await _context.Operarios
                    .Where(o => o.Id == operario && o.FechaBaja == null)
                    .Select(o => o.MRH_LimiteInventarioEuros)
                    .FirstOrDefaultAsync();

                if (operarioData == null)
                {
                    return NotFound($"Operario {operario} no encontrado o está dado de baja");
                }

                // Si no tiene límite establecido, devolver 0
                var limite = operarioData ?? 0m;
                return Ok(limite);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error obteniendo límite del operario: {ex.Message}");
            }
        }

        /// <summary>
        /// GET api/OperariosAcceso/limite-unidades/{operario}
        /// Obtiene el límite de unidades de inventario para un operario específico
        /// </summary>
        [HttpGet("limite-unidades/{operario}")]
        public async Task<IActionResult> GetLimiteUnidadesOperario(int operario)
        {
            try
            {
                var operarioData = await _context.Operarios
                    .Where(o => o.Id == operario && o.FechaBaja == null)
                    .Select(o => o.MRH_LimiteInventarioUnidades)
                    .FirstOrDefaultAsync();

                if (operarioData == null)
                {
                    return NotFound($"Operario {operario} no encontrado o está dado de baja");
                }

                var limite = operarioData ?? 0m;
                return Ok(limite);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error obteniendo límite de unidades del operario: {ex.Message}");
            }
        }

        /// <summary>
        /// GET api/OperariosAcceso/diferencias-dia/{operario}/{codigoArticulo}/{idInventarioActual}
        /// Obtiene las diferencias acumuladas del operario para un artículo específico en el día actual
        /// EXCLUYENDO el inventario actual (para no duplicar)
        /// </summary>
        [HttpGet("diferencias-dia/{operario}/{codigoArticulo}/{idInventarioActual}")]
        public async Task<IActionResult> GetDiferenciasOperarioArticuloDia(int operario, string codigoArticulo, Guid idInventarioActual)
        {
            try
            {
                var fechaHoy = DateTime.Today;
                var fechaManana = fechaHoy.AddDays(1);

                // DEBUG: Mostrar fechas de filtro
                Console.WriteLine($"Filtrando desde: {fechaHoy:yyyy-MM-dd HH:mm:ss} hasta: {fechaManana:yyyy-MM-dd HH:mm:ss}");

                // Obtener todas las líneas temporales del operario para este artículo hoy
                // EXCLUYENDO el inventario actual para no duplicar
                var lineasTemp = await _sgaContext.InventarioLineasTemp
                    .Where(lt => lt.UsuarioConteoId == operario &&
                                lt.CodigoArticulo == codigoArticulo &&
                                lt.IdInventario != idInventarioActual &&  // ← EXCLUIR INVENTARIO ACTUAL
                                lt.FechaConteo >= fechaHoy &&
                                lt.FechaConteo < fechaManana &&
                                !lt.Consolidado)
                    .ToListAsync();

                decimal totalUnidades = 0;
                decimal totalValorEuros = 0;

                // Precio medio a nivel de artículo (si no existe o es 0, el valor € será 0)
                var precioMedio = await _context.AcumuladoStock
                    .Where(a => a.CodigoArticulo == codigoArticulo)
                    .OrderByDescending(a => a.Ejercicio)
                    .Select(a => a.PrecioMedio)
                    .FirstOrDefaultAsync();
                precioMedio ??= 0m;

                foreach (var linea in lineasTemp)
                {
                    if (!linea.CantidadContada.HasValue) continue; // sin conteo no hay diferencia real

                    var diferencia = Math.Abs(linea.CantidadContada.Value - linea.StockActual);
                    Console.WriteLine($"TEMP: Inv={linea.IdInventario}, Contado={linea.CantidadContada}, Stock={linea.StockActual}, Diff={diferencia}, Fecha={linea.FechaConteo:yyyy-MM-dd HH:mm}");
                    if (diferencia > 0.01m)
                    {
                        totalUnidades += diferencia;
                        totalValorEuros += diferencia * precioMedio.Value;
                    }
                }

                // === NUEVO: Incluir también líneas consolidadas del día para el mismo operario y artículo ===
                var lineasConsolidadas = await _sgaContext.InventarioLineas
                    .Where(l => l.UsuarioValidacionId == operario &&
                                l.CodigoArticulo == codigoArticulo &&
                                l.IdInventario != idInventarioActual &&
                                l.FechaValidacion >= fechaHoy &&
                                l.FechaValidacion < fechaManana)
                    .Select(l => new { l.StockContado, l.StockActual })
                    .ToListAsync();

                foreach (var l in lineasConsolidadas)
                {
                    var diferencia = Math.Abs((l.StockContado ?? 0m) - l.StockActual);
                    Console.WriteLine($"CONSOLIDADA: Contado={l.StockContado}, Stock={l.StockActual}, Diff={diferencia}");
                    if (diferencia > 0.01m)
                    {
                        totalUnidades += diferencia;
                        totalValorEuros += diferencia * precioMedio.Value;
                    }
                }

                Console.WriteLine($"=== RESULTADO - Total Unidades: {totalUnidades}, Total Euros: {totalValorEuros} ===");

                return Ok(new
                {
                    codigoArticulo,
                    operario,
                    fecha = fechaHoy,
                    totalUnidades,
                    totalValorEuros,
                    lineasTemporales = lineasTemp.Count,
                    lineasConsolidadas = lineasConsolidadas.Count,
                    lineasEncontradas = lineasTemp.Count + lineasConsolidadas.Count,
                    debug = new {
                        fechaDesde = fechaHoy,
                        fechaHasta = fechaManana,
                        inventarioExcluido = idInventarioActual
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error obteniendo diferencias del operario: {ex.Message}");
            }
        }

        /// <summary>
        /// GET api/OperariosAcceso/debug-lineas-hoy/{operario}/{codigoArticulo}
        /// TEMPORAL: Muestra todas las líneas temporales del operario hoy para debugging
        /// NO MODIFICA NADA - SOLO CONSULTA
        /// </summary>
        [HttpGet("debug-lineas-hoy/{operario}/{codigoArticulo}")]
        public async Task<IActionResult> DebugLineasOperarioHoy(int operario, string codigoArticulo)
        {
            try
            {
                var fechaHoy = DateTime.Today;
                var fechaManana = fechaHoy.AddDays(1);

                // SOLO CONSULTAR - NO MODIFICAR NADA
                var todasLasLineas = await _sgaContext.InventarioLineasTemp
                    .Where(lt => lt.UsuarioConteoId == operario &&
                                lt.CodigoArticulo == codigoArticulo &&
                                lt.FechaConteo >= fechaHoy &&
                                lt.FechaConteo < fechaManana)
                    .Select(lt => new 
                    {
                        lt.IdTemp,
                        lt.IdInventario,
                        lt.CodigoUbicacion,
                        lt.StockActual,
                        lt.CantidadContada,
                        lt.FechaConteo,
                        lt.Consolidado,
                        TieneCantidadContada = lt.CantidadContada.HasValue,
                        Diferencia = lt.CantidadContada.HasValue ? 
                            Math.Abs(lt.CantidadContada.Value - lt.StockActual) : (decimal?)null
                    })
                    .ToListAsync();

                var resumen = new
                {
                    totalLineas = todasLasLineas.Count,
                    lineasConConteo = todasLasLineas.Count(l => l.TieneCantidadContada),
                    lineasSinConteo = todasLasLineas.Count(l => !l.TieneCantidadContada),
                    inventariosDistintos = todasLasLineas.Select(l => l.IdInventario).Distinct().Count(),
                    consolidadas = todasLasLineas.Count(l => l.Consolidado),
                    noConsolidadas = todasLasLineas.Count(l => !l.Consolidado),
                    totalDiferenciasReales = todasLasLineas
                        .Where(l => l.TieneCantidadContada && l.Diferencia > 0.01m)
                        .Sum(l => l.Diferencia ?? 0),
                    fecha = fechaHoy
                };

                return Ok(new
                {
                    resumen,
                    lineasDetalle = todasLasLineas.OrderBy(l => l.FechaConteo)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error en debug: {ex.Message}");
            }
        }
    }
}
