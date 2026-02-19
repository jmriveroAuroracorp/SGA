using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Inventario;
using SGA_Api.Models.Palet;

namespace SGA_Api.Controllers.OrdenConversion
{
    /// <summary>
    /// Controlador para órdenes de conversión/ampliación.
    /// Desktop crea la orden (cabecera), operario la ejecuta desde Android registrando líneas.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenConversionController : ControllerBase
    {
        private readonly AuroraSgaDbContext _context;
        private readonly StorageControlDbContext _storageContext;
        private readonly SageDbContext _sageDbContext;
        private readonly ILogger<OrdenConversionController> _logger;

        public OrdenConversionController(
            AuroraSgaDbContext context,
            StorageControlDbContext storageContext,
            SageDbContext sageDbContext,
            ILogger<OrdenConversionController> logger)
        {
            _context = context;
            _storageContext = storageContext;
            _sageDbContext = sageDbContext;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/OrdenConversion
        /// Crea una orden de conversión (cabecera). Equivalente a planificar-cambio.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CrearOrden([FromBody] CrearOrdenConversionDto dto)
        {
            try
            {
                bool cambioCodigo = !string.IsNullOrWhiteSpace(dto.CodigoArticuloDestino);
                bool cambioFecha = dto.FechaCaducidadDestino.HasValue;

                if (!cambioCodigo && !cambioFecha)
                    return BadRequest("Debe especificarse un cambio de código o de fecha de caducidad");

                if (cambioCodigo && cambioFecha)
                    return BadRequest("No se puede cambiar código y fecha simultáneamente");

                var articuloOrigen = await _sageDbContext.Articulos
                    .FirstOrDefaultAsync(a => a.CodigoEmpresa == dto.CodigoEmpresa &&
                                               a.CodigoArticulo == dto.CodigoArticuloOrigen);
                if (articuloOrigen == null)
                    return NotFound($"Artículo origen no encontrado: {dto.CodigoArticuloOrigen}");

                if (cambioCodigo)
                {
                    var articuloDestino = await _sageDbContext.Articulos
                        .FirstOrDefaultAsync(a => a.CodigoEmpresa == dto.CodigoEmpresa &&
                                                   a.CodigoArticulo == dto.CodigoArticuloDestino);
                    if (articuloDestino == null)
                        return NotFound($"Artículo destino no encontrado: {dto.CodigoArticuloDestino}");
                }

                var fechaCaducidadOrigen = dto.FechaCaducidadOrigen?.Date;
                var fechaCaducidadDestino = dto.FechaCaducidadDestino?.Date;

                var orden = new OrdenConversionCabecera
                {
                    IdOrdenConversion = Guid.NewGuid(),
                    CodigoEmpresa = dto.CodigoEmpresa,
                    UsuarioId = dto.UsuarioId,
                    OperarioAsignadoId = dto.OperarioAsignadoId,
                    Fecha = DateTime.Now,
                    CodigoArticuloOrigen = dto.CodigoArticuloOrigen,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    PartidaOrigen = dto.PartidaOrigen,
                    FechaCaducidadOrigen = fechaCaducidadOrigen,
                    Cantidad = dto.Cantidad,
                    CantidadFinal = dto.CantidadFinal,
                    CodigoArticuloDestino = cambioCodigo ? dto.CodigoArticuloDestino : null,
                    PartidaDestino = (cambioCodigo || cambioFecha) && !string.IsNullOrWhiteSpace(dto.PartidaDestino) ? dto.PartidaDestino : null,
                    FechaCaducidadDestino = cambioFecha ? fechaCaducidadDestino : null,
                    TipoCambio = cambioCodigo ? "CAMBIO_CODIGO" : "AMPLIACION",
                    Comentario = dto.Comentario,
                    Estado = dto.OperarioAsignadoId.HasValue ? "ASIGNADO" : "PLANIFICADO"
                };

                _context.OrdenConversionCabecera.Add(orden);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    orden.IdOrdenConversion,
                    orden.TipoCambio,
                    orden.OperarioAsignadoId,
                    Mensaje = "Orden de conversión creada correctamente."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear orden de conversión: CodigoOrigen={CodigoOrigen}", dto.CodigoArticuloOrigen);
                return StatusCode(500, "Error interno al crear la orden de conversión");
            }
        }

        /// <summary>
        /// GET /api/OrdenConversion/pendientes/{operarioId}
        /// Devuelve las órdenes de conversión pendientes para un operario.
        /// </summary>
        [HttpGet("pendientes/{operarioId}")]
        public async Task<ActionResult<IEnumerable<OrdenConversionPendienteDto>>> GetPendientes(
            int operarioId,
            [FromQuery] short? codigoEmpresa = null)
        {
            var query = _context.OrdenConversionCabecera
                .AsNoTracking()
                .Where(o => o.OperarioAsignadoId == operarioId &&
                            (o.Estado == "ASIGNADO" || o.Estado == "EN_PROCESO"));

            if (codigoEmpresa.HasValue)
                query = query.Where(o => o.CodigoEmpresa == codigoEmpresa.Value);

            var ordenes = await query.OrderBy(o => o.Fecha).ToListAsync();

            var codigosPermiso = await _sageDbContext.AccesosOperarios
                .Where(a => a.Operario == operarioId && a.CodigoEmpresa == 1)
                .Select(a => a.MRH_CodigoAplicacion)
                .ToListAsync();

            bool puedeConversion = codigosPermiso.Contains((short)23);
            bool puedeAmpliacion = codigosPermiso.Contains((short)24);

            ordenes = ordenes
                .Where(o =>
                    (o.TipoCambio == "CAMBIO_CODIGO" && puedeConversion) ||
                    (o.TipoCambio == "AMPLIACION" && puedeAmpliacion))
                .ToList();

            var almacenesOperario = await _sageDbContext.OperariosAlmacenes
                .Where(oa => oa.Operario == operarioId)
                .Select(oa => new { oa.CodigoEmpresa, CodigoAlmacen = oa.CodigoAlmacen ?? "" })
                .ToListAsync();

            ordenes = ordenes
                .Where(o => almacenesOperario.Any(a =>
                    a.CodigoEmpresa == o.CodigoEmpresa &&
                    a.CodigoAlmacen == (o.CodigoAlmacen ?? "")))
                .ToList();

            var result = new List<OrdenConversionPendienteDto>();
            foreach (var o in ordenes)
            {
                var cantidadEjecutada = await _context.OrdenConversionLineas
                    .Where(l => l.IdOrdenConversion == o.IdOrdenConversion)
                    .SumAsync(l => l.Cantidad);
                var cantidadPendiente = o.Cantidad - cantidadEjecutada;

                result.Add(new OrdenConversionPendienteDto
                {
                    IdOrdenConversion = o.IdOrdenConversion,
                    CodigoEmpresa = o.CodigoEmpresa,
                    UsuarioId = o.UsuarioId,
                    OperarioAsignadoId = o.OperarioAsignadoId,
                    Fecha = o.Fecha,
                    CodigoArticuloOrigen = o.CodigoArticuloOrigen ?? string.Empty,
                    CodigoAlmacen = o.CodigoAlmacen ?? string.Empty,
                    PartidaOrigen = o.PartidaOrigen,
                    FechaCaducidadOrigen = o.FechaCaducidadOrigen,
                    Cantidad = o.Cantidad,
                    CantidadFinal = o.CantidadFinal,
                    CodigoArticuloDestino = o.CodigoArticuloDestino,
                    PartidaDestino = o.PartidaDestino,
                    FechaCaducidadDestino = o.FechaCaducidadDestino,
                    TipoCambio = o.TipoCambio ?? string.Empty,
                    Comentario = o.Comentario,
                    Estado = o.Estado ?? string.Empty,
                    CantidadEjecutada = cantidadEjecutada,
                    CantidadPendiente = cantidadPendiente
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// GET /api/OrdenConversion/{id}
        /// Obtiene el detalle de una orden de conversión con sus líneas.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<OrdenConversionDetalleDto>> ObtenerOrden(Guid id)
        {
            var orden = await _context.OrdenConversionCabecera
                .AsNoTracking()
                .Include(o => o.Lineas)
                .FirstOrDefaultAsync(o => o.IdOrdenConversion == id);

            if (orden == null)
                return NotFound($"Orden de conversión no encontrada: {id}");

            var cantidadEjecutada = orden.Lineas.Sum(l => l.Cantidad);
            var cantidadPendiente = orden.Cantidad - cantidadEjecutada;

            var dto = new OrdenConversionDetalleDto
            {
                IdOrdenConversion = orden.IdOrdenConversion,
                CodigoEmpresa = orden.CodigoEmpresa,
                UsuarioId = orden.UsuarioId,
                OperarioAsignadoId = orden.OperarioAsignadoId,
                Fecha = orden.Fecha,
                CodigoArticuloOrigen = orden.CodigoArticuloOrigen ?? string.Empty,
                CodigoAlmacen = orden.CodigoAlmacen ?? string.Empty,
                PartidaOrigen = orden.PartidaOrigen,
                FechaCaducidadOrigen = orden.FechaCaducidadOrigen,
                Cantidad = orden.Cantidad,
                CantidadFinal = orden.CantidadFinal,
                CodigoArticuloDestino = orden.CodigoArticuloDestino,
                PartidaDestino = orden.PartidaDestino,
                FechaCaducidadDestino = orden.FechaCaducidadDestino,
                TipoCambio = orden.TipoCambio ?? string.Empty,
                Comentario = orden.Comentario,
                Estado = orden.Estado ?? string.Empty,
                CantidadEjecutada = cantidadEjecutada,
                CantidadPendiente = cantidadPendiente,
                Lineas = orden.Lineas.OrderBy(l => l.NumeroLinea).Select(l => new OrdenConversionLineaDto
                {
                    IdLinea = l.IdLinea,
                    NumeroLinea = l.NumeroLinea,
                    CodigoAlmacen = l.CodigoAlmacen ?? string.Empty,
                    Ubicacion = l.Ubicacion ?? string.Empty,
                    PaletId = l.PaletId,
                    Partida = l.Partida,
                    FechaCaducidad = l.FechaCaducidad,
                    Cantidad = l.Cantidad,
                    CantidadFinal = l.CantidadFinal,
                    UsuarioEjecucionId = l.UsuarioEjecucionId,
                    FechaEjecucion = l.FechaEjecucion
                }).ToList()
            };

            return Ok(dto);
        }

        /// <summary>
        /// GET /api/OrdenConversion/{id}/ubicaciones-disponibles
        /// Obtiene las ubicaciones con stock del artículo origen (replica Conteos/lecturas-pendientes).
        /// El operario ve dónde puede tomar stock para ejecutar la conversión.
        /// </summary>
        [HttpGet("{id:guid}/ubicaciones-disponibles")]
        public async Task<ActionResult<IEnumerable<UbicacionDisponibleConversionDto>>> ObtenerUbicacionesDisponibles(Guid id)
        {
            var orden = await _context.OrdenConversionCabecera
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.IdOrdenConversion == id);

            if (orden == null)
                return NotFound($"Orden de conversión no encontrada: {id}");

            if (orden.Estado != "ASIGNADO" && orden.Estado != "EN_PROCESO")
                return BadRequest($"La orden no está en estado ejecutable (estado actual: {orden.Estado}).");

            var ejercicio = await _sageDbContext.Periodos
                .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                .OrderByDescending(p => p.Fechainicio)
                .Select(p => p.Ejercicio)
                .FirstOrDefaultAsync();

            if (ejercicio == 0)
                return BadRequest("Sin ejercicio válido");

            var query = _storageContext.AcumuladoStockUbicacion
                .Where(x => x.CodigoEmpresa == orden.CodigoEmpresa &&
                            x.Ejercicio == ejercicio &&
                            x.CodigoArticulo == orden.CodigoArticuloOrigen &&
                            x.CodigoAlmacen == (orden.CodigoAlmacen ?? "") &&
                            (x.UnidadSaldo ?? 0) > 0);

            if (!string.IsNullOrWhiteSpace(orden.PartidaOrigen))
                query = query.Where(x => x.Partida == orden.PartidaOrigen);

            if (orden.FechaCaducidadOrigen.HasValue)
            {
                var fechaNorm = orden.FechaCaducidadOrigen.Value.Date;
                query = query.Where(x => x.FechaCaducidad.HasValue && x.FechaCaducidad.Value.Date == fechaNorm);
            }

            var stockData = await query.ToListAsync();
            var resultado = new List<UbicacionDisponibleConversionDto>();

            foreach (var stock in stockData)
            {
                var ubicacionNorm = stock.Ubicacion ?? string.Empty;
                var paletLineas = await _context.PaletLineas
                    .Where(pl => pl.CodigoEmpresa == orden.CodigoEmpresa &&
                                 pl.CodigoAlmacen == (orden.CodigoAlmacen ?? "") &&
                                 pl.Ubicacion == ubicacionNorm &&
                                 pl.CodigoArticulo == orden.CodigoArticuloOrigen &&
                                 (string.IsNullOrEmpty(stock.Partida) || pl.Lote == stock.Partida) &&
                                 (stock.FechaCaducidad == null || pl.FechaCaducidad == stock.FechaCaducidad) &&
                                 pl.Cantidad > 0)
                    .Include(pl => pl.Palet)
                    .ToListAsync();

                var totalPaletizado = paletLineas.Sum(pl => pl.Cantidad);

                if (paletLineas.Any())
                {
                    foreach (var pl in paletLineas)
                    {
                        resultado.Add(new UbicacionDisponibleConversionDto
                        {
                            CodigoAlmacen = orden.CodigoAlmacen ?? string.Empty,
                            Ubicacion = ubicacionNorm,
                            Partida = stock.Partida,
                            FechaCaducidad = stock.FechaCaducidad,
                            CantidadDisponible = pl.Cantidad,
                            PaletId = pl.PaletId,
                            CodigoPalet = pl.Palet?.Codigo
                        });
                    }
                    var cantidadRestante = (stock.UnidadSaldo ?? 0) - totalPaletizado;
                    if (cantidadRestante > 0.0001m)
                    {
                        resultado.Add(new UbicacionDisponibleConversionDto
                        {
                            CodigoAlmacen = orden.CodigoAlmacen ?? string.Empty,
                            Ubicacion = ubicacionNorm,
                            Partida = stock.Partida,
                            FechaCaducidad = stock.FechaCaducidad,
                            CantidadDisponible = cantidadRestante,
                            PaletId = null,
                            CodigoPalet = null
                        });
                    }
                }
                else
                {
                    resultado.Add(new UbicacionDisponibleConversionDto
                    {
                        CodigoAlmacen = orden.CodigoAlmacen ?? string.Empty,
                        Ubicacion = ubicacionNorm,
                        Partida = stock.Partida,
                        FechaCaducidad = stock.FechaCaducidad,
                        CantidadDisponible = stock.UnidadSaldo ?? 0,
                        PaletId = null,
                        CodigoPalet = null
                    });
                }
            }

            return Ok(resultado.OrderBy(r => r.Ubicacion).ThenBy(r => r.Partida ?? "").ThenBy(r => r.CodigoPalet ?? ""));
        }

        /// <summary>
        /// POST /api/OrdenConversion/{id}/iniciar
        /// Inicia una orden (cambia estado de ASIGNADO a EN_PROCESO).
        /// </summary>
        [HttpPost("{id:guid}/iniciar")]
        public async Task<IActionResult> IniciarOrden(Guid id)
        {
            var orden = await _context.OrdenConversionCabecera.FirstOrDefaultAsync(o => o.IdOrdenConversion == id);
            if (orden == null)
                return NotFound($"Orden de conversión no encontrada: {id}");
            if (orden.Estado != "ASIGNADO")
                return BadRequest($"La orden no está en estado ASIGNADO (estado actual: {orden.Estado}).");

            orden.Estado = "EN_PROCESO";
            await _context.SaveChangesAsync();
            return Ok(new { Mensaje = "Orden iniciada correctamente." });
        }

        /// <summary>
        /// POST /api/OrdenConversion/{id}/lineas
        /// Registra una línea de conversión (lo que el operario ejecuta). Equivalente a ejecutar.
        /// </summary>
        [HttpPost("{id:guid}/lineas")]
        public async Task<IActionResult> RegistrarLinea(Guid id, [FromBody] RegistrarLineaConversionDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var orden = await _context.OrdenConversionCabecera
                    .FirstOrDefaultAsync(o => o.IdOrdenConversion == id);

                if (orden == null)
                    return NotFound($"Orden de conversión no encontrada: {id}");

                if (orden.Estado != "ASIGNADO" && orden.Estado != "EN_PROCESO")
                    return BadRequest($"La orden no está en estado ejecutable (estado actual: {orden.Estado}).");

                bool cambioCodigo = !string.IsNullOrWhiteSpace(orden.CodigoArticuloDestino);
                bool cambioFecha = orden.FechaCaducidadDestino.HasValue;

                var cantidadEjecutada = await _context.OrdenConversionLineas
                    .Where(l => l.IdOrdenConversion == id)
                    .SumAsync(l => l.Cantidad);
                var cantidadPendiente = orden.Cantidad - cantidadEjecutada;

                if (dto.Cantidad > cantidadPendiente)
                    return BadRequest($"Cantidad excede lo pendiente. Pendiente: {cantidadPendiente:N2}, Solicitado: {dto.Cantidad:N2}");

                var ejercicio = await _sageDbContext.Periodos
                    .Where(p => p.CodigoEmpresa == orden.CodigoEmpresa && p.Fechainicio <= DateTime.Now)
                    .OrderByDescending(p => p.Fechainicio)
                    .Select(p => p.Ejercicio)
                    .FirstOrDefaultAsync();

                if (ejercicio == 0)
                    return BadRequest("Sin ejercicio válido");

                decimal stockDisponible = 0;
                var ubicacionNorm = string.IsNullOrEmpty(dto.Ubicacion) ? string.Empty : dto.Ubicacion;

                if (dto.PaletId.HasValue)
                {
                    stockDisponible = await _context.PaletLineas
                        .Where(pl => pl.PaletId == dto.PaletId.Value &&
                                     pl.CodigoEmpresa == orden.CodigoEmpresa &&
                                     pl.CodigoArticulo == orden.CodigoArticuloOrigen &&
                                     pl.CodigoAlmacen == dto.CodigoAlmacen &&
                                     pl.Ubicacion == ubicacionNorm &&
                                     (pl.Lote == dto.Partida || (pl.Lote == null && dto.Partida == null)) &&
                                     (pl.FechaCaducidad == dto.FechaCaducidad || (pl.FechaCaducidad == null && dto.FechaCaducidad == null)))
                        .SumAsync(pl => (decimal?)pl.Cantidad) ?? 0m;
                }
                else
                {
                    var totalUbicacion = await _storageContext.AcumuladoStockUbicacion
                        .Where(s => s.CodigoEmpresa == orden.CodigoEmpresa &&
                                    s.Ejercicio == ejercicio &&
                                    s.CodigoAlmacen == dto.CodigoAlmacen &&
                                    s.CodigoArticulo == orden.CodigoArticuloOrigen &&
                                    s.Ubicacion == ubicacionNorm &&
                                    (s.Partida == dto.Partida || (s.Partida == null && dto.Partida == null)) &&
                                    (s.FechaCaducidad == dto.FechaCaducidad || (s.FechaCaducidad == null && dto.FechaCaducidad == null)))
                        .SumAsync(s => (decimal?)s.UnidadSaldo) ?? 0m;

                    var paletizado = await _context.PaletLineas
                        .Where(pl => pl.CodigoEmpresa == orden.CodigoEmpresa &&
                                     pl.CodigoAlmacen == dto.CodigoAlmacen &&
                                     pl.Ubicacion == ubicacionNorm &&
                                     pl.CodigoArticulo == orden.CodigoArticuloOrigen &&
                                     (pl.Lote == dto.Partida || (pl.Lote == null && dto.Partida == null)) &&
                                     (pl.FechaCaducidad == dto.FechaCaducidad || (pl.FechaCaducidad == null && dto.FechaCaducidad == null)))
                        .SumAsync(pl => (decimal?)pl.Cantidad) ?? 0m;

                    stockDisponible = totalUbicacion - paletizado;
                }

                if (stockDisponible < dto.Cantidad)
                    return BadRequest($"Stock insuficiente. Disponible: {stockDisponible:N2}, Solicitado: {dto.Cantidad:N2}");

                var numeroLinea = await _context.OrdenConversionLineas
                    .Where(l => l.IdOrdenConversion == id)
                    .CountAsync() + 1;

                var fechaCaducidadOrigen = orden.FechaCaducidadOrigen?.Date;
                var fechaCaducidadDestino = orden.FechaCaducidadDestino?.Date;
                var partidaDestino = (cambioCodigo || cambioFecha) && !string.IsNullOrWhiteSpace(orden.PartidaDestino) ? orden.PartidaDestino : orden.PartidaOrigen;
                var fechaCaducidadLinea = dto.FechaCaducidad?.Date ?? fechaCaducidadOrigen;
                var partidaLinea = dto.Partida ?? orden.PartidaOrigen;

                string? codigoPalet = null;
                if (dto.PaletId.HasValue)
                {
                    codigoPalet = await _context.Palets
                        .Where(p => p.Id == dto.PaletId.Value)
                        .Select(p => p.Codigo)
                        .FirstOrDefaultAsync();
                }

                // Cantidad en unidades destino para esta línea (ej. 2 botes → 120 pastillas)
                decimal cantidadDestino = orden.CantidadFinal.HasValue && orden.Cantidad > 0
                    ? dto.Cantidad * (orden.CantidadFinal.Value / orden.Cantidad)
                    : dto.Cantidad;

                var linea = new OrdenConversionLineas
                {
                    IdLinea = Guid.NewGuid(),
                    IdOrdenConversion = id,
                    NumeroLinea = numeroLinea,
                    TipoCambio = orden.TipoCambio,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    Ubicacion = ubicacionNorm,
                    PaletId = dto.PaletId,
                    Partida = partidaLinea,
                    FechaCaducidad = fechaCaducidadLinea,
                    Cantidad = dto.Cantidad,
                    CantidadFinal = cantidadDestino,
                    UsuarioEjecucionId = dto.UsuarioEjecucionId,
                    FechaEjecucion = DateTime.Now
                };

                _context.OrdenConversionLineas.Add(linea);

                var ajusteSalida = new InventarioAjustes
                {
                    IdAjuste = Guid.NewGuid(),
                    IdInventario = null,
                    CodigoArticulo = orden.CodigoArticuloOrigen,
                    CodigoUbicacion = ubicacionNorm,
                    Diferencia = -dto.Cantidad,
                    UsuarioId = dto.UsuarioEjecucionId,
                    Fecha = DateTime.Now,
                    IdConteo = Guid.Empty,
                    IdCambioArticulo = linea.IdLinea, // Reutilizado: IdLinea para flujo OrdenConversion
                    CodigoEmpresa = orden.CodigoEmpresa,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    Estado = "PENDIENTE_ERP",
                    FechaCaducidad = fechaCaducidadOrigen,
                    Partida = partidaLinea,
                    PaletId = dto.PaletId,
                    CodigoPalet = codigoPalet,
                    ProcesadoPalet = false
                };

                var ajusteEntrada = new InventarioAjustes
                {
                    IdAjuste = Guid.NewGuid(),
                    IdInventario = null,
                    CodigoArticulo = cambioCodigo ? orden.CodigoArticuloDestino! : orden.CodigoArticuloOrigen,
                    CodigoUbicacion = ubicacionNorm,
                    Diferencia = cantidadDestino,
                    UsuarioId = dto.UsuarioEjecucionId,
                    Fecha = DateTime.Now,
                    IdConteo = Guid.Empty,
                    IdCambioArticulo = linea.IdLinea, // Reutilizado: IdLinea para flujo OrdenConversion
                    CodigoEmpresa = orden.CodigoEmpresa,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    Estado = "PENDIENTE_ERP",
                    FechaCaducidad = cambioFecha ? fechaCaducidadDestino : fechaCaducidadOrigen,
                    Partida = partidaDestino,
                    PaletId = dto.PaletId,
                    CodigoPalet = codigoPalet,
                    ProcesadoPalet = false
                };

                if (dto.PaletId.HasValue)
                {
                    _context.InventarioAjustes.Add(ajusteEntrada);
                    await _context.SaveChangesAsync();
                    await Task.Delay(15000);
                    ajusteSalida.Fecha = DateTime.Now;
                    _context.InventarioAjustes.Add(ajusteSalida);
                }
                else
                {
                    _context.InventarioAjustes.Add(ajusteSalida);
                    _context.InventarioAjustes.Add(ajusteEntrada);
                }

                if (dto.PaletId.HasValue)
                {
                    string? descOrigen = null, descDestino = null;
                    try
                    {
                        descOrigen = await _sageDbContext.Articulos
                            .Where(a => a.CodigoEmpresa == orden.CodigoEmpresa && a.CodigoArticulo == orden.CodigoArticuloOrigen)
                            .Select(a => a.DescripcionArticulo)
                            .FirstOrDefaultAsync();
                    }
                    catch { }
                    if (cambioCodigo && !string.IsNullOrWhiteSpace(orden.CodigoArticuloDestino))
                    {
                        try
                        {
                            descDestino = await _sageDbContext.Articulos
                                .Where(a => a.CodigoEmpresa == orden.CodigoEmpresa && a.CodigoArticulo == orden.CodigoArticuloDestino)
                                .Select(a => a.DescripcionArticulo)
                                .FirstOrDefaultAsync();
                        }
                        catch { }
                    }

                    _context.TempPaletLineas.Add(new TempPaletLinea
                    {
                        Id = Guid.NewGuid(),
                        PaletId = dto.PaletId.Value,
                        CodigoEmpresa = orden.CodigoEmpresa,
                        CodigoArticulo = orden.CodigoArticuloOrigen,
                        DescripcionArticulo = descOrigen,
                        Cantidad = -dto.Cantidad,
                        UnidadMedida = "UN",
                        Lote = partidaLinea,
                        FechaCaducidad = fechaCaducidadOrigen,
                        CodigoAlmacen = dto.CodigoAlmacen,
                        Ubicacion = ubicacionNorm,
                        UsuarioId = dto.UsuarioEjecucionId,
                        FechaAgregado = DateTime.Now,
                        Observaciones = $"Conversión - {orden.TipoCambio}",
                        CambioArticuloId = linea.IdLinea, // Reutilizado: IdLinea para flujo OrdenConversion
                        Procesada = false,
                        EsHeredada = false
                    });
                    _context.TempPaletLineas.Add(new TempPaletLinea
                    {
                        Id = Guid.NewGuid(),
                        PaletId = dto.PaletId.Value,
                        CodigoEmpresa = orden.CodigoEmpresa,
                        CodigoArticulo = cambioCodigo ? orden.CodigoArticuloDestino! : orden.CodigoArticuloOrigen,
                        DescripcionArticulo = cambioCodigo ? descDestino : descOrigen,
                        Cantidad = cantidadDestino,
                        UnidadMedida = "UN",
                        Lote = partidaDestino,
                        FechaCaducidad = cambioFecha ? fechaCaducidadDestino : fechaCaducidadOrigen,
                        CodigoAlmacen = dto.CodigoAlmacen,
                        Ubicacion = ubicacionNorm,
                        UsuarioId = dto.UsuarioEjecucionId,
                        FechaAgregado = DateTime.Now,
                        Observaciones = $"Conversión - {orden.TipoCambio}",
                        CambioArticuloId = linea.IdLinea, // Reutilizado: IdLinea para flujo OrdenConversion
                        Procesada = false,
                        EsHeredada = false
                    });
                }

                orden.Estado = "EN_PROCESO";
                if (cantidadEjecutada + dto.Cantidad >= orden.Cantidad - 0.000001m)
                    orden.Estado = "COMPLETADO";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    IdLinea = linea.IdLinea,
                    AjusteSalidaId = ajusteSalida.IdAjuste,
                    AjusteEntradaId = ajusteEntrada.IdAjuste,
                    CantidadPendiente = orden.Cantidad - cantidadEjecutada - dto.Cantidad,
                    OrdenCompletada = orden.Estado == "COMPLETADO",
                    Mensaje = "Línea de conversión registrada correctamente."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al registrar línea de conversión: OrdenId={OrdenId}", id);
                return StatusCode(500, "Error interno al registrar la línea");
            }
        }
    }
}
