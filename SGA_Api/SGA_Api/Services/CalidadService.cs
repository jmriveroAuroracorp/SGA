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
                _logger.LogInformation("Iniciando bloqueo de stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                // 1. Verificar que no esté ya bloqueado
                var yaBloqueado = await _auroraSgaContext.BloqueosCalidad
                    .AnyAsync(b => b.CodigoEmpresa == dto.CodigoEmpresa &&
                                  b.CodigoArticulo == dto.CodigoArticulo &&
                                  b.LotePartida == dto.LotePartida &&
                                  b.Bloqueado == true);

                if (yaBloqueado)
                {
                    _logger.LogWarning("Stock ya está bloqueado para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                    return new { 
                        message = "El stock ya está bloqueado",
                        codigoEmpresa = dto.CodigoEmpresa,
                        codigoArticulo = dto.CodigoArticulo,
                        lotePartida = dto.LotePartida
                    };
                }

                // 2. Verificar que el stock existe
                var stockExiste = await _auroraSgaContext.StockDisponible
                    .AnyAsync(s => s.CodigoEmpresa == dto.CodigoEmpresa &&
                                  s.CodigoArticulo == dto.CodigoArticulo &&
                                  s.Partida == dto.LotePartida &&
                                  s.Disponible > 0);

                if (!stockExiste)
                {
                    _logger.LogWarning("Stock no encontrado para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                        dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                    return new { 
                        message = "No se encontró stock disponible para los parámetros especificados",
                        codigoEmpresa = dto.CodigoEmpresa,
                        codigoArticulo = dto.CodigoArticulo,
                        lotePartida = dto.LotePartida
                    };
                }

                // 3. Crear bloqueo
                var bloqueo = new BloqueoCalidad
                {
                    Id = Guid.NewGuid(),
                    CodigoEmpresa = dto.CodigoEmpresa,
                    CodigoArticulo = dto.CodigoArticulo,
                    LotePartida = dto.LotePartida,
                    CodigoAlmacen = dto.CodigoAlmacen,
                    Ubicacion = dto.Ubicacion,
                    Bloqueado = true,
                    UsuarioBloqueoId = dto.UsuarioId,
                    FechaBloqueo = DateTime.Now,
                    ComentarioBloqueo = dto.ComentarioBloqueo,
                    FechaCreacion = DateTime.Now,
                    FechaModificacion = DateTime.Now
                };

                _auroraSgaContext.BloqueosCalidad.Add(bloqueo);
                await _auroraSgaContext.SaveChangesAsync();

                _logger.LogInformation("Bloqueo creado exitosamente con ID {BloqueoId} para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    bloqueo.Id, dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);

                // TODO: Enviar notificación
                // await EnviarNotificacionBloqueoAsync(bloqueo);

                return new { 
                    Id = bloqueo.Id, 
                    Mensaje = "Stock bloqueado exitosamente",
                    FechaBloqueo = bloqueo.FechaBloqueo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al bloquear stock para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    dto.CodigoEmpresa, dto.CodigoArticulo, dto.LotePartida);
                throw;
            }
        }

        public async Task<bool> EstaStockBloqueadoAsync(short codigoEmpresa, string codigoArticulo, string lotePartida)
        {
            try
            {
                var estaBloqueado = await _auroraSgaContext.BloqueosCalidad
                    .AnyAsync(b => b.CodigoEmpresa == codigoEmpresa &&
                                  b.CodigoArticulo == codigoArticulo &&
                                  b.LotePartida == lotePartida &&
                                  b.Bloqueado == true);

                _logger.LogInformation("Verificación de bloqueo para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}: {EstaBloqueado}",
                    codigoEmpresa, codigoArticulo, lotePartida, estaBloqueado);

                return estaBloqueado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar bloqueo para empresa {CodigoEmpresa}, artículo {CodigoArticulo}, partida {Partida}",
                    codigoEmpresa, codigoArticulo, lotePartida);
                return false;
            }
        }

        private async Task<List<StockCalidadDto>> EnriquecerConEstadoBloqueosAsync(List<Models.Stock.StockDisponible> stockData)
        {
            var resultado = new List<StockCalidadDto>();

            foreach (var stock in stockData)
            {
                // Verificar si el stock está bloqueado
                var estaBloqueado = await EstaStockBloqueadoAsync(stock.CodigoEmpresa, stock.CodigoArticulo, stock.Partida);
                
                // Obtener información del bloqueo si existe
                BloqueoCalidad? bloqueoInfo = null;
                if (estaBloqueado)
                {
                    bloqueoInfo = await _auroraSgaContext.BloqueosCalidad
                        .FirstOrDefaultAsync(b => b.CodigoEmpresa == stock.CodigoEmpresa &&
                                                  b.CodigoArticulo == stock.CodigoArticulo &&
                                                  b.LotePartida == stock.Partida &&
                                                  b.Bloqueado == true);
                }

                var stockCalidad = new StockCalidadDto
                {
                    CodigoArticulo = stock.CodigoArticulo,
                    DescripcionArticulo = stock.DescripcionArticulo,
                    CodigoAlmacen = stock.CodigoAlmacen,
                    Almacen = stock.Almacen,
                    Ubicacion = stock.Ubicacion,
                    LotePartida = stock.Partida,
                    FechaCaducidad = stock.FechaCaducidad,
                    CantidadDisponible = stock.Disponible,
                    EstaBloqueado = estaBloqueado,
                    ComentarioBloqueo = bloqueoInfo?.ComentarioBloqueo,
                    FechaBloqueo = bloqueoInfo?.FechaBloqueo,
                    UsuarioBloqueo = bloqueoInfo?.UsuarioBloqueoId.ToString()
                };

                resultado.Add(stockCalidad);
            }

            return resultado;
        }

        public async Task<object> DesbloquearStockAsync(DesbloquearStockDto dto)
        {
            try
            {
                _logger.LogInformation("Iniciando desbloqueo de stock para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);

                // 1. Buscar el bloqueo
                var bloqueo = await _auroraSgaContext.BloqueosCalidad
                    .FirstOrDefaultAsync(b => b.Id == dto.IdBloqueo);

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
                _logger.LogError(ex, "Error al desbloquear stock para bloqueo ID {BloqueoId}",
                    dto.IdBloqueo);
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
    }
}
