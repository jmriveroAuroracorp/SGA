using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Calidad;

namespace SGA_Api.Services
{
    public class CalidadService : ICalidadService
    {
        private readonly AuroraSgaDbContext _auroraSgaContext;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<CalidadService> _logger;

        public CalidadService(
            AuroraSgaDbContext auroraSgaContext,
            SageDbContext sageContext,
            ILogger<CalidadService> logger)
        {
            _auroraSgaContext = auroraSgaContext;
            _sageContext = sageContext;
            _logger = logger;
        }

        public async Task<bool> VerificarPermisoCalidadAsync(int usuarioId)
        {
            try
            {
                var tienePermiso = await _sageContext.AccesosOperarios
                    .AnyAsync(a => a.Operario == usuarioId && a.MRH_CodigoAplicacion == 16);

                _logger.LogInformation("Verificación permiso Calidad para usuario {UsuarioId}: {TienePermiso}", 
                    usuarioId, tienePermiso);

                return tienePermiso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar permiso Calidad para usuario {UsuarioId}", usuarioId);
                return false;
            }
        }

        public async Task<bool> VerificarAccesoEmpresaAsync(int usuarioId, short codigoEmpresa)
        {
            try
            {
                var tieneAcceso = await _sageContext.OperariosEmpresas
                    .AnyAsync(oe => oe.Operario == usuarioId && oe.CodigoEmpresa == codigoEmpresa);

                _logger.LogInformation("Verificación acceso empresa {CodigoEmpresa} para usuario {UsuarioId}: {TieneAcceso}", 
                    codigoEmpresa, usuarioId, tieneAcceso);

                return tieneAcceso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar acceso empresa {CodigoEmpresa} para usuario {UsuarioId}", 
                    codigoEmpresa, usuarioId);
                return false;
            }
        }

        public async Task<List<StockCalidadDto>> BuscarStockPorArticuloYLoteAsync(
            short codigoEmpresa, 
            string codigoArticulo, 
            string partida, 
            string? codigoAlmacen = null, 
            string? codigoUbicacion = null)
        {
            try
            {
                _logger.LogInformation("Buscando stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}", 
                    codigoEmpresa, codigoArticulo, partida);

                // 1. Buscar en StockDisponible con filtros obligatorios
                var query = _auroraSgaContext.StockDisponible
                    .Where(s => s.CodigoEmpresa == codigoEmpresa &&           // OBLIGATORIO
                               s.CodigoArticulo == codigoArticulo &&          // OBLIGATORIO
                               s.Partida == partida &&                        // OBLIGATORIO
                               s.Disponible > 0);                            // Solo stock disponible

                // 2. Filtros opcionales
                if (!string.IsNullOrWhiteSpace(codigoAlmacen))
                    query = query.Where(s => s.CodigoAlmacen == codigoAlmacen);
                
                if (!string.IsNullOrWhiteSpace(codigoUbicacion))
                    query = query.Where(s => s.Ubicacion == codigoUbicacion);

                var stockData = await query.ToListAsync();

                _logger.LogInformation("Encontrados {Count} registros de stock", stockData.Count);

                // 3. Enriquecer con información de bloqueos
                var stockConBloqueos = await EnriquecerConEstadoBloqueosAsync(stockData);

                return stockConBloqueos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}", 
                    codigoEmpresa, codigoArticulo, partida);
                throw;
            }
        }

        public async Task<object> BloquearStockAsync(BloquearStockDto dto)
        {
            try
            {
                // 🔷 BLOQUEO GLOBAL: Si EsBloqueoGlobal es true, bloquear en todas las ubicaciones
                if (dto.EsBloqueoGlobal)
                {
                    _logger.LogInformation("Iniciando BLOQUEO GLOBAL para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                    // Buscar todas las ubicaciones donde existe stock del artículo/lote
                    var todasLasUbicaciones = await _auroraSgaContext.StockDisponible
                        .Where(s => s.CodigoEmpresa == dto.CodigoEmpresa &&
                                   s.CodigoArticulo == dto.CodigoArticulo &&
                                   s.Partida == dto.LotePartida &&
                                   s.Disponible > 0)
                        .Select(s => new { s.CodigoAlmacen, s.Ubicacion })
                        .Distinct()
                        .ToListAsync();

                    if (!todasLasUbicaciones.Any())
                    {
                        _logger.LogWarning("No se encontró stock disponible para bloqueo global - Empresa: {CodigoEmpresa}, Artículo: {CodigoArticulo}, Partida: {Partida}",
                            dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                        return new { 
                            message = "No se encontró stock disponible para este artículo y lote en ninguna ubicación",
                            codigoEmpresa = dto.CodigoEmpresa,
                            codigoArticulo = dto.CodigoArticulo,
                            lotePartida = dto.LotePartida
                        };
                    }

                    var bloqueosCreados = new List<Guid>();
                    var bloqueosDuplicados = 0;

                    // Validar tipo de bloqueo
                    var tipoBloqueo = !string.IsNullOrWhiteSpace(dto.TipoBloqueo) 
                        ? dto.TipoBloqueo.ToUpper() 
                        : "TOTAL";
                    
                    if (tipoBloqueo != "TOTAL" && tipoBloqueo != "SOLO_PULMON")
                    {
                        _logger.LogWarning("Tipo de bloqueo inválido: {TipoBloqueo}. Se usará TOTAL por defecto.", dto.TipoBloqueo);
                        tipoBloqueo = "TOTAL";
                    }

                    // Crear bloqueo para cada ubicación
                    foreach (var ubicacion in todasLasUbicaciones)
                    {
                        // Verificar si ya existe bloqueo en esta ubicación
                        var queryBloqueo = _auroraSgaContext.BloqueosCalidad
                            .Where(b => b.CodigoEmpresa == dto.CodigoEmpresa &&
                                       b.CodigoArticulo == dto.CodigoArticulo &&
                                       b.LotePartida == dto.LotePartida &&
                                       b.CodigoAlmacen == ubicacion.CodigoAlmacen &&
                                       b.Bloqueado == true);

                        if (!string.IsNullOrWhiteSpace(ubicacion.Ubicacion))
                        {
                            queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == ubicacion.Ubicacion);
                        }
                        else
                        {
                            queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                        }

                        if (await queryBloqueo.AnyAsync())
                        {
                            bloqueosDuplicados++;
                            _logger.LogInformation("Bloqueo ya existe en {Almacen}-{Ubicacion}, se omite", 
                                ubicacion.CodigoAlmacen, ubicacion.Ubicacion ?? "(sin ubicación)");
                            continue;
                        }

                        // Crear bloqueo para esta ubicación
                        var bloqueo = new BloqueoCalidad
                        {
                            Id = Guid.NewGuid(),
                            CodigoEmpresa = dto.CodigoEmpresa,
                            CodigoArticulo = dto.CodigoArticulo,
                            LotePartida = dto.LotePartida,
                            CodigoAlmacen = ubicacion.CodigoAlmacen,
                            Ubicacion = ubicacion.Ubicacion,
                            Bloqueado = true,
                            TipoBloqueo = tipoBloqueo,
                            UsuarioBloqueoId = dto.UsuarioId,
                            FechaBloqueo = DateTime.Now,
                            ComentarioBloqueo = $"[BLOQUEO GLOBAL] {dto.ComentarioBloqueo}",
                            FechaCreacion = DateTime.Now,
                            FechaModificacion = DateTime.Now
                        };

                        _auroraSgaContext.BloqueosCalidad.Add(bloqueo);
                        bloqueosCreados.Add(bloqueo.Id);
                    }

                    await _auroraSgaContext.SaveChangesAsync();

                    _logger.LogInformation("Bloqueo global completado: {Creados} bloqueos creados, {Duplicados} ya existían", 
                        bloqueosCreados.Count, bloqueosDuplicados);

                    return new { 
                        Mensaje = $"Bloqueo global aplicado: {bloqueosCreados.Count} ubicaciones bloqueadas",
                        UbicacionesBloqueadas = bloqueosCreados.Count,
                        UbicacionesYaBloqueadas = bloqueosDuplicados,
                        IdsBloqueos = bloqueosCreados,
                        TipoBloqueo = tipoBloqueo
                    };
                }

                // 🔷 BLOQUEO ESPECÍFICO: Lógica existente para bloqueo en ubicación específica
                _logger.LogInformation("Iniciando bloqueo de stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");

                // 1. Verificar que no esté ya bloqueado en esta ubicación específica
                var queryBloqueoEspecifico = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == dto.CodigoEmpresa &&
                               b.CodigoArticulo == dto.CodigoArticulo &&
                               b.LotePartida == dto.LotePartida &&
                               b.CodigoAlmacen == dto.CodigoAlmacen &&
                               b.Bloqueado == true);

                // Verificar por ubicación específica
                if (!string.IsNullOrWhiteSpace(dto.Ubicacion))
                {
                    queryBloqueoEspecifico = queryBloqueoEspecifico.Where(b => b.Ubicacion == dto.Ubicacion);
                }
                else
                {
                    queryBloqueoEspecifico = queryBloqueoEspecifico.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var yaBloqueado = await queryBloqueoEspecifico.AnyAsync();

                if (yaBloqueado)
                {
                    _logger.LogWarning("Stock ya está bloqueado para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");
                    return new { 
                        message = "El stock ya está bloqueado en esta ubicación",
                        codigoEmpresa = dto.CodigoEmpresa,
                        codigoArticulo = dto.CodigoArticulo,
                        lotePartida = dto.LotePartida,
                        codigoAlmacen = dto.CodigoAlmacen,
                        ubicacion = dto.Ubicacion
                    };
                }

                // 2. Verificar que el stock existe en la ubicación especificada
                var queryStock = _auroraSgaContext.StockDisponible
                    .Where(s => s.CodigoEmpresa == dto.CodigoEmpresa &&
                               s.CodigoArticulo == dto.CodigoArticulo &&
                               s.Partida == dto.LotePartida &&
                               s.CodigoAlmacen == dto.CodigoAlmacen &&
                               s.Disponible > 0);

                // Filtrar por ubicación si se especifica
                if (!string.IsNullOrWhiteSpace(dto.Ubicacion))
                {
                    queryStock = queryStock.Where(s => s.Ubicacion == dto.Ubicacion);
                }
                else
                {
                    queryStock = queryStock.Where(s => string.IsNullOrEmpty(s.Ubicacion));
                }

                var stockExiste = await queryStock.AnyAsync();

                if (!stockExiste)
                {
                    _logger.LogWarning("Stock no encontrado para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");
                    return new { 
                        message = "No se encontró stock disponible para los parámetros especificados en esta ubicación",
                        codigoEmpresa = dto.CodigoEmpresa,
                        codigoArticulo = dto.CodigoArticulo,
                        lotePartida = dto.LotePartida,
                        codigoAlmacen = dto.CodigoAlmacen,
                        ubicacion = dto.Ubicacion
                    };
                }

                // 3. Crear bloqueo
                var tipoBloqueoEspecifico = !string.IsNullOrWhiteSpace(dto.TipoBloqueo) 
                    ? dto.TipoBloqueo.ToUpper() 
                    : "TOTAL";
                
                // Validar que el tipo sea válido
                if (tipoBloqueoEspecifico != "TOTAL" && tipoBloqueoEspecifico != "SOLO_PULMON")
                {
                    _logger.LogWarning("Tipo de bloqueo inválido: {TipoBloqueo}. Se usará TOTAL por defecto.", dto.TipoBloqueo);
                    tipoBloqueoEspecifico = "TOTAL";
                }

                var bloqueoEspecifico = new BloqueoCalidad
                {
                    Id = Guid.NewGuid(),
                    CodigoEmpresa = dto.CodigoEmpresa,
                    CodigoArticulo = dto.CodigoArticulo,
                    LotePartida = dto.LotePartida,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    Ubicacion = dto.Ubicacion,
                    Bloqueado = true,
                    TipoBloqueo = tipoBloqueoEspecifico, // 🔷 NUEVO: Tipo de bloqueo
                    UsuarioBloqueoId = dto.UsuarioId,
                    FechaBloqueo = DateTime.Now,
                    ComentarioBloqueo = dto.ComentarioBloqueo,
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                };

                _auroraSgaContext.BloqueosCalidad.Add(bloqueoEspecifico);
                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation("Bloqueo creado exitosamente con ID {BloqueoId} para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    bloqueoEspecifico.Id, dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");

                // TODO: Enviar notificación
                // await EnviarNotificacionBloqueoAsync(bloqueoEspecifico);

                return new { 
                    Id = bloqueoEspecifico.Id, 
                    Mensaje = "Stock bloqueado exitosamente",
                    FechaBloqueo = bloqueoEspecifico.FechaBloqueo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al bloquear stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida, dto.CodigoAlmacen, dto.Ubicacion ?? "(sin ubicación)");
                throw;
            }
        }

        public async Task<bool> EstaStockBloqueadoAsync(short codigoEmpresa, string codigoArticulo, string lotePartida, string codigoAlmacen, string? ubicacion = null)
        {
            try
            {
                var query = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                               b.CodigoArticulo == codigoArticulo &&
                               b.LotePartida == lotePartida &&
                               b.CodigoAlmacen == codigoAlmacen &&
                               b.Bloqueado == true);

                // Verificar por ubicación específica
                if (!string.IsNullOrWhiteSpace(ubicacion))
                {
                    query = query.Where(b => b.Ubicacion == ubicacion);
                }
                else
                {
                    // Si no se especifica ubicación, buscar bloqueos sin ubicación
                    query = query.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var estaBloqueado = await query.AnyAsync();

                _logger.LogInformation("Verificación de bloqueo para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}: {EstaBloqueado}",
                    codigoEmpresa, codigoArticulo, lotePartida, codigoAlmacen, ubicacion ?? "(sin ubicación)", estaBloqueado);

                return estaBloqueado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar bloqueo para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}, almacén {Almacen}, ubicación {Ubicacion}",
                    codigoEmpresa, codigoArticulo, lotePartida, codigoAlmacen, ubicacion ?? "(sin ubicación)");
                return false;
            }
        }

        private async Task<List<StockCalidadDto>> EnriquecerConEstadoBloqueosAsync(List<Models.Stock.StockDisponible> stockData)
        {
            var resultado = new List<StockCalidadDto>();

            // 🔷 ACTUALIZADO: Procesar cada registro individualmente para verificar bloqueos por ubicación específica
            foreach (var stock in stockData)
            {
                // Verificar si este stock específico está bloqueado (por ubicación)
                var estaBloqueado = await EstaStockBloqueadoAsync(
                    stock.CodigoEmpresa, 
                    stock.CodigoArticulo, 
                    stock.Partida,
                    stock.CodigoAlmacen,
                    stock.Ubicacion);
                
                // Obtener información del bloqueo si existe
                BloqueoCalidad? bloqueoInfo = null;
                if (estaBloqueado)
                {
                    var queryBloqueo = _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == stock.CodigoEmpresa &&
                                   b.CodigoArticulo == stock.CodigoArticulo &&
                                   b.LotePartida == stock.Partida &&
                                   b.CodigoAlmacen == stock.CodigoAlmacen &&
                                   b.Bloqueado == true);

                    // Filtrar por ubicación específica
                    if (!string.IsNullOrWhiteSpace(stock.Ubicacion))
                    {
                        queryBloqueo = queryBloqueo.Where(b => b.Ubicacion == stock.Ubicacion);
                    }
                    else
                    {
                        queryBloqueo = queryBloqueo.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                    }

                    bloqueoInfo = await queryBloqueo
                        .OrderByDescending(b => b.FechaBloqueo)
                        .FirstOrDefaultAsync();
                }

                // 🔷 NUEVO: Obtener información de palet si está paletizado
                var paletInfo = await _auroraSgaContext.PaletLineas
                    .Where(pl => pl.CodigoEmpresa == stock.CodigoEmpresa &&
                                pl.CodigoArticulo == stock.CodigoArticulo &&
                                pl.CodigoAlmacen == stock.CodigoAlmacen &&
                                pl.Ubicacion == stock.Ubicacion &&
                                pl.Lote == stock.Partida &&
                                pl.Cantidad > 0)
                    .Include(pl => pl.Palet)
                    .Where(pl => pl.Palet.Estado.ToUpper() == "ABIERTO" || pl.Palet.Estado.ToUpper() == "CERRADO")
                    .Select(pl => new
                    {
                        pl.PaletId,
                        pl.Palet.Codigo,
                        pl.Palet.Estado
                    })
                    .FirstOrDefaultAsync();

                var stockCalidad = new StockCalidadDto
                {
                    CodigoArticulo = stock.CodigoArticulo,
                    DescripcionArticulo = stock.DescripcionArticulo,
                    CodigoAlmacen = stock.CodigoAlmacen,
                    Almacen = stock.Almacen,
                    Ubicacion = stock.Ubicacion ?? "Sin ubicación específica",
                    LotePartida = stock.Partida,
                    FechaCaducidad = stock.FechaCaducidad,
                    CantidadDisponible = stock.Disponible,
                    EstaBloqueado = estaBloqueado,
                    ComentarioBloqueo = bloqueoInfo?.ComentarioBloqueo,
                    FechaBloqueo = bloqueoInfo?.FechaBloqueo,
                    UsuarioBloqueo = bloqueoInfo?.UsuarioBloqueoId.ToString(),
                    // 🔷 NUEVO: Información de palet
                    PaletId = paletInfo?.PaletId,
                    CodigoPalet = paletInfo?.Codigo,
                    EstadoPalet = paletInfo?.Estado
                };

                resultado.Add(stockCalidad);
            }

            return resultado;
        }

        public async Task<object> DesbloquearStockAsync(DesbloquearStockDto dto)
        {
            try
            {
                // 🔷 DESBLOQUEO GLOBAL: Si EsDesbloqueoGlobal es true, desbloquear en todas las ubicaciones
                if (dto.EsDesbloqueoGlobal && dto.CodigoEmpresa.HasValue && 
                    !string.IsNullOrWhiteSpace(dto.CodigoArticulo) && 
                    !string.IsNullOrWhiteSpace(dto.LotePartida))
                {
                    _logger.LogInformation("Iniciando DESBLOQUEO GLOBAL para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                    // Buscar todos los bloqueos activos del artículo y lote
                    var bloqueosGlobales = await _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == dto.CodigoEmpresa.Value &&
                                   b.CodigoArticulo == dto.CodigoArticulo &&
                                   b.LotePartida == dto.LotePartida &&
                                   b.Bloqueado == true)
                        .ToListAsync();

                    if (!bloqueosGlobales.Any())
                    {
                        _logger.LogWarning("No se encontraron bloqueos activos para desbloqueo global - Empresa: {CodigoEmpresa}, Artículo: {CodigoArticulo}, Partida: {Partida}",
                            dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                        return new { 
                            message = "No se encontraron bloqueos activos para este artículo y lote",
                            codigoEmpresa = dto.CodigoEmpresa,
                            codigoArticulo = dto.CodigoArticulo,
                            lotePartida = dto.LotePartida
                        };
                    }

                    var bloqueosDesbloqueados = new List<Guid>();
                    var fechaDesbloqueo = DateTime.Now;

                    // Desbloquear todos los bloqueos encontrados
                    foreach (var bloqueoGlobal in bloqueosGlobales)
                    {
                        bloqueoGlobal.Bloqueado = false;
                        bloqueoGlobal.UsuarioDesbloqueoId = dto.UsuarioId;
                        bloqueoGlobal.FechaDesbloqueo = fechaDesbloqueo;
                        bloqueoGlobal.ComentarioDesbloqueo = $"[DESBLOQUEO GLOBAL] {dto.ComentarioDesbloqueo}";
                        bloqueoGlobal.FechaModificacion = DateTime.Now;
                        bloqueosDesbloqueados.Add(bloqueoGlobal.Id);
                    }

                    await _auroraSgaContext.SaveChangesAsync();

                    _logger.LogInformation("Desbloqueo global completado: {Desbloqueados} bloqueos desbloqueados", 
                        bloqueosDesbloqueados.Count);

                    return new { 
                        Mensaje = $"Desbloqueo global aplicado: {bloqueosDesbloqueados.Count} ubicaciones desbloqueadas",
                        UbicacionesDesbloqueadas = bloqueosDesbloqueados.Count,
                        IdsBloqueos = bloqueosDesbloqueados,
                        FechaDesbloqueo = fechaDesbloqueo
                    };
                }

                // 🔷 DESBLOQUEO ESPECÍFICO: Lógica existente para desbloqueo de un bloqueo específico
                if (!dto.IdBloqueo.HasValue)
                {
                    _logger.LogWarning("Desbloqueo específico requiere IdBloqueo");
                    return new { 
                        message = "Se requiere IdBloqueo para desbloqueo específico o campos para desbloqueo global"
                    };
                }

                _logger.LogInformation("Iniciando desbloqueo de stock para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);

                // 1. Buscar el bloqueo
                var bloqueo = await _auroraSgaContext.BloqueosCalidad
                    .FirstOrDefaultAsync(b => b.Id == dto.IdBloqueo.Value);

                if (bloqueo == null)
                {
                    _logger.LogWarning("Bloqueo no encontrado con ID {BloqueoId}", dto.IdBloqueo);
                    return new { 
                        message = "No se encontró el bloqueo especificado",
                        idBloqueo = dto.IdBloqueo
                    };
                }

                // 2. Verificar que esté bloqueado
                if (!bloqueo.Bloqueado)
                {
                    _logger.LogWarning("El bloqueo ID {BloqueoId} ya está desbloqueado", dto.IdBloqueo);
                    return new { 
                        message = "El bloqueo especificado ya está desbloqueado",
                        idBloqueo = dto.IdBloqueo
                    };
                }

                // 3. Actualizar bloqueo
                bloqueo.Bloqueado = false;
                bloqueo.UsuarioDesbloqueoId = dto.UsuarioId;
                bloqueo.FechaDesbloqueo = DateTime.Now;
                bloqueo.ComentarioDesbloqueo = dto.ComentarioDesbloqueo;
                bloqueo.FechaModificacion = DateTime.Now;

                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation("Desbloqueo ejecutado exitosamente para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);

                // TODO: Enviar notificación
                // await EnviarNotificacionDesbloqueoAsync(bloqueo);

                return new { 
                    Id = bloqueo.Id, 
                    Mensaje = "Stock desbloqueado exitosamente",
                    FechaDesbloqueo = bloqueo.FechaDesbloqueo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desbloquear stock");
                throw;
            }
        }

        public async Task<List<BloqueoCalidadDto>> ObtenerBloqueosAsync(short codigoEmpresa, bool? soloBloqueados = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo bloqueos para empresa {CodigoEmpresa}, soloBloqueados: {SoloBloqueados}",
                    codigoEmpresa, soloBloqueados);

                var query = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa);

                // Filtro opcional por estado
                if (soloBloqueados.HasValue)
                {
                    query = query.Where(b => b.Bloqueado == soloBloqueados.Value);
                }

                var bloqueos = await query
                    .OrderByDescending(b => b.FechaBloqueo)
                    .ToListAsync();

                var resultado = new List<BloqueoCalidadDto>();

                foreach (var bloqueo in bloqueos)
                {
                    // Obtener información del artículo desde StockDisponible
                    var stockInfo = await _auroraSgaContext.StockDisponible
                        .FirstOrDefaultAsync(s => s.CodigoEmpresa == bloqueo.CodigoEmpresa &&
                                                 s.CodigoArticulo == bloqueo.CodigoArticulo &&
                                                 s.Partida == bloqueo.LotePartida);

                    var bloqueoDto = new BloqueoCalidadDto
                    {
                        Id = bloqueo.Id,
                        CodigoArticulo = bloqueo.CodigoArticulo,
                        DescripcionArticulo = stockInfo?.DescripcionArticulo ?? "N/A",
                        LotePartida = bloqueo.LotePartida,
                        CodigoAlmacen = bloqueo.CodigoAlmacen,
                        Almacen = stockInfo?.Almacen ?? "N/A",
                        Ubicacion = bloqueo.Ubicacion,
                        Bloqueado = bloqueo.Bloqueado,
                        TipoBloqueo = bloqueo.TipoBloqueo ?? "TOTAL", // 🔷 NUEVO: Tipo de bloqueo
                        UsuarioBloqueo = bloqueo.UsuarioBloqueoId.ToString(),
                        FechaBloqueo = bloqueo.FechaBloqueo,
                        ComentarioBloqueo = bloqueo.ComentarioBloqueo,
                        UsuarioDesbloqueo = bloqueo.UsuarioDesbloqueoId?.ToString(),
                        FechaDesbloqueo = bloqueo.FechaDesbloqueo,
                        ComentarioDesbloqueo = bloqueo.ComentarioDesbloqueo
                    };

                    resultado.Add(bloqueoDto);
                }

                _logger.LogInformation("Encontrados {Count} bloqueos para empresa {CodigoEmpresa}",
                    resultado.Count, codigoEmpresa);

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener bloqueos para empresa {CodigoEmpresa}",
                    codigoEmpresa);
                throw;
            }
        }

        /// <summary>
        /// 🔷 NUEVO: Obtiene información de bloqueos de calidad para una lista de artículos
        /// </summary>
        public async Task<Dictionary<string, object>> ObtenerBloqueosPorArticulosAsync(
            short codigoEmpresa, 
            List<string> codigosArticulos)
        {
            try
            {
                _logger.LogInformation("Obteniendo bloqueos para empresa {CodigoEmpresa}, artículos: {Count}",
                    codigoEmpresa, codigosArticulos.Count);

                var resultado = new Dictionary<string, object>();

                foreach (var codigoArticulo in codigosArticulos)
                {
                    // Buscar bloqueos activos para este artículo
                    var bloqueos = await _auroraSgaContext.BloqueosCalidad
                        .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                                   b.CodigoArticulo == codigoArticulo &&
                                   b.Bloqueado == true)
                        .OrderByDescending(b => b.FechaBloqueo)
                        .ToListAsync();

                    if (bloqueos.Any())
                    {
                        // Si hay bloqueos, tomar el más reciente
                        var bloqueoMasReciente = bloqueos.First();
                         resultado[codigoArticulo] = new
                         {
                             isBloqueado = true,
                             motivoBloqueo = bloqueoMasReciente.ComentarioBloqueo,
                             fechaBloqueo = bloqueoMasReciente.FechaBloqueo,
                             usuarioBloqueo = bloqueoMasReciente.UsuarioBloqueoId.ToString(),
                             idBloqueo = bloqueoMasReciente.Id
                         };
                    }
                    else
                    {
                        // Si no hay bloqueos, indicar que no está bloqueado
                         resultado[codigoArticulo] = new
                         {
                             isBloqueado = false,
                             motivoBloqueo = (string?)null,
                             fechaBloqueo = (DateTime?)null,
                             usuarioBloqueo = (string?)null,
                             idBloqueo = (Guid?)null
                         };
                    }
                }

                _logger.LogInformation("Procesados {Count} artículos para empresa {CodigoEmpresa}",
                    resultado.Count, codigoEmpresa);

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener bloqueos por artículos para empresa {CodigoEmpresa}",
                    codigoEmpresa);
                throw;
            }
        }

        /// <summary>
        /// 🔷 NUEVO: Copia un bloqueo de calidad desde una ubicación origen a una ubicación destino
        /// </summary>
        public async Task<bool> CopiarBloqueoCalidadAsync(
            short codigoEmpresa,
            string codigoArticulo,
            string lotePartida,
            string almacenOrigen,
            string? ubicacionOrigen,
            string almacenDestino,
            string? ubicacionDestino)
        {
            try
            {
                // Normalizar ubicaciones (null o vacío = "sin ubicación")
                var ubicacionOrigenNormalizada = string.IsNullOrWhiteSpace(ubicacionOrigen) ? "" : ubicacionOrigen.Trim();
                var ubicacionDestinoNormalizada = string.IsNullOrWhiteSpace(ubicacionDestino) ? "" : ubicacionDestino.Trim();

                _logger.LogInformation($"🔍 CopiarBloqueoCalidad - Artículo: {codigoArticulo}, Partida: {lotePartida}, Origen: {almacenOrigen}-{ubicacionOrigenNormalizada}, Destino: {almacenDestino}-{ubicacionDestinoNormalizada}");

                // 1. Buscar bloqueo en origen
                var queryBloqueoOrigen = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                               b.CodigoArticulo == codigoArticulo &&
                               b.LotePartida == lotePartida &&
                               b.CodigoAlmacen == almacenOrigen &&
                               b.Bloqueado == true);

                // Filtrar por ubicación origen
                if (!string.IsNullOrWhiteSpace(ubicacionOrigenNormalizada))
                {
                    queryBloqueoOrigen = queryBloqueoOrigen.Where(b => b.Ubicacion == ubicacionOrigenNormalizada);
                }
                else
                {
                    queryBloqueoOrigen = queryBloqueoOrigen.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var bloqueoOrigen = await queryBloqueoOrigen
                    .OrderByDescending(b => b.FechaBloqueo)
                    .FirstOrDefaultAsync();

                if (bloqueoOrigen == null)
                {
                    _logger.LogInformation($"✅ No hay bloqueo en origen para copiar - Artículo: {codigoArticulo}, Partida: {lotePartida}, Origen: {almacenOrigen}-{ubicacionOrigenNormalizada}");
                    return false; // No hay bloqueo en origen, no hay nada que copiar
                }

                // 2. Verificar si ya existe bloqueo en destino (evitar duplicados)
                var queryBloqueoDestino = _auroraSgaContext.BloqueosCalidad
                    .Where(b => b.CodigoEmpresa == codigoEmpresa &&
                               b.CodigoArticulo == codigoArticulo &&
                               b.LotePartida == lotePartida &&
                               b.CodigoAlmacen == almacenDestino &&
                               b.Bloqueado == true);

                // Filtrar por ubicación destino
                if (!string.IsNullOrWhiteSpace(ubicacionDestinoNormalizada))
                {
                    queryBloqueoDestino = queryBloqueoDestino.Where(b => b.Ubicacion == ubicacionDestinoNormalizada);
                }
                else
                {
                    queryBloqueoDestino = queryBloqueoDestino.Where(b => string.IsNullOrEmpty(b.Ubicacion));
                }

                var bloqueoDestinoExistente = await queryBloqueoDestino.AnyAsync();

                if (bloqueoDestinoExistente)
                {
                    _logger.LogInformation($"⚠️ Ya existe bloqueo en destino - Artículo: {codigoArticulo}, Partida: {lotePartida}, Destino: {almacenDestino}-{ubicacionDestinoNormalizada}. No se copia para evitar duplicados.");
                    return false; // Ya existe bloqueo en destino, no copiar
                }

                // 3. Crear bloqueo en destino copiando datos del origen
                var bloqueoDestino = new BloqueoCalidad
                {
                    Id = Guid.NewGuid(),
                    CodigoEmpresa = bloqueoOrigen.CodigoEmpresa,
                    CodigoArticulo = bloqueoOrigen.CodigoArticulo,
                    LotePartida = bloqueoOrigen.LotePartida,
                    CodigoAlmacen = almacenDestino,
                    Ubicacion = ubicacionDestinoNormalizada,
                    Bloqueado = true,
                    TipoBloqueo = bloqueoOrigen.TipoBloqueo ?? "TOTAL", // Copiar el tipo de bloqueo
                    UsuarioBloqueoId = bloqueoOrigen.UsuarioBloqueoId,
                    FechaBloqueo = DateTime.Now, // Nueva fecha de bloqueo
                    ComentarioBloqueo = $"Bloqueo copiado desde {almacenOrigen}-{ubicacionOrigenNormalizada}. Original: {bloqueoOrigen.ComentarioBloqueo}",
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                };

                _auroraSgaContext.BloqueosCalidad.Add(bloqueoDestino);
                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation($"✅ Bloqueo copiado exitosamente - ID Origen: {bloqueoOrigen.Id}, ID Destino: {bloqueoDestino.Id}, Tipo: {bloqueoDestino.TipoBloqueo}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al copiar bloqueo de calidad - Artículo: {codigoArticulo}, Partida: {lotePartida}, Origen: {almacenOrigen}-{ubicacionOrigen}, Destino: {almacenDestino}-{ubicacionDestino}");
                // En caso de error, no fallar el traspaso, solo loguear
                return false;
            }
        }
    }
}
