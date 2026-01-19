using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGA_Api.Models.OrdenTraspaso;

namespace SGA_Api.Services
{
    /// <summary>
    /// Servicio para gestionar notificaciones específicas de órdenes de traspaso
    /// </summary>
    public class NotificacionesOrdenTraspasoService : INotificacionesOrdenTraspasoService
    {
        private readonly INotificacionesUnificadasService _notificacionesUnificadas;
        private readonly AuroraSgaDbContext _context;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<NotificacionesOrdenTraspasoService> _logger;

        public NotificacionesOrdenTraspasoService(
            INotificacionesUnificadasService notificacionesUnificadas,
            AuroraSgaDbContext context,
            SageDbContext sageContext,
            ILogger<NotificacionesOrdenTraspasoService> logger)
        {
            _notificacionesUnificadas = notificacionesUnificadas;
            _context = context;
            _sageContext = sageContext;
            _logger = logger;
        }

        /// <summary>
        /// Notifica cuando se crea una nueva orden de traspaso
        /// </summary>
        public async Task NotificarOrdenCreadaAsync(Guid ordenId, int usuarioCreacion, short codigoEmpresa, string codigoOrden, string? codigoAlmacenDestino = null)
        {
            try
            {
                // Obtener información de la orden y sus líneas
                var orden = await _context.OrdenTraspasoCabecera
                    .Include(o => o.Lineas)
                    .Where(o => o.IdOrdenTraspaso == ordenId)
                    .FirstOrDefaultAsync();

                if (orden == null)
                {
                    _logger.LogWarning("No se encontró la orden {OrdenId} para notificar creación", ordenId);
                    return;
                }

                // Obtener nombre del creador
                var nombreCreador = await ObtenerNombreUsuarioAsync(usuarioCreacion);
                
                // Obtener almacenes únicos (origen y destino)
                var almacenesOrigen = orden.Lineas
                    .Where(l => !string.IsNullOrEmpty(l.CodigoAlmacenOrigen))
                    .Select(l => l.CodigoAlmacenOrigen)
                    .Distinct()
                    .ToList();
                
                var almacenesDestino = orden.Lineas
                    .Where(l => !string.IsNullOrEmpty(l.CodigoAlmacenDestino))
                    .Select(l => l.CodigoAlmacenDestino)
                    .Distinct()
                    .ToList();

                if (!string.IsNullOrEmpty(codigoAlmacenDestino) && !almacenesDestino.Contains(codigoAlmacenDestino))
                {
                    almacenesDestino.Add(codigoAlmacenDestino);
                }

                var todosAlmacenes = almacenesOrigen.Union(almacenesDestino).Distinct().ToList();

                var titulo = "Nueva Orden de Traspaso Creada";
                var mensaje = $"Se ha creado una nueva orden de traspaso: {codigoOrden}\n" +
                            $"Creada por: {nombreCreador}\n" +
                            $"Total de líneas: {orden.Lineas.Count}";

                if (todosAlmacenes.Any())
                {
                    mensaje += $"\nAlmacenes: {string.Join(", ", todosAlmacenes)}";
                }

                // 1. Notificar al creador (siempre)
                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                    usuarioCreacion,
                    "ORDEN_TRASPASO_CREADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    orden.Estado,
                    "info");

                // 2. Notificar a supervisores de los almacenes involucrados (origen y destino)
                if (todosAlmacenes.Any())
                {
                    var supervisoresIds = await ObtenerSupervisoresConAccesoAlmacenesAsync(todosAlmacenes, codigoEmpresa, usuarioCreacion);
                    
                    if (supervisoresIds.Any())
                    {
                        foreach (var supervisorId in supervisoresIds)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                supervisorId,
                                "ORDEN_TRASPASO_CREADA",
                                titulo,
                                mensaje,
                                ordenId,
                                null,
                                orden.Estado,
                                "info");
                        }
                    }
                }

                // 3. Notificar a todos los administradores (excluyendo al creador si es admin)
                var administradoresIds = await _context.Usuarios
                    .Where(u => u.IdRol == 3 && u.IdUsuario != usuarioCreacion)
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                if (administradoresIds.Any())
                {
                    foreach (var adminId in administradoresIds)
                    {
                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                            adminId,
                            "ORDEN_TRASPASO_CREADA",
                            titulo,
                            mensaje,
                            ordenId,
                            null,
                            orden.Estado,
                            "info");
                    }
                }

                _logger.LogInformation("Notificación de orden creada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden creada para orden {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se completa una orden de traspaso
        /// </summary>
        public async Task NotificarOrdenCompletadaAsync(Guid ordenId, int usuarioCreacion, short codigoEmpresa, string codigoOrden)
        {
            try
            {
                // Obtener información de la orden y sus líneas
                var orden = await _context.OrdenTraspasoCabecera
                    .Include(o => o.Lineas)
                    .Where(o => o.IdOrdenTraspaso == ordenId)
                    .FirstOrDefaultAsync();

                if (orden == null)
                {
                    _logger.LogWarning("No se encontró la orden {OrdenId} para notificar finalización", ordenId);
                    return;
                }

                // Obtener nombre del creador
                var nombreCreador = await ObtenerNombreUsuarioAsync(usuarioCreacion);
                
                // Obtener almacenes únicos (origen y destino)
                var almacenesOrigen = orden.Lineas
                    .Where(l => !string.IsNullOrEmpty(l.CodigoAlmacenOrigen))
                    .Select(l => l.CodigoAlmacenOrigen)
                    .Distinct()
                    .ToList();
                
                var almacenesDestino = orden.Lineas
                    .Where(l => !string.IsNullOrEmpty(l.CodigoAlmacenDestino))
                    .Select(l => l.CodigoAlmacenDestino)
                    .Distinct()
                    .ToList();

                var todosAlmacenes = almacenesOrigen.Union(almacenesDestino).Distinct().ToList();

                var titulo = "Orden de Traspaso Completada";
                var mensaje = $"La orden de traspaso {codigoOrden} ha sido completada\n" +
                            $"Creada por: {nombreCreador}\n" +
                            $"Total de líneas: {orden.Lineas.Count}";

                if (todosAlmacenes.Any())
                {
                    mensaje += $"\nAlmacenes: {string.Join(", ", todosAlmacenes)}";
                }

                // 1. Notificar a supervisores de los almacenes involucrados (origen y destino)
                if (todosAlmacenes.Any())
                {
                    var supervisoresIds = await ObtenerSupervisoresConAccesoAlmacenesAsync(todosAlmacenes, codigoEmpresa, null);
                    
                    if (supervisoresIds.Any())
                    {
                        foreach (var supervisorId in supervisoresIds)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                supervisorId,
                                "ORDEN_TRASPASO_COMPLETADA",
                                titulo,
                                mensaje,
                                ordenId,
                                null,
                                orden.Estado,
                                "success");
                        }
                    }
                }

                // 2. Notificar a todos los administradores
                var administradoresIds = await _context.Usuarios
                    .Where(u => u.IdRol == 3)
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                if (administradoresIds.Any())
                {
                    foreach (var adminId in administradoresIds)
                    {
                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                            adminId,
                            "ORDEN_TRASPASO_COMPLETADA",
                            titulo,
                            mensaje,
                            ordenId,
                            null,
                            orden.Estado,
                            "success");
                    }
                }

                _logger.LogInformation("Notificación de orden completada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden completada para orden {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se asigna un operario a una línea de orden de traspaso
        /// </summary>
        public async Task NotificarLineaAsignadaAsync(Guid lineaId, Guid ordenId, int operarioAsignado, string codigoArticulo, string codigoAlmacenOrigen, short codigoEmpresa)
        {
            try
            {
                // Obtener información de la línea y orden
                var linea = await _context.OrdenTraspasoLinea
                    .Include(l => l.OrdenTraspaso)
                    .Where(l => l.IdLineaOrdenTraspaso == lineaId)
                    .FirstOrDefaultAsync();

                if (linea == null)
                {
                    _logger.LogWarning("No se encontró la línea {LineaId} para notificar asignación", lineaId);
                    return;
                }

                // Obtener nombres
                var nombreOperario = await ObtenerNombreUsuarioAsync(operarioAsignado);

                var titulo = "Línea de Orden Asignada";
                var mensaje = $"Línea del artículo {codigoArticulo} asignada al operario {nombreOperario}\n" +
                            $"Orden: {linea.OrdenTraspaso.CodigoOrden}\n" +
                            $"Almacén origen: {codigoAlmacenOrigen}";

                // 1. Notificar al operario asignado (si es operario, no supervisor ni admin)
                var operario = await _context.Usuarios
                    .Where(u => u.IdUsuario == operarioAsignado)
                    .Select(u => new { u.IdRol })
                    .FirstOrDefaultAsync();

                if (operario != null && operario.IdRol == 1)
                {
                    await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                        operarioAsignado,
                        "LINEA_ORDEN_ASIGNADA",
                        titulo,
                        mensaje,
                        ordenId,
                        null,
                        linea.Estado,
                        "info");
                }

                // 2. Notificar a supervisores del almacén origen
                if (!string.IsNullOrEmpty(codigoAlmacenOrigen))
                {
                    var supervisoresIds = await ObtenerSupervisoresConAccesoAlmacenesAsync(
                        new List<string> { codigoAlmacenOrigen }, 
                        codigoEmpresa, 
                        operarioAsignado);

                    if (supervisoresIds.Any())
                    {
                        foreach (var supervisorId in supervisoresIds)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                supervisorId,
                                "LINEA_ORDEN_ASIGNADA",
                                titulo,
                                mensaje,
                                ordenId,
                                null,
                                linea.Estado,
                                "info");
                        }
                    }
                }

                // 3. Notificar a todos los administradores (excluyendo al operario si es admin)
                var administradoresIds = await _context.Usuarios
                    .Where(u => u.IdRol == 3 && u.IdUsuario != operarioAsignado)
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                if (administradoresIds.Any())
                {
                    foreach (var adminId in administradoresIds)
                    {
                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                            adminId,
                            "LINEA_ORDEN_ASIGNADA",
                            titulo,
                            mensaje,
                            ordenId,
                            null,
                            linea.Estado,
                            "info");
                    }
                }

                _logger.LogInformation("Notificación de línea asignada enviada para línea {LineaId}", lineaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de línea asignada para línea {LineaId}", lineaId);
            }
        }

        /// <summary>
        /// Obtiene el nombre de un usuario desde vUsuariosConNombre
        /// </summary>
        private async Task<string> ObtenerNombreUsuarioAsync(int usuarioId)
        {
            try
            {
                var usuario = await _context.vUsuariosConNombre
                    .Where(u => u.UsuarioId == usuarioId)
                    .Select(u => u.NombreOperario)
                    .FirstOrDefaultAsync();

                return usuario ?? usuarioId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener nombre del usuario {UsuarioId}, usando ID", usuarioId);
                return usuarioId.ToString();
            }
        }

        /// <summary>
        /// Obtiene supervisores con acceso a los almacenes especificados
        /// </summary>
        private async Task<List<int>> ObtenerSupervisoresConAccesoAlmacenesAsync(List<string> codigosAlmacen, short codigoEmpresa, int? usuarioExcluir = null)
        {
            var supervisoresIds = new List<int>();

            try
            {
                if (codigosAlmacen == null || !codigosAlmacen.Any())
                    return supervisoresIds;

                // Iterar por cada almacén para evitar problemas con OPENJSON en EF Core
                foreach (var codigoAlmacen in codigosAlmacen.Distinct())
                {
                    if (string.IsNullOrEmpty(codigoAlmacen)) continue;

                    // Obtener IDs de operarios con acceso al almacén
                    var operariosConAcceso = await _sageContext.OperariosAlmacenes
                        .Where(oa => oa.CodigoAlmacen == codigoAlmacen && oa.CodigoEmpresa == codigoEmpresa)
                        .Select(oa => oa.Operario)
                        .Distinct()
                        .ToListAsync();

                    if (operariosConAcceso.Any())
                    {
                        // Obtener supervisores (IdRol == 2) con acceso al almacén
                        var supervisores = await _context.Usuarios
                            .Where(u => u.IdRol == 2 
                                && operariosConAcceso.Contains(u.IdUsuario)
                                && (usuarioExcluir == null || u.IdUsuario != usuarioExcluir))
                            .Select(u => u.IdUsuario)
                            .ToListAsync();

                        supervisoresIds.AddRange(supervisores);
                    }
                }

                // Eliminar duplicados
                return supervisoresIds.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener supervisores con acceso a almacenes {Almacenes}", string.Join(", ", codigosAlmacen));
                return new List<int>();
            }
        }
    }
}
