using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Notificaciones;

namespace SGA_Api.Services
{
    /// <summary>
    /// Servicio para la gestión de notificaciones en base de datos
    /// </summary>
    public class NotificacionesService : INotificacionesService
    {
        private readonly AuroraSgaDbContext _context;
        private readonly ILogger<NotificacionesService> _logger;

        public NotificacionesService(AuroraSgaDbContext context, ILogger<NotificacionesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Crea una nueva notificación en la base de datos
        /// </summary>
        public async Task<Notificacion> CrearNotificacionAsync(CrearNotificacionDto crearDto)
        {
            try
            {
                var notificacion = new Notificacion
                {
                    IdNotificacion = Guid.NewGuid(),
                    CodigoEmpresa = crearDto.CodigoEmpresa,
                    TipoNotificacion = crearDto.TipoNotificacion,
                    ProcesoId = crearDto.ProcesoId,
                    Titulo = crearDto.Titulo,
                    Mensaje = crearDto.Mensaje,
                    EstadoAnterior = crearDto.EstadoAnterior,
                    EstadoActual = crearDto.EstadoActual,
                    EsGrupal = crearDto.EsGrupal,
                    GrupoDestino = crearDto.GrupoDestino,
                    Comentario = crearDto.Comentario
                };

                _context.Notificaciones.Add(notificacion);

                // Agregar destinatarios
                foreach (var usuarioId in crearDto.UsuarioIds)
                {
                    var destinatario = new NotificacionDestinatario
                    {
                        IdDestinatario = Guid.NewGuid(),
                        IdNotificacion = notificacion.IdNotificacion,
                        UsuarioId = usuarioId
                    };
                    _context.NotificacionesDestinatarios.Add(destinatario);
                }

                await _context.SaveChangesAsync();

                _logger.LogDebug("Notificación creada: {IdNotificacion} - {Titulo}", notificacion.IdNotificacion, notificacion.Titulo);
                return notificacion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación");
                throw;
            }
        }

        /// <summary>
        /// Obtiene las notificaciones de un usuario específico
        /// </summary>
        public async Task<List<NotificacionDto>> ObtenerNotificacionesUsuarioAsync(int usuarioId, bool soloNoLeidas = false, int? limit = null)
        {
            try
            {
                var query = _context.Notificaciones
                    .Where(n => n.EsActiva && 
                               n.Destinatarios.Any(d => d.UsuarioId == usuarioId && d.EsActiva));

                if (soloNoLeidas)
                {
                    query = query.Where(n => !n.Lecturas.Any(l => l.UsuarioId == usuarioId));
                }

                query = query.OrderByDescending(n => n.FechaCreacion);

                if (limit.HasValue)
                {
                    query = query.Take(limit.Value);
                }

                var notificaciones = await query
                    .Select(n => new NotificacionDto
                    {
                        IdNotificacion = n.IdNotificacion,
                        CodigoEmpresa = n.CodigoEmpresa,
                        TipoNotificacion = n.TipoNotificacion,
                        ProcesoId = n.ProcesoId,
                        Titulo = n.Titulo,
                        Mensaje = n.Mensaje,
                        EstadoAnterior = n.EstadoAnterior,
                        EstadoActual = n.EstadoActual,
                        FechaCreacion = n.FechaCreacion,
                        EsActiva = n.EsActiva,
                        EsGrupal = n.EsGrupal,
                        GrupoDestino = n.GrupoDestino,
                        Comentario = n.Comentario,
                        Leida = n.Lecturas.Any(l => l.UsuarioId == usuarioId),
                        FechaLeida = n.Lecturas
                            .Where(l => l.UsuarioId == usuarioId)
                            .Select(l => l.FechaLeida)
                            .FirstOrDefault(),
                        Destinatarios = n.Destinatarios
                            .Where(d => d.EsActiva)
                            .Select(d => new NotificacionDestinatarioDto
                            {
                                IdDestinatario = d.IdDestinatario,
                                IdNotificacion = d.IdNotificacion,
                                UsuarioId = d.UsuarioId,
                                FechaCreacion = d.FechaCreacion,
                                EsActiva = d.EsActiva,
                                Leida = n.Lecturas.Any(l => l.UsuarioId == d.UsuarioId),
                                FechaLeida = n.Lecturas
                                    .Where(l => l.UsuarioId == d.UsuarioId)
                                    .Select(l => l.FechaLeida)
                                    .FirstOrDefault()
                            }).ToList()
                    })
                    .ToListAsync();

                return notificaciones;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener notificaciones para usuario {UsuarioId}", usuarioId);
                throw;
            }
        }

        /// <summary>
        /// Obtiene el resumen de notificaciones para un usuario
        /// </summary>
        public async Task<List<NotificacionResumenDto>> ObtenerResumenNotificacionesUsuarioAsync(int usuarioId, int? limit = null)
        {
            try
            {
                var query = _context.Notificaciones
                    .Where(n => n.EsActiva && 
                               n.Destinatarios.Any(d => d.UsuarioId == usuarioId && d.EsActiva))
                    .OrderByDescending(n => n.FechaCreacion);

                var selectQuery = query.Select(n => new NotificacionResumenDto
                {
                    IdNotificacion = n.IdNotificacion,
                    TipoNotificacion = n.TipoNotificacion,
                    Titulo = n.Titulo,
                    MensajeResumido = n.Mensaje.Length > 100 ? n.Mensaje.Substring(0, 100) + "..." : n.Mensaje,
                    FechaCreacion = n.FechaCreacion,
                    Leida = n.Lecturas.Any(l => l.UsuarioId == usuarioId),
                    FechaLeida = n.Lecturas
                        .Where(l => l.UsuarioId == usuarioId)
                        .Select(l => l.FechaLeida)
                        .FirstOrDefault(),
                    EstadoActual = n.EstadoActual,
                    ProcesoId = n.ProcesoId
                });

                if (limit.HasValue)
                {
                    selectQuery = selectQuery.Take(limit.Value);
                }

                var notificaciones = await selectQuery.ToListAsync();

                return notificaciones;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen de notificaciones para usuario {UsuarioId}", usuarioId);
                throw;
            }
        }

        /// <summary>
        /// Marca una notificación como leída para un usuario específico
        /// </summary>
        public async Task<bool> MarcarComoLeidaAsync(Guid idNotificacion, int usuarioId)
        {
            try
            {
                // Verificar si ya está marcada como leída
                var yaLeida = await _context.NotificacionesLecturas
                    .AnyAsync(l => l.IdNotificacion == idNotificacion && l.UsuarioId == usuarioId);

                if (yaLeida)
                {
                    return true; // Ya estaba marcada como leída
                }

                var lectura = new NotificacionLectura
                {
                    IdLectura = Guid.NewGuid(),
                    IdNotificacion = idNotificacion,
                    UsuarioId = usuarioId,
                    FechaLeida = DateTime.Now
                };

                _context.NotificacionesLecturas.Add(lectura);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Notificación marcada como leída: {IdNotificacion} - Usuario: {UsuarioId}", idNotificacion, usuarioId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar notificación como leída: {IdNotificacion} - Usuario: {UsuarioId}", idNotificacion, usuarioId);
                return false;
            }
        }

        /// <summary>
        /// Marca múltiples notificaciones como leídas para un usuario específico
        /// </summary>
        public async Task<int> MarcarMultiplesComoLeidasAsync(List<Guid> idNotificaciones, int usuarioId)
        {
            try
            {
                var lecturasExistentes = await _context.NotificacionesLecturas
                    .Where(l => idNotificaciones.Contains(l.IdNotificacion) && l.UsuarioId == usuarioId)
                    .Select(l => l.IdNotificacion)
                    .ToListAsync();

                var notificacionesParaMarcar = idNotificaciones
                    .Where(id => !lecturasExistentes.Contains(id))
                    .ToList();

                var nuevasLecturas = notificacionesParaMarcar.Select(id => new NotificacionLectura
                {
                    IdLectura = Guid.NewGuid(),
                    IdNotificacion = id,
                    UsuarioId = usuarioId,
                    FechaLeida = DateTime.Now
                }).ToList();

                _context.NotificacionesLecturas.AddRange(nuevasLecturas);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Marcadas {Cantidad} notificaciones como leídas para usuario {UsuarioId}", nuevasLecturas.Count, usuarioId);
                return nuevasLecturas.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar múltiples notificaciones como leídas para usuario {UsuarioId}", usuarioId);
                return 0;
            }
        }

        /// <summary>
        /// Obtiene el conteo de notificaciones no leídas para un usuario
        /// </summary>
        public async Task<int> ObtenerConteoNoLeidasAsync(int usuarioId)
        {
            try
            {
                var conteo = await _context.Notificaciones
                    .Where(n => n.EsActiva && 
                               n.Destinatarios.Any(d => d.UsuarioId == usuarioId && d.EsActiva) &&
                               !n.Lecturas.Any(l => l.UsuarioId == usuarioId))
                    .CountAsync();

                return conteo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener conteo de notificaciones no leídas para usuario {UsuarioId}", usuarioId);
                return 0;
            }
        }

        /// <summary>
        /// Obtiene una notificación específica por ID
        /// </summary>
        public async Task<NotificacionDto?> ObtenerNotificacionPorIdAsync(Guid idNotificacion, int usuarioId)
        {
            try
            {
                var notificacion = await _context.Notificaciones
                    .Where(n => n.IdNotificacion == idNotificacion && 
                               n.EsActiva && 
                               n.Destinatarios.Any(d => d.UsuarioId == usuarioId && d.EsActiva))
                    .Select(n => new NotificacionDto
                    {
                        IdNotificacion = n.IdNotificacion,
                        CodigoEmpresa = n.CodigoEmpresa,
                        TipoNotificacion = n.TipoNotificacion,
                        ProcesoId = n.ProcesoId,
                        Titulo = n.Titulo,
                        Mensaje = n.Mensaje,
                        EstadoAnterior = n.EstadoAnterior,
                        EstadoActual = n.EstadoActual,
                        FechaCreacion = n.FechaCreacion,
                        EsActiva = n.EsActiva,
                        EsGrupal = n.EsGrupal,
                        GrupoDestino = n.GrupoDestino,
                        Comentario = n.Comentario,
                        Leida = n.Lecturas.Any(l => l.UsuarioId == usuarioId),
                        FechaLeida = n.Lecturas
                            .Where(l => l.UsuarioId == usuarioId)
                            .Select(l => l.FechaLeida)
                            .FirstOrDefault(),
                        Destinatarios = n.Destinatarios
                            .Where(d => d.EsActiva)
                            .Select(d => new NotificacionDestinatarioDto
                            {
                                IdDestinatario = d.IdDestinatario,
                                IdNotificacion = d.IdNotificacion,
                                UsuarioId = d.UsuarioId,
                                FechaCreacion = d.FechaCreacion,
                                EsActiva = d.EsActiva,
                                Leida = n.Lecturas.Any(l => l.UsuarioId == d.UsuarioId),
                                FechaLeida = n.Lecturas
                                    .Where(l => l.UsuarioId == d.UsuarioId)
                                    .Select(l => l.FechaLeida)
                                    .FirstOrDefault()
                            }).ToList()
                    })
                    .FirstOrDefaultAsync();

                return notificacion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener notificación {IdNotificacion} para usuario {UsuarioId}", idNotificacion, usuarioId);
                return null;
            }
        }

        /// <summary>
        /// Elimina una notificación (soft delete - marca como inactiva)
        /// </summary>
        public async Task<bool> EliminarNotificacionAsync(Guid idNotificacion, int usuarioId)
        {
            try
            {
                var notificacion = await _context.Notificaciones
                    .FirstOrDefaultAsync(n => n.IdNotificacion == idNotificacion && 
                                             n.EsActiva && 
                                             n.Destinatarios.Any(d => d.UsuarioId == usuarioId && d.EsActiva));

                if (notificacion == null)
                {
                    return false;
                }

                notificacion.EsActiva = false;
                await _context.SaveChangesAsync();

                _logger.LogDebug("Notificación eliminada (soft delete): {IdNotificacion} por usuario {UsuarioId}", idNotificacion, usuarioId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar notificación {IdNotificacion} por usuario {UsuarioId}", idNotificacion, usuarioId);
                return false;
            }
        }

        /// <summary>
        /// Marca todas las notificaciones de un usuario como leídas
        /// </summary>
        public async Task<int> MarcarTodasComoLeidasAsync(int usuarioId)
        {
            try
            {
                // Obtener todas las notificaciones no leídas del usuario
                var notificacionesNoLeidas = await _context.Notificaciones
                    .Where(n => n.EsActiva && 
                               n.Destinatarios.Any(d => d.UsuarioId == usuarioId && d.EsActiva) &&
                               !n.Lecturas.Any(l => l.UsuarioId == usuarioId))
                    .Select(n => n.IdNotificacion)
                    .ToListAsync();

                if (!notificacionesNoLeidas.Any())
                {
                    return 0; // No hay notificaciones para marcar como leídas
                }

                // Crear las lecturas
                var nuevasLecturas = notificacionesNoLeidas.Select(id => new NotificacionLectura
                {
                    IdLectura = Guid.NewGuid(),
                    IdNotificacion = id,
                    UsuarioId = usuarioId,
                    FechaLeida = DateTime.Now
                }).ToList();

                _context.NotificacionesLecturas.AddRange(nuevasLecturas);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Marcadas {Cantidad} notificaciones como leídas para usuario {UsuarioId}", nuevasLecturas.Count, usuarioId);
                return nuevasLecturas.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar todas las notificaciones como leídas para usuario {UsuarioId}", usuarioId);
                return 0;
            }
        }

        /// <summary>
        /// Obtiene notificaciones por tipo y proceso
        /// </summary>
        public async Task<List<NotificacionDto>> ObtenerNotificacionesPorProcesoAsync(string tipoNotificacion, Guid procesoId)
        {
            try
            {
                var notificaciones = await _context.Notificaciones
                    .Where(n => n.EsActiva && 
                               n.TipoNotificacion == tipoNotificacion && 
                               n.ProcesoId == procesoId)
                    .OrderByDescending(n => n.FechaCreacion)
                    .Select(n => new NotificacionDto
                    {
                        IdNotificacion = n.IdNotificacion,
                        CodigoEmpresa = n.CodigoEmpresa,
                        TipoNotificacion = n.TipoNotificacion,
                        ProcesoId = n.ProcesoId,
                        Titulo = n.Titulo,
                        Mensaje = n.Mensaje,
                        EstadoAnterior = n.EstadoAnterior,
                        EstadoActual = n.EstadoActual,
                        FechaCreacion = n.FechaCreacion,
                        EsActiva = n.EsActiva,
                        EsGrupal = n.EsGrupal,
                        GrupoDestino = n.GrupoDestino,
                        Comentario = n.Comentario,
                        Destinatarios = n.Destinatarios
                            .Where(d => d.EsActiva)
                            .Select(d => new NotificacionDestinatarioDto
                            {
                                IdDestinatario = d.IdDestinatario,
                                IdNotificacion = d.IdNotificacion,
                                UsuarioId = d.UsuarioId,
                                FechaCreacion = d.FechaCreacion,
                                EsActiva = d.EsActiva,
                                Leida = n.Lecturas.Any(l => l.UsuarioId == d.UsuarioId),
                                FechaLeida = n.Lecturas
                                    .Where(l => l.UsuarioId == d.UsuarioId)
                                    .Select(l => l.FechaLeida)
                                    .FirstOrDefault()
                            }).ToList()
                    })
                    .ToListAsync();

                return notificaciones;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener notificaciones por proceso: {TipoNotificacion} - {ProcesoId}", tipoNotificacion, procesoId);
                throw;
            }
        }

        /// <summary>
        /// Crea una notificación de traspaso con destinatarios automáticos
        /// </summary>
        public async Task<Notificacion> CrearNotificacionTraspasoAsync(Guid traspasoId, string titulo, string mensaje, string estadoAnterior, string estadoActual, int usuarioDestinatario)
        {
            var crearDto = new CrearNotificacionDto
            {
                CodigoEmpresa = 1,
                TipoNotificacion = "TRASPASO",
                ProcesoId = traspasoId,
                Titulo = titulo,
                Mensaje = mensaje,
                EstadoAnterior = estadoAnterior,
                EstadoActual = estadoActual,
                EsGrupal = false,
                UsuarioIds = new List<int> { usuarioDestinatario }
            };

            return await CrearNotificacionAsync(crearDto);
        }

        /// <summary>
        /// Crea una notificación grupal
        /// </summary>
        public async Task<Notificacion> CrearNotificacionGrupalAsync(string tipoNotificacion, string titulo, string mensaje, string grupoDestino, List<int> usuarioIds)
        {
            var crearDto = new CrearNotificacionDto
            {
                CodigoEmpresa = 1,
                TipoNotificacion = tipoNotificacion,
                Titulo = titulo,
                Mensaje = mensaje,
                EsGrupal = true,
                GrupoDestino = grupoDestino,
                UsuarioIds = usuarioIds
            };

            return await CrearNotificacionAsync(crearDto);
        }
    }
}
