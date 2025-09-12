using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Stock;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Palet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

                // Filtro por almacén - ACTUALIZADO para soporte multialmacén
                if (!string.IsNullOrWhiteSpace(filtro.CodigoAlmacen))
                {
                    // Filtrar inventarios que incluyan este almacén específico
                    query = query.Where(i => _context.InventarioAlmacenes
                        .Any(ia => ia.IdInventario == i.IdInventario && ia.CodigoAlmacen == filtro.CodigoAlmacen));
                }
                else if (filtro.CodigosAlmacen?.Any() == true)
                {
                    // Filtrar inventarios que incluyan cualquiera de estos almacenes
                    query = query.Where(i => _context.InventarioAlmacenes
                        .Any(ia => ia.IdInventario == i.IdInventario && filtro.CodigosAlmacen.Contains(ia.CodigoAlmacen)));
                }

                // Filtro por estado
                if (!string.IsNullOrWhiteSpace(filtro.EstadoInventario))
                {
                    query = query.Where(i => i.Estado == filtro.EstadoInventario);
                }

                // Filtros de fecha
                if (filtro.FechaDesde.HasValue)
                {
                    // Ajustar para incluir desde el inicio del día
                    var fechaDesde = filtro.FechaDesde.Value.Date;
                    query = query.Where(i => i.FechaCreacion >= fechaDesde);
                }

                if (filtro.FechaHasta.HasValue)
                {
                    // Ajustar para incluir hasta el final del día
                    var fechaHasta = filtro.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.FechaCreacion <= fechaHasta);
                }

                // Obtener inventarios con información de almacenes
                var inventarios = await query
                    .Include(i => i.Almacenes)  // ← NUEVO: Incluir almacenes del inventario
                    .OrderByDescending(i => i.FechaCreacion)
                    .Take(100)
                    .ToListAsync();

                // Crear diccionario de usuarios para mapear IDs a nombres (igual que en Palets)
                var nombreDict = await _context.vUsuariosConNombre
                    .ToDictionaryAsync(x => x.UsuarioId, x => x.NombreOperario);

                // Asignar nombres de usuarios y estadísticas a cada inventario
                foreach (var inventario in inventarios)
                {
                    if (nombreDict.TryGetValue(inventario.UsuarioCreacionId, out var nombre))
                        inventario.UsuarioCreacionNombre = nombre;

                    if (inventario.UsuarioProcesamientoId.HasValue && 
                        nombreDict.TryGetValue(inventario.UsuarioProcesamientoId.Value, out var nombreProcesamiento))
                        inventario.UsuarioProcesamientoNombre = nombreProcesamiento;

                    // Calcular estadísticas de líneas
                    var totalLineas = await _context.InventarioLineasTemp
                        .Where(lt => lt.IdInventario == inventario.IdInventario)
                        .CountAsync();
                    
                    var lineasContadas = await _context.InventarioLineasTemp
                        .Where(lt => lt.IdInventario == inventario.IdInventario && lt.CantidadContada.HasValue)
                        .CountAsync();

                    inventario.TotalLineas = totalLineas;
                    inventario.LineasContadas = lineasContadas;
                }

                // Mapear información de almacenes para cada inventario
                var inventariosDto = inventarios.Select(inv => new 
                {
                    IdInventario = inv.IdInventario,
                    CodigoInventario = inv.CodigoInventario,
                    CodigoEmpresa = inv.CodigoEmpresa,
                    CodigoAlmacen = inv.CodigoAlmacen,
                    RangoUbicaciones = inv.RangoUbicaciones,
                    TipoInventario = inv.TipoInventario,
                    Comentarios = inv.Comentarios,
                    Estado = inv.Estado,
                    UsuarioCreacionId = inv.UsuarioCreacionId,
                    UsuarioCreacionNombre = inv.UsuarioCreacionNombre,
                    UsuarioProcesamientoId = inv.UsuarioProcesamientoId,
                    UsuarioProcesamientoNombre = inv.UsuarioProcesamientoNombre,
                    FechaCreacion = inv.FechaCreacion,
                    FechaCierre = inv.FechaCierre,
                    UsuarioCierreId = inv.UsuarioCierreId,
                    TotalLineas = inv.TotalLineas,
                    LineasContadas = inv.LineasContadas,
                    // Información de conteo
                    ConteoACiegas = inv.ConteoACiegas,
                    // NUEVO: Información de almacenes
                    CodigosAlmacen = inv.Almacenes.Select(a => a.CodigoAlmacen).ToList()
                }).ToList();

                return Ok(inventariosDto);
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

                // Validar almacenes - debe tener al menos uno en CodigoAlmacen o CodigosAlmacen
                if (string.IsNullOrWhiteSpace(dto.CodigoAlmacen) && (!dto.CodigosAlmacen?.Any() ?? true))
                    return BadRequest("Debe especificar al menos un almacén");

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

                string rangoFormateado;
                try
                {
                    rangoFormateado = FormatearRangoUbicaciones(dto);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al formatear rango de ubicaciones");
                    rangoFormateado = "Error al formatear rango";
                }

                // Determinar almacenes a incluir - compatibilidad hacia atrás y nueva funcionalidad
                var almacenesAIncluir = new List<string>();
                if (dto.CodigosAlmacen?.Any() == true)
                {
                    almacenesAIncluir.AddRange(dto.CodigosAlmacen.Distinct());
                }
                else if (!string.IsNullOrWhiteSpace(dto.CodigoAlmacen))
                {
                    almacenesAIncluir.Add(dto.CodigoAlmacen);
                }

                var inventario = new InventarioCabecera
                {
                    IdInventario = Guid.NewGuid(),
                    CodigoInventario = dto.CodigoInventario,
                    CodigoEmpresa = dto.CodigoEmpresa,
                    CodigoAlmacen = almacenesAIncluir.First(), // Primer almacén para compatibilidad
                    RangoUbicaciones = rangoFormateado,
                    TipoInventario = dto.TipoInventario.ToUpper(),
                    Comentarios = dto.Comentarios,
                    Estado = "ABIERTO",
                    UsuarioCreacionId = dto.UsuarioCreacionId,
                    FechaCreacion = DateTime.Now, // Siempre usar la hora del servidor/API
                    ConteoACiegas = dto.IncluirUnidadesCero // true = ciego, false = normal
                };

                _context.InventarioCabecera.Add(inventario);
                
                // Crear relaciones de almacenes
                foreach (var codigoAlmacen in almacenesAIncluir)
                {
                    var relacionAlmacen = new InventarioAlmacenes
                    {
                        IdInventario = inventario.IdInventario,
                        CodigoAlmacen = codigoAlmacen,
                        CodigoEmpresa = dto.CodigoEmpresa
                    };
                    _context.InventarioAlmacenes.Add(relacionAlmacen);
                }
                
                await _context.SaveChangesAsync();

                // Generar líneas temporales automáticamente
                try
                {
                    _logger.LogInformation("Creando inventario con parámetros: IncluirUnidadesCero={IncluirUnidadesCero}, IncluirArticulosConStockCero={IncluirArticulosConStockCero}, IncluirUbicacionesEspeciales={IncluirUbicacionesEspeciales}", 
                    dto.IncluirUnidadesCero, dto.IncluirArticulosConStockCero, dto.IncluirUbicacionesEspeciales);
                var resultadoGeneracion = await GenerarLineasTemporalesInterno(inventario.IdInventario, dto.IncluirUnidadesCero, dto.IncluirArticulosConStockCero, dto.IncluirUbicacionesEspeciales, dto.CodigoArticuloFiltro, dto.ArticuloDesde, dto.ArticuloHasta);
                    if (resultadoGeneracion.Exito)
                    {
                        return Ok(new { 
                            Id = inventario.IdInventario, 
                            Mensaje = "Inventario creado correctamente",
                            LineasGeneradas = resultadoGeneracion.LineasGeneradas,
                            UbicacionesEnRango = resultadoGeneracion.UbicacionesEnRango,
                            StockEncontrado = resultadoGeneracion.StockEncontrado,
                            AlmacenesIncluidos = almacenesAIncluir,
                            EsMultialmacen = almacenesAIncluir.Count > 1
                        });
                    }
                    else
                    {
                        return Ok(new { 
                            Id = inventario.IdInventario, 
                            Mensaje = "Inventario creado correctamente, pero no se pudieron generar líneas temporales",
                            ErrorGeneracion = resultadoGeneracion.Mensaje,
                            AlmacenesIncluidos = almacenesAIncluir,
                            EsMultialmacen = almacenesAIncluir.Count > 1
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Inventario creado pero error al generar líneas temporales");
                    return Ok(new { 
                        Id = inventario.IdInventario, 
                        Mensaje = "Inventario creado correctamente, pero error al generar líneas temporales",
                        AlmacenesIncluidos = almacenesAIncluir,
                        EsMultialmacen = almacenesAIncluir.Count > 1
                    });
                }
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
                    CodigoAlmacen = dto.CodigoAlmacen, // ← AGREGAR ESTA LÍNEA
                    CantidadContada = dto.CantidadContada,
                    UsuarioConteoId = dto.UsuarioConteoId,
                    FechaConteo = DateTime.Now, // Siempre usar la hora del servidor/API
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
                        CodigoAlmacen = lineaTemp.CodigoAlmacen, // Copiar almacén de la línea temporal
                        Partida = lineaTemp.Partida, // Copiar partida de la línea temporal
                        FechaCaducidad = lineaTemp.FechaCaducidad, // Copiar fecha de caducidad de la línea temporal
                        StockTeorico = 0, // Se calculará después
                        StockContado = lineaTemp.CantidadContada,
                        Estado = "CONTADA",
                        Observaciones = lineaTemp.Observaciones
                    };

                    _context.InventarioLineas.Add(linea);
                    lineaTemp.Consolidado = true;
                    lineaTemp.FechaConsolidacion = DateTime.Now; // Siempre usar la hora del servidor/API
                }

                inventario.Estado = "CONSOLIDADO";
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
        /// Cierra un inventario y genera los ajustes correspondientes
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

                if (inventario.Estado != "CONSOLIDADO")
                    return BadRequest("Solo se pueden cerrar inventarios consolidados");

                // Obtener las líneas del inventario para calcular ajustes
                var lineas = await _context.InventarioLineas
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();

                // Generar ajustes para cada línea
                foreach (var linea in lineas)
                {
                    // Calcular la diferencia entre stock contado y stock actual
                    var diferencia = (linea.StockContado ?? 0) - linea.StockActual;
                    
                    // Solo crear ajuste si hay diferencia significativa
                    if (Math.Abs(diferencia) > 0.01m)
                    {
                        var ajuste = new InventarioAjustes
                        {
                            IdAjuste = Guid.NewGuid(),
                            IdInventario = idInventario,
                            CodigoArticulo = linea.CodigoArticulo,
                            CodigoUbicacion = linea.CodigoUbicacion,
                            Diferencia = diferencia,
                            UsuarioId = inventario.UsuarioCreacionId,
                            Fecha = DateTime.Now,
                            IdConteo = Guid.Empty,
                            CodigoEmpresa = inventario.CodigoEmpresa,
                            CodigoAlmacen = linea.CodigoAlmacen ?? inventario.CodigoAlmacen,
                            Estado = "PENDIENTE_ERP",
                            FechaCaducidad = linea.FechaCaducidad,
                            Partida = linea.Partida
                        };

                        _context.InventarioAjustes.Add(ajuste);
                    }
                }

                // Cambiar el estado a CERRADO
                inventario.Estado = "CERRADO";
                inventario.FechaCierre = DateTime.Now;
                inventario.UsuarioCierreId = inventario.UsuarioCreacionId;

                // Guardar todos los cambios (ajustes + cierre)
                await _context.SaveChangesAsync();

                return Ok(new { Mensaje = "Inventario cerrado correctamente con ajustes generados" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar inventario {IdInventario}", idInventario);
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
                // Obtener el tipo de inventario para determinar si incluir stock 0
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);
                

                
                var lineas = inventario?.TipoInventario == "TOTAL" 
                    ? await _context.InventarioLineas
                        .Where(l => l.IdInventario == idInventario)
                        .ToListAsync()
                    : await _context.InventarioLineas
                        .Where(l => l.IdInventario == idInventario && l.StockActual > 0)
                        .ToListAsync();
                


                // Obtener descripciones de artículos con manejo de errores
                Dictionary<string, string> articulos = new();
                try
                {
                    var codigosArticulos = lineas.Select(l => l.CodigoArticulo).Distinct().ToList();
                    if (codigosArticulos.Any())
                    {
                        // Consulta filtrada por empresa para obtener solo los artículos relevantes
                        var articulosSage = await _sageDbContext.Articulos
                            .Where(a => a.CodigoEmpresa == inventario.CodigoEmpresa)
                            .Select(a => new { a.CodigoArticulo, a.DescripcionArticulo })
                            .ToListAsync();
                            
                        articulos = articulosSage
                            .Where(a => codigosArticulos.Contains(a.CodigoArticulo))
                            .ToDictionary(
                                a => a.CodigoArticulo, 
                                a => a.DescripcionArticulo ?? ""
                            );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron obtener descripciones de artículos de Sage");
                    // Continuar sin descripciones
                }

                // Obtener información de palets para cada línea
                
                var paletsInfo = new Dictionary<string, List<object>>();
                if (inventario != null)
                {
                    paletsInfo = await ObtenerInformacionPaletsAsync(lineas.Select(l => new InventarioLineasTemp
                    {
                        CodigoArticulo = l.CodigoArticulo,
                        CodigoUbicacion = l.CodigoUbicacion,
                        Partida = l.Partida
                    }).ToList(), inventario.CodigoEmpresa);
                }

                // Crear resultado con descripciones usando DTO
                var lineasDto = lineas.Select(l => new LineaInventarioDto
                {
                    CodigoArticulo = l.CodigoArticulo,
                    DescripcionArticulo = articulos.GetValueOrDefault(l.CodigoArticulo, ""),
                    CodigoUbicacion = l.CodigoUbicacion,
                    Partida = l.Partida ?? "",
                    FechaCaducidad = l.FechaCaducidad,
                    StockActual = l.StockActual,
                    StockContado = l.StockContado ?? 0,
                    StockTeorico = l.StockTeorico,
                    AjusteFinal = l.AjusteFinal,
                    Estado = l.Estado,
                    // Información de palets
                    Palets = paletsInfo.GetValueOrDefault($"{l.CodigoArticulo}_{l.CodigoUbicacion}_{l.Partida ?? ""}", new List<object>())
                        .Cast<PaletDetalleDto>()
                        .ToList()
                }).ToList();

                return Ok(lineasDto);
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
                    .Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
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
                    .Where(p => p.CodigoEmpresa == codigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                    return BadRequest("Sin ejercicio");

                // Obtener ubicaciones del almacén (excluyendo las obsoletas)
                var ubicaciones = await _storageContext.Ubicaciones
                    .Where(u => u.CodigoAlmacen == codigoAlmacen &&
                               u.Ubicacion.StartsWith("UB") &&
                               u.Obsoleta == 0) // Excluir ubicaciones obsoletas
                    .Select(u => u.Ubicacion)
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
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                {
                    _logger.LogError("No se encontró ejercicio válido para la empresa");
                    return BadRequest("Sin ejercicio válido");
                }

                // === NUEVO: Validación de límites por artículo antes de guardar ===
                foreach (var articulo in conteo.Articulos)
                {
                    var (acumUnid, acumEur) = await CalcularDiferenciasDiariasPorArticuloAsync(
                        articulo.UsuarioConteo,
                        articulo.CodigoArticulo,
                        conteo.IdInventario,
                        inventario.CodigoEmpresa);

                    // Límites del operario desde SAGE (si no hay, 0 => sin límite)
                    var limEuros = await _sageDbContext.Operarios
                        .Where(o => o.Id == articulo.UsuarioConteo)
                        .Select(o => o.MRH_LimiteInventarioEuros)
                        .FirstOrDefaultAsync() ?? 0m;

                    var limUnidades = await _sageDbContext.Operarios
                        .Where(o => o.Id == articulo.UsuarioConteo)
                        .Select(o => o.MRH_LimiteInventarioUnidades)
                        .FirstOrDefaultAsync() ?? 0m;

                    // Diferencia que se intenta registrar ahora
                    var stockActual = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s =>
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == articulo.CodigoAlmacen && // ← CORREGIDO: Usar almacén del artículo
                            s.CodigoArticulo == articulo.CodigoArticulo &&
                            s.Ubicacion == articulo.CodigoUbicacion &&
                            (s.Partida == articulo.Partida || (s.Partida == null && articulo.Partida == null)) &&
                            (s.FechaCaducidad == articulo.FechaCaducidad || (s.FechaCaducidad == null && articulo.FechaCaducidad == null)));

                    var diferenciaNueva = Math.Abs((articulo.CantidadInventario) - (stockActual?.UnidadSaldo ?? 0));

                    // Solo validar límites si hay diferencia real (no 0 absoluto)
                    if (diferenciaNueva > 0)
                    {
                        if (limEuros > 0)
                        {
                            var precioMedio = await _sageDbContext.AcumuladoStock
                                .Where(a => a.CodigoEmpresa == inventario.CodigoEmpresa && a.CodigoArticulo == articulo.CodigoArticulo)
                                .OrderByDescending(a => a.Ejercicio)
                                .Select(a => a.PrecioMedio)
                                .FirstOrDefaultAsync() ?? 0m;

                            var totalEuros = acumEur + (diferenciaNueva * precioMedio);
                            if (totalEuros > limEuros)
                            {
                                return BadRequest($"Límite diario de euros superado para el artículo {articulo.CodigoArticulo}.");
                            }
                        }

                        if (limUnidades > 0)
                        {
                            var totalUnidades = acumUnid + diferenciaNueva;
                            if (totalUnidades > limUnidades)
                            {
                                return BadRequest($"Límite diario de unidades superado para el artículo {articulo.CodigoArticulo}.");
                            }
                        }
                    }
                }

                // Procesar cada artículo del conteo
                foreach (var articulo in conteo.Articulos)
                {
                    // Buscar línea temporal existente para este artículo/ubicación/partida/fecha
                    var lineaTempExistente = await _context.InventarioLineasTemp
                        .FirstOrDefaultAsync(lt => 
                            lt.IdInventario == inventario.IdInventario &&
                            lt.CodigoArticulo == articulo.CodigoArticulo &&
                            lt.CodigoUbicacion == articulo.CodigoUbicacion &&
                            (lt.Partida == articulo.Partida || (lt.Partida == null && articulo.Partida == null)) &&
                            (lt.FechaCaducidad == articulo.FechaCaducidad || (lt.FechaCaducidad == null && articulo.FechaCaducidad == null)) &&
                            !lt.Consolidado);

                    if (lineaTempExistente != null)
                    {
                        // Actualizar línea existente
                        lineaTempExistente.CantidadContada = articulo.CantidadInventario;
                        lineaTempExistente.UsuarioConteoId = articulo.UsuarioConteo;
                        lineaTempExistente.FechaConteo = DateTime.Now; // Siempre usar la hora del servidor/API
                        
                        _logger.LogInformation($"Línea temporal actualizada: {articulo.CodigoArticulo} en {articulo.CodigoUbicacion}. " +
                                              $"StockActual: {lineaTempExistente.StockActual}, CantidadContada: {articulo.CantidadInventario}");
                    }
                    else
                    {
                        // Buscar stock actual del artículo para nueva línea
                        var stockActual = await _storageContext.AcumuladoStockUbicacion
                            .FirstOrDefaultAsync(s => 
                                s.CodigoEmpresa == inventario.CodigoEmpresa &&
                                s.Ejercicio == ejercicio &&
                                s.CodigoAlmacen == articulo.CodigoAlmacen && // ← CORREGIDO: Usar almacén del artículo
                                s.CodigoArticulo == articulo.CodigoArticulo &&
                                s.Ubicacion == articulo.CodigoUbicacion &&
                                (s.Partida == articulo.Partida || (s.Partida == null && articulo.Partida == null)) &&
                                (s.FechaCaducidad == articulo.FechaCaducidad || (s.FechaCaducidad == null && articulo.FechaCaducidad == null)));

                        // Crear nueva línea temporal solo si no existe
                        var nuevaLineaTemp = new InventarioLineasTemp
                        {
                            IdInventario = inventario.IdInventario,
                            CodigoArticulo = articulo.CodigoArticulo,
                            CodigoUbicacion = articulo.CodigoUbicacion,
                            CodigoAlmacen = articulo.CodigoAlmacen, // ← AGREGAR ESTA LÍNEA
                            CantidadContada = articulo.CantidadInventario,
                            StockActual = stockActual?.UnidadSaldo ?? 0,
                            Partida = stockActual?.Partida,
                            FechaCaducidad = stockActual?.FechaCaducidad,
                            UsuarioConteoId = articulo.UsuarioConteo,
                            FechaConteo = DateTime.Now, // Siempre usar la hora del servidor/API
                            Consolidado = false
                        };

                        _context.InventarioLineasTemp.Add(nuevaLineaTemp);
                        
                        _logger.LogInformation($"Nueva línea temporal creada: {articulo.CodigoArticulo} en {articulo.CodigoUbicacion}. " +
                                              $"StockActual: {nuevaLineaTemp.StockActual}, CantidadContada: {articulo.CantidadInventario}");
                    }
                }

                // NOTA: El inventario permanece en estado "ABIERTO" durante todo el proceso de conteo
                // Solo cambiará a "CONSOLIDADO" cuando se consolide explícitamente
                _logger.LogInformation($"Estado del inventario {conteo.IdInventario} permanece como: {inventario.Estado}");

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
        /// Calcula diferencias diarias por artículo para un operario, excluyendo un inventario dado
        /// Suma tanto líneas temporales (no consolidadas) como líneas definitivas consolidadas del día.
        /// </summary>
        private async Task<(decimal unidades, decimal euros)> CalcularDiferenciasDiariasPorArticuloAsync(
            int operarioId, string codigoArticulo, Guid inventarioAExcluir, short codigoEmpresa)
        {
            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);

            decimal totalUnidades = 0m;
            decimal totalEuros = 0m;

            // Precio medio (si no hay o es 0, aceptamos 0€ como pediste)
            var precioMedio = await _sageDbContext.AcumuladoStock
                .Where(a => a.CodigoEmpresa == codigoEmpresa && a.CodigoArticulo == codigoArticulo)
                .OrderByDescending(a => a.Ejercicio)
                .Select(a => a.PrecioMedio)
                .FirstOrDefaultAsync() ?? 0m;

            // InventarioLineasTemp del día (no consolidadas) distinto del actual
            var temp = await _context.InventarioLineasTemp
                .Where(lt => lt.UsuarioConteoId == operarioId &&
                             lt.CodigoArticulo == codigoArticulo &&
                             lt.IdInventario != inventarioAExcluir &&
                             lt.FechaConteo >= hoy && lt.FechaConteo < manana &&
                             !lt.Consolidado &&
                             lt.CantidadContada.HasValue)
                .Select(lt => new { lt.CantidadContada, lt.StockActual })
                .ToListAsync();

            foreach (var l in temp)
            {
                var diff = Math.Abs((l.CantidadContada ?? 0) - (l.StockActual));
                Console.WriteLine($"🔍 TEMP CALC: Contado={l.CantidadContada}, Stock={l.StockActual}, Diff={diff}");
                if (diff > 0.01m)
                {
                    totalUnidades += diff;
                    totalEuros += diff * precioMedio;
                }
            }

            // InventarioLineas del día (consolidadas) distinto del actual
            var finales = await _context.InventarioLineas
                .Where(l => l.UsuarioValidacionId == operarioId &&
                            l.CodigoArticulo == codigoArticulo &&
                            l.IdInventario != inventarioAExcluir &&
                            l.FechaValidacion >= hoy && l.FechaValidacion < manana)
                .Select(l => new { l.StockContado, l.StockActual })
                .ToListAsync();

            foreach (var l in finales)
            {
                var diff = Math.Abs((l.StockContado ?? 0m) - l.StockActual);
                Console.WriteLine($"🔍 FINAL CALC: Contado={l.StockContado}, Stock={l.StockActual}, Diff={diff}");
                if (diff > 0.01m)
                {
                    totalUnidades += diff;
                    totalEuros += diff * precioMedio;
                }
            }

            return (totalUnidades, totalEuros);
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
                        FechaValidacion = DateTime.Now // Siempre usar la hora del servidor/API
                    };

                    _context.InventarioLineas.Add(lineaConsolidada);
                }

                // Marcar líneas temporales como consolidadas
                foreach (var lineaTemp in lineasTemp)
                {
                    lineaTemp.Consolidado = true;
                    lineaTemp.FechaConsolidacion = DateTime.Now; // Siempre usar la hora del servidor/API
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
        /// POST /api/Inventario/consolidar-inteligente/{idInventario}
        /// Consolida las líneas temporales de un inventario (método simplificado)
        /// </summary>
        [HttpPost("consolidar-inteligente/{idInventario}")]
        public async Task<IActionResult> ConsolidarInventarioInteligente(Guid idInventario, [FromQuery] int usuarioValidacionId)
        {
            try
            {
                _logger.LogInformation($"Consolidando inventario {idInventario}");

                // Validar que el inventario existe y está en estado válido
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);

                if (inventario == null)
                {
                    _logger.LogWarning($"Inventario {idInventario} no encontrado");
                    return NotFound("Inventario no encontrado");
                }

                if (inventario.Estado != "ABIERTO" && inventario.Estado != "EN_CONTEO")
                {
                    _logger.LogWarning($"Inventario {idInventario} no está en estado válido para consolidar. Estado actual: {inventario.Estado}");
                    return BadRequest("El inventario debe estar abierto o en conteo para consolidar");
                }

                // Obtener solo las líneas temporales NO consolidadas del inventario
                var lineasTemp = await _context.InventarioLineasTemp
                    .Where(lt => lt.IdInventario == idInventario && !lt.Consolidado)
                    .ToListAsync();

                if (!lineasTemp.Any())
                {
                    return BadRequest("No hay líneas temporales para consolidar");
                }



                // ELIMINAR todas las líneas finales existentes para evitar duplicados
                var lineasExistentes = await _context.InventarioLineas
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();
                
                _context.InventarioLineas.RemoveRange(lineasExistentes);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Eliminadas {lineasExistentes.Count} líneas finales existentes para evitar duplicados");

                // Obtener ejercicio actual una sola vez para todo el método
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                // Crear líneas definitivas para todas las líneas temporales
                foreach (var lineaTemp in lineasTemp)
                {
                    // Stock teórico: el que había cuando se creó el inventario
                    var stockTeorico = lineaTemp.StockActual;
                    
                    // Stock actual: consultar el stock actual del sistema al momento de consolidar
                    // 🔷 CORREGIDO: Usar el almacén de cada línea individual en lugar del almacén del inventario
                    var stockActualSistema = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => 
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == lineaTemp.CodigoAlmacen && // ← CORREGIDO: Usar almacén de la línea
                            s.CodigoArticulo == lineaTemp.CodigoArticulo &&
                            s.Ubicacion == lineaTemp.CodigoUbicacion &&
                            s.Partida == lineaTemp.Partida);
                    
                    var stockActual = stockActualSistema?.UnidadSaldo ?? 0;
                    
                    // Stock contado por el usuario
                    var stockContado = lineaTemp.CantidadContada ?? lineaTemp.StockActual;
                    
                    // 🔷 CORREGIDO: El ajuste final debe ser sobre StockActual, no sobre StockTeorico
                    // StockActual = lo que hay actualmente en el sistema
                    // StockContado = lo que contó el usuario
                    // AjusteFinal = diferencia entre lo que hay y lo que debería haber
                    var ajusteFinal = stockContado - stockActual;
                    
                    var nuevaLinea = new InventarioLineas
                    {
                        IdInventario = idInventario,
                        CodigoArticulo = lineaTemp.CodigoArticulo,
                        CodigoUbicacion = lineaTemp.CodigoUbicacion,
                        // 🔷 NUEVO: Preservar el almacén de cada línea individual
                        CodigoAlmacen = lineaTemp.CodigoAlmacen,
                        StockTeorico = stockTeorico, // Stock cuando se creó el inventario
                        StockActual = stockActual, // Stock actual del sistema al consolidar
                        StockContado = stockContado, // Lo que contó el usuario
                        AjusteFinal = ajusteFinal, // 🔷 CORREGIDO: StockContado - StockActual
                        Estado = "CONTADA",
                        Partida = lineaTemp.Partida,
                        FechaCaducidad = lineaTemp.FechaCaducidad,
                        UsuarioValidacionId = usuarioValidacionId,
                        FechaValidacion = DateTime.Now, // Siempre usar la hora del servidor/API
                        Observaciones = lineaTemp.Observaciones
                    };
                    
                    _context.InventarioLineas.Add(nuevaLinea);
                    _logger.LogInformation($"Creando línea: Artículo={lineaTemp.CodigoArticulo}, Almacén={lineaTemp.CodigoAlmacen}, StockTeórico={stockTeorico}, StockActual={stockActual}, StockContado={stockContado}, AjusteFinal={ajusteFinal} (StockContado - StockActual)");
                    
                    // Marcar línea temporal como consolidada
                    lineaTemp.Consolidado = true;
                    lineaTemp.FechaConsolidacion = DateTime.Now; // Siempre usar la hora del servidor/API
                    lineaTemp.UsuarioConsolidacionId = usuarioValidacionId;
                }

                // Detectar líneas con diferencias significativas entre el stock al crear y el stock actual
                var lineasConDiferencias = new List<object>();
                var tolerancia = 0.01m; // Tolerancia para diferencias de redondeo

                foreach (var lineaTemp in lineasTemp)
                {
                    // 🔷 CORREGIDO: Usar el almacén de cada línea individual en lugar del almacén del inventario
                    var stockActualSistema = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => 
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == lineaTemp.CodigoAlmacen && // ← CORREGIDO: Usar almacén de la línea
                            s.CodigoArticulo == lineaTemp.CodigoArticulo &&
                            s.Ubicacion == lineaTemp.CodigoUbicacion);

                    var stockActual = stockActualSistema?.UnidadSaldo ?? 0;
                    var stockAlCrear = lineaTemp.StockActual; // Stock cuando se creó el inventario
                    var diferencia = Math.Abs(stockActual - stockAlCrear);

                    if (diferencia > 0m) // Sin tolerancia, cualquier diferencia cuenta
                    {
                        lineasConDiferencias.Add(new
                        {
                            CodigoArticulo = lineaTemp.CodigoArticulo,
                            CodigoAlmacen = lineaTemp.CodigoAlmacen, // ← NUEVO: Incluir almacén en las diferencias
                            CodigoUbicacion = lineaTemp.CodigoUbicacion,
                            StockAlCrear = stockAlCrear,
                            StockActual = stockActual,
                            Diferencia = diferencia
                        });
                    }
                }

                // Cambiar estado a CONSOLIDADO
                inventario.Estado = "CONSOLIDADO";

                // Guardar cambios
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Inventario {idInventario} consolidado exitosamente. {lineasTemp.Count} líneas procesadas, {lineasConDiferencias.Count} con diferencias");

                return Ok(new { 
                    mensaje = "Inventario consolidado correctamente",
                    totalProcesadas = lineasTemp.Count,
                    tieneAdvertencias = lineasConDiferencias.Count > 0,
                    lineasConStockCambiado = lineasConDiferencias
                });
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
        /// Parsea el string de rango de ubicaciones y retorna un array de rangos
        /// Formato esperado: "P:1-3,E:1-5,A:1-3,O:1-3"
        /// Si no hay rango, retorna null para indicar "todas las ubicaciones"
        /// </summary>
        private (int desde, int hasta)[]? ParsearRangoUbicaciones(string rango)
        {
            if (string.IsNullOrWhiteSpace(rango) || rango == "Rango no especificado")
                return null; // Indica "todas las ubicaciones"

            var rangos = new (int desde, int hasta)[4]; // P, E, A, O
            var partes = rango.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var parte in partes)
            {
                var trimParte = parte.Trim();
                if (trimParte.StartsWith("P:"))
                {
                    var valores = trimParte.Substring(2).Split('-');
                    if (valores.Length == 2 && int.TryParse(valores[0], out var desde) && int.TryParse(valores[1], out var hasta))
                        rangos[0] = (desde, hasta);
                }
                else if (trimParte.StartsWith("E:"))
                {
                    var valores = trimParte.Substring(2).Split('-');
                    if (valores.Length == 2 && int.TryParse(valores[0], out var desde) && int.TryParse(valores[1], out var hasta))
                        rangos[1] = (desde, hasta);
                }
                else if (trimParte.StartsWith("A:"))
                {
                    var valores = trimParte.Substring(2).Split('-');
                    if (valores.Length == 2 && int.TryParse(valores[0], out var desde) && int.TryParse(valores[1], out var hasta))
                        rangos[2] = (desde, hasta);
                }
                else if (trimParte.StartsWith("O:"))
                {
                    var valores = trimParte.Substring(2).Split('-');
                    if (valores.Length == 2 && int.TryParse(valores[0], out var desde) && int.TryParse(valores[1], out var hasta))
                        rangos[3] = (desde, hasta);
                }
            }

            return rangos;
        }

        /// <summary>
        /// Obtiene ubicaciones reales que existen en la BD y caen dentro del rango especificado
        /// Si rangos es null, obtiene todas las ubicaciones del almacén
        /// </summary>
        private async Task<List<string>> ObtenerUbicacionesEnRangoAsync(
            short codigoEmpresa, 
            string codigoAlmacen, 
            (int desde, int hasta)[]? rangos)
        {
            var query = _context.Ubicaciones
                .Where(u => u.CodigoEmpresa == codigoEmpresa &&
                            u.CodigoAlmacen == codigoAlmacen &&
                            u.Obsoleta == 0);

            // Si hay rangos específicos, aplicar filtros solo para los que están especificados
            if (rangos != null)
            {
                // Solo aplicar filtro de pasillo si está especificado (desde > 0)
                if (rangos[0].desde > 0)
                {
                    query = query.Where(u => u.Pasillo >= rangos[0].desde && u.Pasillo <= rangos[0].hasta);
                }

                // Solo aplicar filtro de estantería si está especificado (desde > 0)
                if (rangos[1].desde > 0)
                {
                    query = query.Where(u => u.Estanteria >= rangos[1].desde && u.Estanteria <= rangos[1].hasta);
                }

                // Solo aplicar filtro de altura si está especificado (desde > 0)
                if (rangos[2].desde > 0)
                {
                    query = query.Where(u => u.Altura >= rangos[2].desde && u.Altura <= rangos[2].hasta);
                }

                // Solo aplicar filtro de posición si está especificado (desde > 0)
                if (rangos[3].desde > 0)
                {
                    query = query.Where(u => u.Posicion >= rangos[3].desde && u.Posicion <= rangos[3].hasta);
                }
            }
            // Si no hay rangos, obtener todas las ubicaciones del almacén

            var ubicaciones = await query
                .Select(u => u.CodigoUbicacion)
                .ToListAsync();

            return ubicaciones;
        }

        /// <summary>
        /// Método interno para generar líneas temporales (usado por CrearInventario)
        /// </summary>
                private async Task<(bool Exito, int LineasGeneradas, int UbicacionesEnRango, int StockEncontrado, string Mensaje)>
            GenerarLineasTemporalesInterno(Guid idInventario, bool incluirUnidadesCero = false, bool incluirArticulosConStockCero = false, bool incluirUbicacionesEspeciales = false, string? codigoArticuloFiltro = null, string? articuloDesde = null, string? articuloHasta = null)
        {
            try
            {
                _logger.LogInformation("Generando líneas temporales para inventario {IdInventario}, incluirUnidadesCero: {IncluirUnidadesCero}, incluirArticulosConStockCero: {IncluirArticulosConStockCero}, incluirUbicacionesEspeciales: {IncluirUbicacionesEspeciales}, codigoArticuloFiltro: {CodigoArticuloFiltro}", 
                    idInventario, incluirUnidadesCero, incluirArticulosConStockCero, incluirUbicacionesEspeciales, codigoArticuloFiltro ?? "null");
                
                // 1. Obtener inventario con sus almacenes
                var inventario = await _context.InventarioCabecera
                    .Include(i => i.Almacenes)
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);
                
                if (inventario == null) 
                    return (false, 0, 0, 0, "Inventario no encontrado");

                // 2. Obtener almacenes del inventario
                var almacenesInventario = inventario.Almacenes.Select(a => a.CodigoAlmacen).ToList();
                if (!almacenesInventario.Any())
                {
                    // Compatibilidad hacia atrás: si no hay relaciones, usar el almacén de la cabecera
                    almacenesInventario.Add(inventario.CodigoAlmacen);
                }

                _logger.LogInformation("Inventario {IdInventario} incluye {NumAlmacenes} almacenes: {Almacenes}", 
                    idInventario, almacenesInventario.Count, string.Join(", ", almacenesInventario));

                // 3. Parsear rango de ubicaciones (aplica a todos los almacenes)
                var rangos = ParsearRangoUbicaciones(inventario.RangoUbicaciones);

                // 4. Procesar cada almacén del inventario
                var stockActualTotal = new List<AcumuladoStockUbicacion>();
                var totalUbicacionesEnRango = 0;

                foreach (var codigoAlmacen in almacenesInventario)
                {
                    _logger.LogInformation("Procesando almacén {CodigoAlmacen} para inventario {IdInventario}", 
                        codigoAlmacen, idInventario);

                    // 4.1. Obtener ejercicio para este almacén específico
                    var ejercicio = await _sageDbContext.Periodos
                        .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                        .OrderByDescending(p => p.Fechainicio)
                        .Select(p => p.Ejercicio)
                        .FirstOrDefaultAsync();

                    if (ejercicio == 0)
                    {
                        _logger.LogWarning("No se encontró ejercicio válido para almacén {CodigoAlmacen}", codigoAlmacen);
                        continue; // Saltar este almacén y continuar con el siguiente
                    }

                    // 4.2. Obtener ubicaciones reales en rango para este almacén
                    var ubicacionesEnRangoAlmacen = await ObtenerUbicacionesEnRangoAsync(
                        inventario.CodigoEmpresa, 
                        codigoAlmacen, 
                        rangos);

                    totalUbicacionesEnRango += ubicacionesEnRangoAlmacen.Count;

                    if (ubicacionesEnRangoAlmacen.Count == 0)
                    {
                        _logger.LogWarning("No se encontraron ubicaciones en rango para almacén {CodigoAlmacen}", codigoAlmacen);
                        continue; // Saltar este almacén si no tiene ubicaciones válidas
                    }

                    // 4.3. Obtener stock actual para las ubicaciones de este almacén
                    var stockAlmacen = await ObtenerStockParaInventario(
                        inventario.CodigoEmpresa, 
                        ejercicio, 
                        codigoAlmacen, 
                        ubicacionesEnRangoAlmacen, 
                        incluirArticulosConStockCero,
                        codigoArticuloFiltro,
                        articuloDesde,
                        articuloHasta);

                    stockActualTotal.AddRange(stockAlmacen);

                    // 4.4. Si se incluyen ubicaciones especiales, agregarlas para este almacén
                    if (incluirUbicacionesEspeciales)
                    {
                        var ubicacionesEspeciales = await ObtenerStockParaInventarioUbicacionesEspeciales(
                            inventario.CodigoEmpresa, 
                            ejercicio, 
                            codigoAlmacen, 
                            incluirArticulosConStockCero,
                            codigoArticuloFiltro,
                            articuloDesde,
                            articuloHasta);

                        stockActualTotal.AddRange(ubicacionesEspeciales);
                    }

                    _logger.LogInformation("Almacén {CodigoAlmacen}: {NumUbicaciones} ubicaciones, {NumRegistrosStock} registros de stock", 
                        codigoAlmacen, ubicacionesEnRangoAlmacen.Count, stockAlmacen.Count);
                }

                // Verificar que se encontró stock en al menos un almacén
                if (!stockActualTotal.Any())
                    return (false, 0, totalUbicacionesEnRango, 0, "No se encontró stock en ninguno de los almacenes especificados");

                // 5. Crear líneas temporales (agrupando para evitar duplicados entre almacenes)
                var lineasTemporales = new List<InventarioLineasTemp>();
                
                // Agrupar por artículo, ALMACÉN, ubicación, partida y fecha para evitar duplicados
                // IMPORTANTE: Incluir CodigoAlmacen en el grouping para inventarios multialmacén
                var stockAgrupado = stockActualTotal
                    .GroupBy(s => new { 
                        s.CodigoArticulo, 
                        s.CodigoAlmacen,  // ← NUEVO: Agrupar también por almacén
                        s.Ubicacion, 
                        s.Partida, 
                        s.FechaCaducidad 
                    })
                    .Select(g => new
                    {
                        CodigoArticulo = g.Key.CodigoArticulo,
                        CodigoAlmacen = g.Key.CodigoAlmacen,  // ← NUEVO: Incluir almacén en el resultado
                        Ubicacion = g.Key.Ubicacion,
                        Partida = g.Key.Partida,
                        FechaCaducidad = g.Key.FechaCaducidad,
                        UnidadSaldo = g.Sum(s => s.UnidadSaldo ?? 0), // Sumar stock si hay múltiples registros
                        RegistrosOriginales = g.Count() // Contar cuántos registros originales había
                    })
                    .ToList();
                

                
                foreach (var stock in stockAgrupado)
                {
                    // NUEVO: Las líneas se crean SIN contar (CantidadContada = null)
                    // El progreso se basará en líneas donde el usuario realmente haya contado
                    
                    _logger.LogInformation("Artículo {CodigoArticulo} en {CodigoAlmacen}/{Ubicacion}: Creando línea temporal sin contar (CantidadContada = null), stockActual={StockActual}", 
                        stock.CodigoArticulo, stock.CodigoAlmacen, stock.Ubicacion, stock.UnidadSaldo);

                    var nuevaLinea = new InventarioLineasTemp
                    {
                        IdInventario = idInventario,
                        CodigoArticulo = stock.CodigoArticulo ?? "",
                        CodigoUbicacion = stock.Ubicacion ?? "",
                        CodigoAlmacen = stock.CodigoAlmacen, // ← AGREGAR ESTA LÍNEA
                        Partida = stock.Partida,
                        FechaCaducidad = stock.FechaCaducidad,
                        CantidadContada = null,
                        StockActual = stock.UnidadSaldo,
                        UsuarioConteoId = inventario.UsuarioCreacionId,
                        FechaConteo = DateTime.Now, // Siempre usar la hora del servidor/API
                        Consolidado = false
                    };

                    lineasTemporales.Add(nuevaLinea);
                }

                // 7. Guardar líneas temporales
                await _context.InventarioLineasTemp.AddRangeAsync(lineasTemporales);
                await _context.SaveChangesAsync();

                // Verificar que las líneas se guardaron correctamente
                var lineasGuardadas = await _context.InventarioLineasTemp
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();
                


                return (true, lineasTemporales.Count, totalUbicacionesEnRango, stockActualTotal.Count, "Líneas generadas correctamente para inventario multialmacén");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar líneas temporales del inventario {IdInventario}", idInventario);
                return (false, 0, 0, 0, $"Error interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene las líneas temporales de un inventario con información adicional
        /// </summary>
        [HttpGet("lineas-temporales/{idInventario}")]
        public async Task<IActionResult> ObtenerLineasTemporales(Guid idInventario)
        {
            try
            {
                // Obtener el inventario para el código de almacén
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);
                
                if (inventario == null)
                    return NotFound("Inventario no encontrado");

                var lineas = await _context.InventarioLineasTemp
                    .Where(l => l.IdInventario == idInventario && !l.Consolidado)
                    .OrderBy(l => l.CodigoAlmacen)        // ← CAMBIAR: Ordenar por almacén primero
                    .ThenBy(l => l.CodigoUbicacion)       // ← CAMBIAR: Luego por ubicación
                    .ThenBy(l => l.CodigoArticulo)        // ← MANTENER: Finalmente por artículo
                    .ToListAsync();

                // Obtener descripciones de artículos con manejo de errores
                Dictionary<string, string> articulos = new();
                try
                {
                    var codigosArticulos = lineas.Select(l => l.CodigoArticulo).Distinct().ToList();
                    if (codigosArticulos.Any())
                    {
                        // Consulta filtrada por empresa para obtener solo los artículos relevantes
                        var articulosSage = await _sageDbContext.Articulos
                            .Where(a => a.CodigoEmpresa == inventario.CodigoEmpresa)
                            .Select(a => new { a.CodigoArticulo, a.DescripcionArticulo })
                            .ToListAsync();
                            
                        articulos = articulosSage
                            .Where(a => codigosArticulos.Contains(a.CodigoArticulo))
                            .ToDictionary(
                                a => a.CodigoArticulo, 
                                a => a.DescripcionArticulo ?? ""
                            );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron obtener descripciones de artículos de Sage");
                    // Continuar sin descripciones
                }



                // === NUEVO: Obtener información de palets para cada línea ===
                var paletsInfo = await ObtenerInformacionPaletsAsync(lineas, inventario.CodigoEmpresa);

                // Mapear a DTO con información completa
                var lineasDto = lineas.Select(l => new SGA_Api.Models.Inventario.LineaTemporalInventarioDto
                {
                    IdTemp = l.IdTemp,
                    IdInventario = l.IdInventario,
                    CodigoArticulo = l.CodigoArticulo,
                    DescripcionArticulo = articulos.GetValueOrDefault(l.CodigoArticulo, ""),
                    CodigoUbicacion = l.CodigoUbicacion,
                    CodigoAlmacen = l.CodigoAlmacen ?? "", // ← AGREGAR ESTA LÍNEA
                    Partida = l.Partida ?? "",
                    FechaCaducidad = l.FechaCaducidad,
                    CantidadContada = l.CantidadContada,
                    StockActual = l.StockActual,
                    UsuarioConteoId = l.UsuarioConteoId,
                    FechaConteo = l.FechaConteo,
                    Observaciones = l.Observaciones,
                    Consolidado = l.Consolidado,
                    FechaConsolidacion = l.FechaConsolidacion,
                    UsuarioConsolidacionId = l.UsuarioConsolidacionId,
                    Palets = paletsInfo.GetValueOrDefault($"{l.CodigoArticulo}_{l.CodigoUbicacion}_{l.Partida ?? ""}", new List<object>())
                        .Cast<PaletDetalleDto>()
                        .ToList()
                }).ToList();

                return Ok(lineasDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener líneas temporales del inventario {IdInventario}", idInventario);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene la información de palets para las líneas de inventario
        /// </summary>
        private async Task<Dictionary<string, List<object>>> ObtenerInformacionPaletsAsync(
            List<InventarioLineasTemp> lineas, 
            short codigoEmpresa)
        {
            var resultado = new Dictionary<string, List<object>>();

            try
            {
                // Obtener todas las líneas de palets (definitivas y temporales) que coincidan con las líneas de inventario
                var lineasPalets = await _context.PaletLineas
                    .Where(pl => pl.CodigoEmpresa == codigoEmpresa)
                    .Select(pl => new
                    {
                        pl.PaletId,
                        pl.CodigoArticulo,
                        pl.Ubicacion,
                        pl.Lote,
                        pl.Cantidad,
                        pl.CodigoAlmacen
                    })
                    .ToListAsync();

                var lineasTempPalets = await _context.TempPaletLineas
                    .Where(tpl => tpl.CodigoEmpresa == codigoEmpresa && !tpl.Procesada)
                    .Select(tpl => new
                    {
                        tpl.PaletId,
                        tpl.CodigoArticulo,
                        tpl.Ubicacion,
                        tpl.Lote,
                        tpl.Cantidad,
                        tpl.CodigoAlmacen
                    })
                    .ToListAsync();

                // Combinar líneas definitivas y temporales
                var todasLasLineas = lineasPalets.Concat(lineasTempPalets).ToList();

                // Obtener información de los palets
                var paletIds = todasLasLineas.Select(l => l.PaletId).Distinct().ToList();
                var palets = await _context.Palets
                    .Where(p => paletIds.Contains(p.Id))
                    .Select(p => new
                    {
                        p.Id,
                        p.Codigo,
                        p.Estado,
                        p.TipoPaletCodigo,
                        p.FechaApertura,
                        p.FechaCierre
                    })
                    .ToDictionaryAsync(p => p.Id, p => p);

                // Agrupar por artículo, ubicación y lote
                foreach (var linea in lineas)
                {
                    var clave = $"{linea.CodigoArticulo}_{linea.CodigoUbicacion}_{linea.Partida ?? ""}";
                    
                    var paletsEnEstaUbicacion = todasLasLineas
                        .Where(l => l.CodigoArticulo == linea.CodigoArticulo &&
                                   l.Ubicacion.Trim().ToUpper() == linea.CodigoUbicacion.Trim().ToUpper() &&
                                   (l.Lote ?? "") == (linea.Partida ?? ""))
                        .GroupBy(l => l.PaletId)
                        .Select(g => new
                        {
                            paletId = g.Key,
                            cantidadEnPalet = g.Sum(x => x.Cantidad),
                            paletInfo = palets.GetValueOrDefault(g.Key)
                        })
                        .Where(p => p.paletInfo != null && p.cantidadEnPalet > 0)
                        .Select(p => new PaletDetalleDto
                        {
                            PaletId = p.paletId,
                            CodigoPalet = p.paletInfo.Codigo,
                            EstadoPalet = p.paletInfo.Estado,
                            Cantidad = p.cantidadEnPalet,
                            Ubicacion = linea.CodigoUbicacion,
                            Partida = linea.Partida,
                            FechaApertura = p.paletInfo.FechaApertura,
                            FechaCierre = p.paletInfo.FechaCierre
                        })
                        .OrderBy(p => p.CodigoPalet)
                        .ToList();

                    resultado[clave] = paletsEnEstaUbicacion.ToList<object>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo obtener información de palets");
                // Continuar sin información de palets
            }

            return resultado;
        }

        /// <summary>
        /// Genera líneas temporales del inventario basadas en ubicaciones reales con stock
        /// </summary>
        [HttpPost("generar-lineas-temporales/{idInventario}")]
        public async Task<IActionResult> GenerarLineasTemporales(Guid idInventario, [FromQuery] bool incluirUnidadesCero = false, [FromQuery] bool incluirArticulosConStockCero = false, [FromQuery] bool incluirUbicacionesEspeciales = false, [FromQuery] string? codigoArticuloFiltro = null, [FromQuery] string? articuloDesde = null, [FromQuery] string? articuloHasta = null)
        {
            var resultado = await GenerarLineasTemporalesInterno(idInventario, incluirUnidadesCero, incluirArticulosConStockCero, incluirUbicacionesEspeciales, codigoArticuloFiltro, articuloDesde, articuloHasta);
            
            if (resultado.Exito)
            {
                return Ok(new { 
                    Exito = true,
                    LineasGeneradas = resultado.LineasGeneradas,
                    UbicacionesEnRango = resultado.UbicacionesEnRango,
                    StockEncontrado = resultado.StockEncontrado
                });
            }
            else
            {
                return BadRequest(new { 
                    Exito = false,
                    Mensaje = resultado.Mensaje
                });
            }
        }

        /// <summary>
        /// Formatea los rangos de ubicaciones para almacenamiento
        /// </summary>
        private string FormatearRangoUbicaciones(CrearInventarioDto dto)
        {
            var rangos = new List<string>();

            // Formatear rango de pasillo si está especificado
            if (dto.PasilloDesde.HasValue && dto.PasilloHasta.HasValue)
            {
                rangos.Add($"P:{dto.PasilloDesde}-{dto.PasilloHasta}");
            }

            // Formatear rango de estantería si está especificado
            if (dto.EstanteriaDesde.HasValue && dto.EstanteriaHasta.HasValue)
            {
                rangos.Add($"E:{dto.EstanteriaDesde}-{dto.EstanteriaHasta}");
            }

            // Formatear rango de altura si está especificado
            if (dto.AlturaDesde.HasValue && dto.AlturaHasta.HasValue)
            {
                rangos.Add($"A:{dto.AlturaDesde}-{dto.AlturaHasta}");
            }

            // Formatear rango de posición si está especificado
            if (dto.PosicionDesde.HasValue && dto.PosicionHasta.HasValue)
            {
                rangos.Add($"O:{dto.PosicionDesde}-{dto.PosicionHasta}");
            }

            // Si no hay rangos específicos, usar el rango general o texto por defecto
            if (!rangos.Any())
            {
                return dto.RangoUbicaciones ?? "Rango no especificado";
            }

            return string.Join(",", rangos);
        }

        /// <summary>
        /// Obtiene stock para inventario haciendo consulta SQL directa sin filtros
        /// </summary>
        private async Task<List<AcumuladoStockUbicacion>> ObtenerStockParaInventario(
            short codigoEmpresa, 
            short ejercicio, 
            string codigoAlmacen, 
            List<string> ubicacionesEnRango, 
            bool incluirArticulosConStockCero,
            string? codigoArticuloFiltro = null,
            string? articuloDesde = null,
            string? articuloHasta = null)
        {
            try
            {
                IQueryable<AcumuladoStockUbicacion> query = _storageContext.AcumuladoStockUbicacion
                    .Where(s => s.CodigoEmpresa == codigoEmpresa &&
                               s.Ejercicio == ejercicio &&
                               s.CodigoAlmacen == codigoAlmacen &&
                               ubicacionesEnRango.Contains(s.Ubicacion));

                if (!incluirArticulosConStockCero)
                {
                    query = query.Where(s => s.UnidadSaldo > 0);
                }

                // NUEVO: Filtro por artículo específico si se especifica
                if (!string.IsNullOrWhiteSpace(codigoArticuloFiltro))
                {
                    query = query.Where(s => s.CodigoArticulo == codigoArticuloFiltro);
                }

                // NUEVO: Filtro por rango de artículos si se especifica
                if (!string.IsNullOrWhiteSpace(articuloDesde) && !string.IsNullOrWhiteSpace(articuloHasta))
                {
                    query = query.Where(s => string.Compare(s.CodigoArticulo, articuloDesde) >= 0 && 
                                           string.Compare(s.CodigoArticulo, articuloHasta) <= 0);
                }

                var result = await query.ToListAsync();



                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock para inventario");
                return new List<AcumuladoStockUbicacion>();
            }
        }

        /// <summary>
        /// Obtiene stock de ubicaciones especiales para inventario
        /// </summary>
        private async Task<List<AcumuladoStockUbicacion>> ObtenerStockParaInventarioUbicacionesEspeciales(
            short codigoEmpresa, 
            short ejercicio, 
            string codigoAlmacen, 
            bool incluirArticulosConStockCero,
            string? codigoArticuloFiltro = null,
            string? articuloDesde = null,
            string? articuloHasta = null)
        {
            try
            {
                IQueryable<AcumuladoStockUbicacion> query = _storageContext.AcumuladoStockUbicacion
                    .Where(s => s.CodigoEmpresa == codigoEmpresa &&
                               s.Ejercicio == ejercicio &&
                               s.CodigoAlmacen == codigoAlmacen &&
                               (s.Ubicacion == "" || s.Ubicacion == "ND" || !s.Ubicacion.StartsWith("UB")));

                if (!incluirArticulosConStockCero)
                {
                    query = query.Where(s => s.UnidadSaldo > 0);
                }

                // NUEVO: Filtro por artículo específico si se especifica
                if (!string.IsNullOrWhiteSpace(codigoArticuloFiltro))
                {
                    query = query.Where(s => s.CodigoArticulo == codigoArticuloFiltro);
                }

                // NUEVO: Filtro por rango de artículos si se especifica
                if (!string.IsNullOrWhiteSpace(articuloDesde) && !string.IsNullOrWhiteSpace(articuloHasta))
                {
                    query = query.Where(s => string.Compare(s.CodigoArticulo, articuloDesde) >= 0 && 
                                           string.Compare(s.CodigoArticulo, articuloHasta) <= 0);
                }

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener stock de ubicaciones especiales");
                return new List<AcumuladoStockUbicacion>();
            }
        }

        /// <summary>
        /// Verifica si hay advertencias de consolidación sin consolidar el inventario
        /// </summary>
        [HttpGet("verificar-advertencias/{idInventario}")]
        public async Task<IActionResult> VerificarAdvertenciasConsolidacion(Guid idInventario)
        {
            try
            {
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);

                if (inventario == null)
                {
                    return NotFound(new { mensaje = "Inventario no encontrado" });
                }

                // Obtener ejercicio actual para consultas de stock
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                {
                    return BadRequest(new { mensaje = "No se pudo determinar el ejercicio actual" });
                }

                var lineasTemp = await _context.InventarioLineasTemp
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();

                var lineasConDiferencias = new List<object>();
                var tolerancia = 0.01m; // Tolerancia para diferencias de redondeo

                foreach (var lineaTemp in lineasTemp)
                {
                    // Obtener stock actual del sistema
                    // 🔷 CORREGIDO: Usar el almacén de cada línea individual en lugar del almacén del inventario
                    var stockActual = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => 
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == lineaTemp.CodigoAlmacen && // ← CORREGIDO: Usar almacén de la línea
                            s.CodigoArticulo == lineaTemp.CodigoArticulo &&
                            s.Ubicacion == lineaTemp.CodigoUbicacion &&
                            s.Partida == lineaTemp.Partida);

                    var stockActualSistema = stockActual?.UnidadSaldo ?? 0;
                    var stockAlCrearInventario = lineaTemp.StockActual; // Stock cuando se creó el inventario
                    
                    // SOLO detectar cambios de stock real, NO cambios en el conteo del usuario
                    var diferencia = Math.Abs(stockAlCrearInventario - stockActualSistema);

                    if (diferencia > 0m) // Sin tolerancia, cualquier diferencia cuenta
                    {
                        lineasConDiferencias.Add(new
                        {
                            codigoArticulo = lineaTemp.CodigoArticulo,
                            codigoAlmacen = lineaTemp.CodigoAlmacen, // ← NUEVO: Incluir almacén en las diferencias
                            codigoUbicacion = lineaTemp.CodigoUbicacion,
                            partida = lineaTemp.Partida,
                            fechaCaducidad = lineaTemp.FechaCaducidad,
                            stockAlCrear = stockAlCrearInventario,
                            stockActual = stockActualSistema,
                            cantidadContada = lineaTemp.CantidadContada ?? 0,
                            diferencia = diferencia
                        });
                    }
                }

                return Ok(new
                {
                    tieneAdvertencias = lineasConDiferencias.Count > 0,
                    lineasConStockCambiado = lineasConDiferencias
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar advertencias de consolidación del inventario {IdInventario}", idInventario);
                return StatusCode(500, new { mensaje = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene las líneas problemáticas de un inventario (con stock cambiado)
        /// </summary>
        [HttpGet("lineas-problematicas/{idInventario}")]
        public async Task<IActionResult> ObtenerLineasProblematicas(Guid idInventario)
        {
            try
            {
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == idInventario);

                if (inventario == null)
                {
                    return NotFound(new { mensaje = "Inventario no encontrado" });
                }

                // Obtener ejercicio actual para consultas de stock
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                // Obtener líneas temporales
                var lineasTemp = await _context.InventarioLineasTemp
                    .Where(l => l.IdInventario == idInventario)
                    .ToListAsync();

                var lineasProblematicas = new List<LineaProblematicaDto>();

                foreach (var lineaTemp in lineasTemp)
                {
                    // Verificar stock actual en tiempo real para mostrar información adicional
                    var stockActual = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => 
                            s.CodigoEmpresa == inventario.CodigoEmpresa &&
                            s.Ejercicio == ejercicio &&
                            s.CodigoAlmacen == inventario.CodigoAlmacen &&
                            s.CodigoArticulo == lineaTemp.CodigoArticulo &&
                            s.Ubicacion == lineaTemp.CodigoUbicacion &&
                            s.Partida == lineaTemp.Partida);

                    var stockRealActual = stockActual?.UnidadSaldo ?? 0;
                    var stockAlCrearInventario = lineaTemp.StockActual;
                    var cantidadContada = lineaTemp.CantidadContada ?? 0;
                    
                    // DEBUG: Mostrar información de evaluación
                    Console.WriteLine($"🔍 RECONTEO: Art={lineaTemp.CodigoArticulo}, Ubic={lineaTemp.CodigoUbicacion}");
                    Console.WriteLine($"   StockAlCrear={stockAlCrearInventario}, StockActual={stockRealActual}");
                    
                    // SOLO detectar cambios de stock real, NO cambios en el conteo del usuario
                    // Comparar stock al crear inventario vs stock actual del sistema
                    var diferenciaStock = Math.Abs(stockRealActual - stockAlCrearInventario);
                    
                    Console.WriteLine($"   DiferenciaStock={diferenciaStock}, ¿HaCambiado?={diferenciaStock > 0m}");
                    
                    // Sin tolerancia: cualquier diferencia se considera problemática
                    bool stockHaCambiado = diferenciaStock > 0m;
                    
                    if (stockHaCambiado)
                    {
                        // Obtener descripción del artículo
                        var articulo = await _sageDbContext.Articulos
                            .FirstOrDefaultAsync(a => a.CodigoEmpresa == inventario.CodigoEmpresa && 
                                                     a.CodigoArticulo == lineaTemp.CodigoArticulo);

                        lineasProblematicas.Add(new LineaProblematicaDto
                        {
                            CodigoArticulo = lineaTemp.CodigoArticulo,
                            DescripcionArticulo = articulo?.DescripcionArticulo ?? "Sin descripción",
                            CodigoAlmacen = inventario.CodigoAlmacen,
                            CodigoUbicacion = lineaTemp.CodigoUbicacion,
                            Partida = lineaTemp.Partida,
                            FechaCaducidad = lineaTemp.FechaCaducidad,
                            StockAlCrearInventario = stockAlCrearInventario,
                            StockActual = stockRealActual,
                            CantidadContada = cantidadContada
                        });
                    }
                }

                return Ok(lineasProblematicas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener líneas problemáticas del inventario {IdInventario}", idInventario);
                return StatusCode(500, new { mensaje = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Guarda el reconteo de líneas problemáticas
        /// </summary>
        [HttpPost("guardar-reconteo")]
        public async Task<IActionResult> GuardarReconteo([FromBody] GuardarReconteoDto reconteo)
        {
            try
            {
                var inventario = await _context.InventarioCabecera
                    .FirstOrDefaultAsync(i => i.IdInventario == reconteo.IdInventario);

                if (inventario == null)
                {
                    return NotFound(new { mensaje = "Inventario no encontrado" });
                }

                // === NUEVO: Validación de límites por artículo antes de guardar reconteo ===
                foreach (var lineaReconteo in reconteo.LineasRecontadas)
                {
                    var (acumUnid, acumEur) = await CalcularDiferenciasDiariasPorArticuloAsync(
                        lineaReconteo.UsuarioReconteo,
                        lineaReconteo.CodigoArticulo,
                        reconteo.IdInventario,
                        inventario.CodigoEmpresa);

                    var limEuros = await _sageDbContext.Operarios
                        .Where(o => o.Id == lineaReconteo.UsuarioReconteo)
                        .Select(o => o.MRH_LimiteInventarioEuros)
                        .FirstOrDefaultAsync() ?? 0m;

                    var limUnidades = await _sageDbContext.Operarios
                        .Where(o => o.Id == lineaReconteo.UsuarioReconteo)
                        .Select(o => o.MRH_LimiteInventarioUnidades)
                        .FirstOrDefaultAsync() ?? 0m;

                                    // Calcular diferencia con stock actual del sistema
                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == inventario.CodigoEmpresa && p.Fechainicio <= DateTime.Now) // Filtro de fecha para obtener ejercicio actual
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                    var stockActual = await _storageContext.AcumuladoStockUbicacion
                        .FirstOrDefaultAsync(s => s.CodigoEmpresa == inventario.CodigoEmpresa && s.Ejercicio == ejercicio &&
                                                  s.CodigoAlmacen == inventario.CodigoAlmacen && s.CodigoArticulo == lineaReconteo.CodigoArticulo &&
                                                  s.Ubicacion == lineaReconteo.CodigoUbicacion && s.Partida == lineaReconteo.Partida);

                    var diferenciaNueva = Math.Abs(lineaReconteo.CantidadReconteo - (stockActual?.UnidadSaldo ?? 0));

                    if (limEuros > 0)
                    {
                        var precioMedio = await _sageDbContext.AcumuladoStock
                            .Where(a => a.CodigoEmpresa == inventario.CodigoEmpresa && a.CodigoArticulo == lineaReconteo.CodigoArticulo)
                            .OrderByDescending(a => a.Ejercicio)
                            .Select(a => a.PrecioMedio)
                            .FirstOrDefaultAsync() ?? 0m;

                        var totalEuros = acumEur + (diferenciaNueva * precioMedio);
                        if (totalEuros > limEuros)
                        {
                            return BadRequest(new { mensaje = $"Límite diario de euros superado para el artículo {lineaReconteo.CodigoArticulo}." });
                        }
                    }

                    if (limUnidades > 0)
                    {
                        var totalUnidades = acumUnid + diferenciaNueva;
                        if (totalUnidades > limUnidades)
                        {
                            return BadRequest(new { mensaje = $"Límite diario de unidades superado para el artículo {lineaReconteo.CodigoArticulo}." });
                        }
                    }
                }

                foreach (var lineaReconteo in reconteo.LineasRecontadas)
                {
                    // Buscar la línea temporal correspondiente
                    var lineaTemp = await _context.InventarioLineasTemp
                        .FirstOrDefaultAsync(l => l.IdInventario == reconteo.IdInventario &&
                                                 l.CodigoArticulo == lineaReconteo.CodigoArticulo &&
                                                 l.CodigoUbicacion == lineaReconteo.CodigoUbicacion &&
                                                 l.Partida == lineaReconteo.Partida);

                    if (lineaTemp != null)
                    {
                        // Actualizar la cantidad contada con el reconteo
                        lineaTemp.CantidadContada = lineaReconteo.CantidadReconteo;
                        lineaTemp.UsuarioConteoId = lineaReconteo.UsuarioReconteo;
                        lineaTemp.FechaConteo = DateTime.Now; // Siempre usar la hora del servidor/API
                        
                        _context.InventarioLineasTemp.Update(lineaTemp);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Reconteo guardado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar reconteo del inventario {IdInventario}", reconteo.IdInventario);
                return StatusCode(500, new { mensaje = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Aplica los ajustes de inventario a los palets correspondientes
        /// </summary>
        [HttpPost("{idInventario}/aplicar-ajustes")]
        public async Task<IActionResult> AplicarAjustesInventario(Guid idInventario, [FromQuery] int usuarioId)
        {
            try
            {
                // 1. Obtener el inventario y sus líneas definitivas
                var inventario = await _context.InventarioCabecera.FindAsync(idInventario);
                if (inventario == null) return NotFound("Inventario no encontrado");
                
                if (inventario.Estado != "CONSOLIDADO")
                    return BadRequest("Solo se pueden aplicar ajustes a inventarios consolidados");

                var lineas = await _context.InventarioLineas
                    .Where(l => l.IdInventario == idInventario && l.AjusteFinal.HasValue && l.AjusteFinal != 0)
                    .ToListAsync();

                if (!lineas.Any())
                    return Ok(new { message = "No hay ajustes que aplicar" });

                var resultados = new List<object>();

                foreach (var linea in lineas)
                {
                    // 2. Buscar TODAS las líneas de palet en esa ubicación específica
                    var lineasPalet = await _context.PaletLineas
                        .Where(pl => pl.CodigoEmpresa == inventario.CodigoEmpresa &&
                                    pl.CodigoArticulo == linea.CodigoArticulo &&
                                    pl.CodigoAlmacen == inventario.CodigoAlmacen &&
                                    pl.Ubicacion == linea.CodigoUbicacion &&
                                    pl.Lote == linea.Partida)
                        .Include(pl => pl.Palet)
                        .ToListAsync();

                    if (lineasPalet.Any())
                    {
                        // 3. Aplicar ajuste distribuyendo entre los palets encontrados
                        var ajuste = linea.AjusteFinal.Value;
                        var ajusteRestante = ajuste;
                        
                        // Ordenar por fecha de agregado (más reciente primero)
                        var lineasOrdenadas = lineasPalet.OrderByDescending(pl => pl.FechaAgregado).ToList();
                        
                        foreach (var lineaPalet in lineasOrdenadas)
                        {
                            if (ajusteRestante == 0) break;
                            
                            if (ajusteRestante > 0)
                            {
                                // Añadir stock al palet
                                var cantidadAAnadir = Math.Min(ajusteRestante, lineaPalet.Cantidad * 0.1m); // Máximo 10% del stock actual
                                lineaPalet.Cantidad += cantidadAAnadir;
                                ajusteRestante -= cantidadAAnadir;
                                
                                _context.PaletLineas.Update(lineaPalet);
                                
                                // Log del ajuste
                                _context.LogPalet.Add(new LogPalet
                                {
                                    PaletId = lineaPalet.PaletId,
                                    Fecha = DateTime.Now, // Siempre usar la hora del servidor/API
                                    IdUsuario = usuarioId,
                                    Accion = "AjusteInventario",
                                    Detalle = $"Añadido {cantidadAAnadir:F4} unidades por inventario. Artículo: {linea.CodigoArticulo}, Línea ID: {lineaPalet.Id}"
                                });
                            }
                            else if (ajusteRestante < 0)
                            {
                                // Restar stock del palet
                                var cantidadARestar = Math.Min(Math.Abs(ajusteRestante), lineaPalet.Cantidad);
                                lineaPalet.Cantidad -= cantidadARestar;
                                ajusteRestante += cantidadARestar;
                                
                                _context.PaletLineas.Update(lineaPalet);
                                
                                // Log del ajuste
                                _context.LogPalet.Add(new LogPalet
                                {
                                    PaletId = lineaPalet.PaletId,
                                    Fecha = DateTime.Now, // Siempre usar la hora del servidor/API
                                    IdUsuario = usuarioId,
                                    Accion = "AjusteInventario",
                                    Detalle = $"Restado {cantidadARestar:F4} unidades por inventario. Artículo: {linea.CodigoArticulo}, Línea ID: {lineaPalet.Id}"
                                });
                            }
                        }

                        // 4. Verificar si quedó ajuste sin aplicar
                        if (ajusteRestante != 0)
                        {
                            resultados.Add(new
                            {
                                linea.CodigoArticulo,
                                linea.CodigoUbicacion,
                                AjusteSolicitado = ajuste,
                                AjusteAplicado = ajuste - ajusteRestante,
                                AjustePendiente = ajusteRestante,
                                PaletsAfectados = lineasPalet.Count,
                                Error = ajusteRestante > 0 ? "Stock insuficiente en palets" : "No se pudo aplicar todo el ajuste"
                            });
                        }
                        else
                        {
                            var paletsInfo = lineasPalet.Select(pl => new
                            {
                                PaletId = pl.PaletId,
                                CodigoPalet = pl.Palet.Codigo,
                                EstadoPalet = pl.Palet.Estado,
                                StockFinal = pl.Cantidad
                            }).ToList();

                            resultados.Add(new
                            {
                                linea.CodigoArticulo,
                                linea.CodigoUbicacion,
                                AjusteAplicado = ajuste,
                                PaletsAfectados = lineasPalet.Count,
                                PaletsInfo = paletsInfo
                            });
                        }
                    }
                    else
                    {
                        // No hay palets en esa ubicación
                        resultados.Add(new
                        {
                            linea.CodigoArticulo,
                            linea.CodigoUbicacion,
                            AjusteSolicitado = linea.AjusteFinal,
                            Error = "No se encontraron palets en la ubicación"
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // 5. Marcar inventario como cerrado
                inventario.Estado = "CERRADO";
                inventario.FechaCierre = DateTime.Now; // Siempre usar la hora del servidor/API
                inventario.UsuarioCierreId = usuarioId;
                _context.InventarioCabecera.Update(inventario);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Ajustes aplicados correctamente",
                    inventarioId = idInventario,
                    resultados
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aplicar ajustes del inventario {IdInventario}", idInventario);
                return Problem(detail: ex.ToString(), statusCode: 500, title: "Error aplicando ajustes");
            }
        }






    }
} 