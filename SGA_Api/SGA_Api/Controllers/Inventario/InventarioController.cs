using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Inventario;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace SGA_Api.Controllers.Inventario
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventarioController : ControllerBase
    {
        private readonly AuroraSgaDbContext _context;
        private readonly StorageControlDbContext _storageContext;
        private readonly SageDbContext _sageDbContext;
        private readonly ILogger<InventarioController> _logger;

        public InventarioController(AuroraSgaDbContext context, StorageControlDbContext storageContext, SageDbContext sageDbContext, ILogger<InventarioController> logger)
        {
            _context = context;
            _storageContext = storageContext;
            _sageDbContext = sageDbContext;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/Inventario/consultar
        /// Consulta inventarios con filtros
        /// </summary>
        [HttpPost("consultar")]
        public async Task<IActionResult> ConsultarInventarios([FromBody] FiltroInventarioDto filtro)
        {
            try
            {
                var query = _context.InventarioCabecera.AsQueryable();

                // Filtro por empresa (obligatorio)
                query = query.Where(i => i.CodigoEmpresa == filtro.CodigoEmpresa);

                // Filtro por almacén
                if (!string.IsNullOrWhiteSpace(filtro.CodigoAlmacen))
                {
                    query = query.Where(i => i.CodigoAlmacen == filtro.CodigoAlmacen);
                }
                else if (filtro.CodigosAlmacen?.Any() == true)
                {
                    query = query.Where(i => filtro.CodigosAlmacen.Contains(i.CodigoAlmacen));
                }

                // Filtro por estado
                if (!string.IsNullOrWhiteSpace(filtro.EstadoInventario))
                {
                    query = query.Where(i => i.Estado == filtro.EstadoInventario);
                }

                // Filtros de fecha
                if (filtro.FechaDesde.HasValue)
                {
                    query = query.Where(i => i.FechaCreacion >= filtro.FechaDesde.Value);
                }

                if (filtro.FechaHasta.HasValue)
                {
                    query = query.Where(i => i.FechaCreacion <= filtro.FechaHasta.Value);
                }

                // Obtener inventarios primero
                var inventarios = await query
                    .OrderByDescending(i => i.FechaCreacion)
                    .Take(100)
                    .ToListAsync();

                // Crear diccionario de usuarios para mapear IDs a nombres (igual que en Palets)
                var nombreDict = await _context.vUsuariosConNombre
                    .ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

                // Asignar nombres de usuarios a cada inventario
                foreach (var inventario in inventarios)
                {
                    if (nombreDict.TryGetValue(inventario.UsuarioCreacionId, out var nombre))
                        inventario.UsuarioCreacionNombre = nombre;

                    if (inventario.UsuarioProcesamientoId.HasValue && 
                        nombreDict.TryGetValue(inventario.UsuarioProcesamientoId.Value, out var nombreProcesamiento))
                        inventario.UsuarioProcesamientoNombre = nombreProcesamiento;
                }

                return Ok(inventarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar inventarios");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// GET /api/Inventario/test
        /// Endpoint de prueba para verificar conexión
        /// </summary>
        [HttpGet("test")]
        public IActionResult TestConnection()
        {
            return Ok(new { 
                Success = true, 
                Message = "API funcionando correctamente", 
                Timestamp = DateTime.Now
            });
        }







        /// <summary>
        /// POST /api/Inventario/crear
        /// Crea un nuevo inventario (cabecera)
        /// </summary>
        [HttpPost("crear")]
        public async Task<IActionResult> CrearInventario([FromBody] CrearInventarioDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.CodigoInventario))
                    return BadRequest("El código de inventario es obligatorio");

                if (string.IsNullOrWhiteSpace(dto.CodigoAlmacen))
                    return BadRequest("El código de almacén es obligatorio");

                if (string.IsNullOrWhiteSpace(dto.TipoInventario))
                    return BadRequest("El tipo de inventario es obligatorio");

                if (!new[] { "TOTAL", "PARCIAL" }.Contains(dto.TipoInventario.ToUpper()))
                    return BadRequest("El tipo de inventario debe ser 'TOTAL' o 'PARCIAL'");

                // Verificar que el código de inventario sea único para la empresa
                var existeCodigo = await _context.InventarioCabecera
                    .AnyAsync(i => i.CodigoEmpresa == dto.CodigoEmpresa && 
                                   i.CodigoInventario == dto.CodigoInventario);

                if (existeCodigo)
                    return BadRequest($"Ya existe un inventario con el código '{dto.CodigoInventario}' en esta empresa");

                var rangoFormateado = FormatearRangoUbicaciones(dto);

                var inventario = new InventarioCabecera
                {
                    IdInventario = Guid.NewGuid(),
                    CodigoInventario = dto.CodigoInventario,
                    CodigoEmpresa = dto.CodigoEmpresa,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    RangoUbicaciones = rangoFormateado,
                    TipoInventario = dto.TipoInventario.ToUpper(),
                    Comentarios = dto.Comentarios,
                    Estado = "ABIERTO",
                    UsuarioCreacionId = dto.UsuarioCreacionId,
                    FechaCreacion = DateTime.Now
                };

                _context.InventarioCabecera.Add(inventario);
                await _context.SaveChangesAsync();

                return Ok(new { Id = inventario.IdInventario, Mensaje = "Inventario creado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// POST /api/Inventario/contar
        /// Registra un conteo de inventario (línea temporal)
        /// </summary>
        [HttpPost("contar")]
        public async Task<IActionResult> ContarInventario([FromBody] ContarInventarioDto dto)
        {
            try
            {
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == dto.IdInventario);

                if (inventario == null)
                    return NotFound("Inventario no encontrado");

                if (inventario.Estado != "ABIERTO")
                    return BadRequest("Solo se pueden contar inventarios abiertos");

                var lineaTemp = new InventarioLineasTemp
                {
                    IdTemp = Guid.NewGuid(),
                    IdInventario = dto.IdInventario,
                    CodigoArticulo = dto.CodigoArticulo,
                    CodigoUbicacion = dto.CodigoUbicacion,
                    CantidadContada = dto.CantidadContada,
                    UsuarioConteoId = dto.UsuarioConteoId,
                    FechaConteo = DateTime.Now,
                    Observaciones = dto.Observaciones,
                    Consolidado = false
                };

                _context.InventarioLineasTemp.Add(lineaTemp);
                await _context.SaveChangesAsync();

                return Ok(new { Mensaje = "Conteo registrado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al contar inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// POST /api/Inventario/consolidar/{idInventario}
        /// Consolida las líneas temporales en líneas definitivas
        /// </summary>
        [HttpPost("consolidar/{idInventario}")]
        public async Task<IActionResult> ConsolidarInventario(Guid idInventario)
        {
            try
            {
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);

                if (inventario == null)
                    return NotFound("Inventario no encontrado");

                if (inventario.Estado != "ABIERTO")
                    return BadRequest("Solo se pueden consolidar inventarios abiertos");

                var lineasTemp = await _context.InventarioLineasTemp
                    .Where(lt => lt.IdInventario == idInventario && !lt.Consolidado)
                    .ToListAsync();

                if (!lineasTemp.Any())
                    return BadRequest("No hay líneas temporales para consolidar");

                foreach (var lineaTemp in lineasTemp)
                {
                    var linea = new InventarioLineas
                    {
                        IdLinea = Guid.NewGuid(),
                        IdInventario = idInventario,
                        CodigoArticulo = lineaTemp.CodigoArticulo,
                        CodigoUbicacion = lineaTemp.CodigoUbicacion,
                        StockTeorico = 0, // Se calculará después
                        StockContado = lineaTemp.CantidadContada,
                        Estado = "CONTADA",
                        Observaciones = lineaTemp.Observaciones
                    };

                    _context.InventarioLineas.Add(linea);
                    lineaTemp.Consolidado = true;
                    lineaTemp.FechaConsolidacion = DateTime.Now;
                }

                inventario.Estado = "PENDIENTE_CIERRE";
                await _context.SaveChangesAsync();

                return Ok(new { Mensaje = "Inventario consolidado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consolidar inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// POST /api/Inventario/cerrar/{idInventario}
        /// Cierra un inventario y genera ajustes
        /// </summary>
        [HttpPost("cerrar/{idInventario}")]
        public async Task<IActionResult> CerrarInventario(Guid idInventario)
        {
            try
            {
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);

                if (inventario == null)
                    return NotFound("Inventario no encontrado");

                if (inventario.Estado != "PENDIENTE_CIERRE")
                    return BadRequest("Solo se pueden cerrar inventarios pendientes de cierre");

                var lineas = await _context.InventarioLineas
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();

                foreach (var linea in lineas)
                {
                    if (linea.StockContado.HasValue)
                    {
                        var diferencia = linea.StockContado.Value - linea.StockTeorico;
                        
                        if (Math.Abs(diferencia) > 0.01m)
                        {
                            var ajuste = new InventarioAjustes
                            {
                                IdAjuste = Guid.NewGuid(),
                                IdInventario = idInventario,
                                CodigoArticulo = linea.CodigoArticulo,
                                CodigoUbicacion = linea.CodigoUbicacion,
                                Diferencia = diferencia,
                                TipoAjuste = diferencia > 0 ? "POSITIVO" : "NEGATIVO",
                                UsuarioId = inventario.UsuarioCreacionId,
                                Fecha = DateTime.Now
                            };

                            _context.InventarioAjustes.Add(ajuste);
                        }
                    }
                }

                inventario.Estado = "CERRADO";
                inventario.FechaCierre = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new { Mensaje = "Inventario cerrado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// GET /api/Inventario/lineas/{idInventario}
        /// Obtiene las líneas de un inventario
        /// </summary>
        [HttpGet("lineas/{idInventario}")]
        public async Task<IActionResult> ObtenerLineasInventario(Guid idInventario)
        {
            try
            {
                var lineas = await _context.InventarioLineas
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();

                return Ok(lineas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener líneas de inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// GET /api/Inventario/ajustes/{idInventario}
        /// Obtiene los ajustes de un inventario
        /// </summary>
        [HttpGet("ajustes/{idInventario}")]
        public async Task<IActionResult> ObtenerAjustesInventario(Guid idInventario)
        {
            try
            {
                var ajustes = await _context.InventarioAjustes
                    .Where(a => a.IdInventario == idInventario)
                    .ToListAsync();

                return Ok(ajustes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ajustes de inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// GET /api/Inventario/stock-ubicaciones
        /// Obtiene el stock actual de un rango de ubicaciones para el grid de inventario
        /// </summary>
        [HttpGet("stock-ubicaciones")]
        public async Task<IActionResult> ObtenerStockUbicaciones(
            [FromQuery] int codigoEmpresa,
            [FromQuery] string codigoAlmacen,
            [FromQuery] int? pasilloDesde = null,
            [FromQuery] int? pasilloHasta = null,
            [FromQuery] int? estanteriaDesde = null,
            [FromQuery] int? estanteriaHasta = null,
            [FromQuery] int? alturaDesde = null,
            [FromQuery] int? alturaHasta = null,
            [FromQuery] int? posicionDesde = null,
            [FromQuery] int? posicionHasta = null)
        {
            try
            {
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                    return BadRequest("Sin ejercicio");

                var totalCombinaciones = CalcularCombinaciones(
                    pasilloDesde, pasilloHasta,
                    estanteriaDesde, estanteriaHasta,
                    alturaDesde, alturaHasta,
                    posicionDesde, posicionHasta
                );

                if (totalCombinaciones > 10000)
                    return BadRequest($"Rango demasiado amplio. Máximo 10.000 ubicaciones permitidas. Solicitadas: {totalCombinaciones:N0}");

                var query = _storageContext.AcumuladoStockUbicacion
                    .Where(s => s.CodigoEmpresa == codigoEmpresa &&
                               s.Ejercicio == ejercicio &&
                               s.CodigoAlmacen == codigoAlmacen &&
                               s.UnidadSaldo != 0 &&
                               s.Ubicacion.StartsWith("UB"));

                var stockData = await query.ToListAsync();

                var filteredData = stockData.Where(s =>
                {
                    if (s.Ubicacion.Length < 14) return false;

                    var pasilloStr = s.Ubicacion.Substring(2, 3);
                    var estanteriaStr = s.Ubicacion.Substring(5, 3);
                    var alturaStr = s.Ubicacion.Substring(8, 3);
                    var posicionStr = s.Ubicacion.Substring(11, 3);

                    if (!int.TryParse(pasilloStr, out int pasillo) ||
                        !int.TryParse(estanteriaStr, out int estanteria) ||
                        !int.TryParse(alturaStr, out int altura) ||
                        !int.TryParse(posicionStr, out int posicion))
                        return false;

                    if (pasilloDesde.HasValue && pasillo < pasilloDesde.Value) return false;
                    if (pasilloHasta.HasValue && pasillo > pasilloHasta.Value) return false;
                    if (estanteriaDesde.HasValue && estanteria < estanteriaDesde.Value) return false;
                    if (estanteriaHasta.HasValue && estanteria > estanteriaHasta.Value) return false;
                    if (alturaDesde.HasValue && altura < alturaDesde.Value) return false;
                    if (alturaHasta.HasValue && altura > alturaHasta.Value) return false;
                    if (posicionDesde.HasValue && posicion < posicionDesde.Value) return false;
                    if (posicionHasta.HasValue && posicion > posicionHasta.Value) return false;

                    return true;
                });

                var almacenes = await _sageDbContext.Almacenes
                    .Where(a => a.CodigoEmpresa == codigoEmpresa)
                    .ToListAsync();

                var articulos = await _sageDbContext.Articulos
                    .Where(a => a.CodigoEmpresa == codigoEmpresa)
                    .ToListAsync();

                var stockUbicaciones = filteredData
                    .Select(s =>
                    {
                        var alm = almacenes.FirstOrDefault(x =>
                            x.CodigoEmpresa == s.CodigoEmpresa &&
                            x.CodigoAlmacen == s.CodigoAlmacen);
                        var art = articulos.FirstOrDefault(x =>
                            x.CodigoEmpresa == s.CodigoEmpresa &&
                            x.CodigoArticulo == s.CodigoArticulo);

                        return new
                        {
                            CodigoArticulo = s.CodigoArticulo,
                            DescripcionArticulo = art?.DescripcionArticulo,
                            CodigoAlmacen = s.CodigoAlmacen,
                            Ubicacion = s.Ubicacion,
                            StockTeorico = s.UnidadSaldo,
                            StockContado = (decimal?)null,
                            Diferencia = 0m,
                            TieneDiferencia = false
                        };
                    })
                    .OrderBy(s => s.Ubicacion)
                    .ThenBy(s => s.CodigoArticulo)
                    .ToList();

                return Ok(stockUbicaciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock de ubicaciones");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// GET /api/Inventario/rangos-disponibles
        /// Obtiene los rangos de ubicaciones disponibles en un almacén
        /// </summary>
        [HttpGet("rangos-disponibles")]
        public async Task<IActionResult> ObtenerRangosDisponibles(
            [FromQuery] int codigoEmpresa,
            [FromQuery] string codigoAlmacen)
        {
            try
            {
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                    return BadRequest("Sin ejercicio");

                var ubicaciones = await _storageContext.AcumuladoStockUbicacion
                    .Where(s => s.CodigoEmpresa == codigoEmpresa &&
                               s.Ejercicio == ejercicio &&
                               s.CodigoAlmacen == codigoAlmacen &&
                               s.UnidadSaldo != 0 &&
                               s.Ubicacion.StartsWith("UB"))
                    .Select(s => s.Ubicacion)
                    .ToListAsync();

                var rangos = new
                {
                    Pasillos = ubicaciones
                        .Where(u => u.Length >= 5)
                        .Select(u => int.Parse(u.Substring(2, 3)))
                        .Distinct()
                        .OrderBy(p => p)
                        .ToList(),
                    Estanterias = ubicaciones
                        .Where(u => u.Length >= 8)
                        .Select(u => int.Parse(u.Substring(5, 3)))
                        .Distinct()
                        .OrderBy(e => e)
                        .ToList(),
                    Alturas = ubicaciones
                        .Where(u => u.Length >= 11)
                        .Select(u => int.Parse(u.Substring(8, 3)))
                        .Distinct()
                        .OrderBy(a => a)
                        .ToList(),
                    Posiciones = ubicaciones
                        .Where(u => u.Length >= 14)
                        .Select(u => int.Parse(u.Substring(11, 3)))
                        .Distinct()
                        .OrderBy(p => p)
                        .ToList(),
                    TotalUbicaciones = ubicaciones.Count
                };

                return Ok(rangos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rangos disponibles");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// POST /api/Inventario/guardar-conteo
        /// Guarda el conteo físico de un inventario y genera líneas temporales
        /// </summary>
        [HttpPost("guardar-conteo")]
        public async Task<IActionResult> GuardarConteoInventario([FromBody] GuardarConteoInventarioDto conteo)
        {
            try
            {
                _logger.LogInformation($"Guardando conteo para inventario {conteo.IdInventario}");

                // Validar que el inventario existe y está en estado válido
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == conteo.IdInventario);

                if (inventario == null)
                {
                    _logger.LogWarning($"Inventario {conteo.IdInventario} no encontrado");
                    return NotFound("Inventario no encontrado");
                }

                if (inventario.Estado != "ABIERTO")
                {
                    _logger.LogWarning($"Inventario {conteo.IdInventario} no está abierto. Estado actual: {inventario.Estado}");
                    return BadRequest("El inventario debe estar abierto para guardar conteo");
                }

                // Obtener ejercicio actual
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                {
                    _logger.LogError("No se encontró ejercicio válido para la empresa");
                    return BadRequest("Sin ejercicio válido");
                }

                // Procesar cada artículo del conteo
                foreach (var articulo in conteo.Articulos)
                {
                    // Buscar stock actual del artículo
                    var stockActual = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => 
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == inventario.CodigoAlmacen &&
                            s.CodigoArticulo == articulo.CodigoArticulo &&
                            s.Ubicacion == articulo.CodigoUbicacion &&
                            s.Partida == articulo.Partida);

                    // Calcular diferencia
                    var stockTeorico = stockActual?.UnidadSaldo ?? 0;
                    var stockContado = articulo.CantidadInventario;
                    var diferencia = stockContado - stockTeorico;

                    // Crear línea temporal de inventario para cada conteo
                    var lineaTemp = new InventarioLineasTemp
                    {
                        IdInventario = inventario.IdInventario,
                        CodigoArticulo = articulo.CodigoArticulo,
                        CodigoUbicacion = articulo.CodigoUbicacion,
                        CantidadContada = stockContado,
                        UsuarioConteoId = articulo.UsuarioConteo,
                        FechaConteo = DateTime.Now,
                        Consolidado = false
                    };

                    _context.InventarioLineasTemp.Add(lineaTemp);
                }

                // Guardar cambios
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Conteo guardado exitosamente para inventario {conteo.IdInventario}. {conteo.Articulos.Count} artículos procesados");
                return Ok(new { mensaje = "Conteo guardado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar conteo de inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// POST /api/Inventario/consolidar/{idInventario}
        /// Consolida las líneas temporales de un inventario
        /// </summary>
        [HttpPost("consolidar/{idInventario}")]
        public async Task<IActionResult> ConsolidarInventario(Guid idInventario, [FromBody] int usuarioValidacionId)
        {
            try
            {
                _logger.LogInformation($"Consolidando inventario {idInventario}");

                // Validar que el inventario existe y está abierto
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);

                if (inventario == null)
                {
                    _logger.LogWarning($"Inventario {idInventario} no encontrado");
                    return NotFound("Inventario no encontrado");
                }

                if (inventario.Estado != "ABIERTO")
                {
                    _logger.LogWarning($"Inventario {idInventario} no está abierto. Estado actual: {inventario.Estado}");
                    return BadRequest("El inventario debe estar abierto para consolidar");
                }

                // Obtener ejercicio actual
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                {
                    _logger.LogError("No se encontró ejercicio válido para la empresa");
                    return BadRequest("Sin ejercicio válido");
                }

                // Obtener líneas temporales no consolidadas
                var lineasTemp = await _context.InventarioLineasTemp
                    .Where(lt => lt.IdInventario == idInventario && !lt.Consolidado)
                    .ToListAsync();

                if (!lineasTemp.Any())
                {
                    return BadRequest("No hay líneas temporales para consolidar");
                }

                // Agrupar por artículo y ubicación para consolidar
                var lineasConsolidadas = lineasTemp
                    .GroupBy(lt => new { lt.CodigoArticulo, lt.CodigoUbicacion })
                    .Select(g => new
                    {
                        CodigoArticulo = g.Key.CodigoArticulo,
                        CodigoUbicacion = g.Key.CodigoUbicacion,
                        StockContado = g.Sum(lt => lt.CantidadContada)
                    })
                    .ToList();

                // Crear líneas consolidadas
                foreach (var linea in lineasConsolidadas)
                {
                    // Buscar stock teórico
                    var stockActual = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => 
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == inventario.CodigoAlmacen &&
                            s.CodigoArticulo == linea.CodigoArticulo &&
                            s.Ubicacion == linea.CodigoUbicacion);

                    var stockTeorico = stockActual?.UnidadSaldo ?? 0;

                    var lineaConsolidada = new InventarioLineas
                    {
                        IdInventario = idInventario,
                        CodigoArticulo = linea.CodigoArticulo,
                        CodigoUbicacion = linea.CodigoUbicacion,
                        StockTeorico = stockTeorico,
                        StockContado = linea.StockContado,
                        Estado = "CONTADO",
                        UsuarioValidacionId = usuarioValidacionId,
                        FechaValidacion = DateTime.Now
                    };

                    _context.InventarioLineas.Add(lineaConsolidada);
                }

                // Marcar líneas temporales como consolidadas
                foreach (var lineaTemp in lineasTemp)
                {
                    lineaTemp.Consolidado = true;
                    lineaTemp.FechaConsolidacion = DateTime.Now;
                    lineaTemp.UsuarioConsolidacionId = usuarioValidacionId;
                }

                // Guardar cambios
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Inventario {idInventario} consolidado exitosamente. {lineasConsolidadas.Count} líneas consolidadas");
                return Ok(new { mensaje = "Inventario consolidado exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consolidar inventario");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Calcula el número total de combinaciones posibles en el rango especificado
        /// </summary>
        private int CalcularCombinaciones(
            int? pasilloDesde, int? pasilloHasta,
            int? estanteriaDesde, int? estanteriaHasta,
            int? alturaDesde, int? alturaHasta,
            int? posicionDesde, int? posicionHasta)
        {
            if (!pasilloDesde.HasValue && !pasilloHasta.HasValue &&
                !estanteriaDesde.HasValue && !estanteriaHasta.HasValue &&
                !alturaDesde.HasValue && !alturaHasta.HasValue &&
                !posicionDesde.HasValue && !posicionHasta.HasValue)
            {
                return 0;
            }

            if (pasilloDesde.HasValue != pasilloHasta.HasValue)
                throw new ArgumentException("Debe especificar tanto pasilloDesde como pasilloHasta");

            if (estanteriaDesde.HasValue != estanteriaHasta.HasValue)
                throw new ArgumentException("Debe especificar tanto estanteriaDesde como estanteriaHasta");

            if (alturaDesde.HasValue != alturaHasta.HasValue)
                throw new ArgumentException("Debe especificar tanto alturaDesde como alturaHasta");

            if (posicionDesde.HasValue != posicionHasta.HasValue)
                throw new ArgumentException("Debe especificar tanto posicionDesde como posicionHasta");

            var pDesde = pasilloDesde ?? 1;
            var pHasta = pasilloHasta ?? 1;
            var eDesde = estanteriaDesde ?? 1;
            var eHasta = estanteriaHasta ?? 1;
            var aDesde = alturaDesde ?? 1;
            var aHasta = alturaHasta ?? 1;
            var posDesde = posicionDesde ?? 1;
            var posHasta = posicionHasta ?? 1;

            return (pHasta - pDesde + 1) * (eHasta - eDesde + 1) *
                   (aHasta - aDesde + 1) * (posHasta - posDesde + 1);
        }

        /// <summary>
        /// Formatea los rangos de ubicaciones para almacenamiento
        /// </summary>
        private string FormatearRangoUbicaciones(CrearInventarioDto dto)
        {
            return dto.RangoUbicaciones ?? "Rango no especificado";
        }
    }
} 