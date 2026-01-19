using SGA_Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGA_Api.Models.Notificaciones;
using SGA_Api.Models.UsuarioConf;

namespace SGA_Api.Services
{
    /// <summary>
    /// Clase para almacenar nombres de usuarios
    /// </summary>
    public class NombresUsuarios
    {
        public string CreadoPor { get; set; } = string.Empty;
        public string Supervisor { get; set; } = string.Empty;
        public string Operario { get; set; } = string.Empty;
    }

    /// <summary>
    /// Servicio para gestionar notificaciones específicas de conteos
    /// </summary>
    public class NotificacionesConteosService : INotificacionesConteosService
    {
        private readonly INotificacionesUnificadasService _notificacionesUnificadas;
        private readonly AuroraSgaDbContext _context;
        private readonly SageDbContext _sageContext;
        private readonly ILogger<NotificacionesConteosService> _logger;

        public NotificacionesConteosService(
            INotificacionesUnificadasService notificacionesUnificadas,
            AuroraSgaDbContext context,
            SageDbContext sageContext,
            ILogger<NotificacionesConteosService> logger)
        {
            _notificacionesUnificadas = notificacionesUnificadas;
            _context = context;
            _sageContext = sageContext;
            _logger = logger;
        }

        /// <summary>
        /// Notifica cuando se crea una nueva orden de conteo
        /// </summary>
        public async Task NotificarOrdenCreadaAsync(Guid ordenId, string titulo, string creadoPorCodigo, string? supervisorCodigo = null, 
            string? codigoAlmacen = null, string? alcance = null, string? codigoOperario = null, string? codigoUbicacion = null, string? codigoArticulo = null, byte prioridad = 3)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync(creadoPorCodigo, supervisorCodigo, codigoOperario);
                
                // Mensaje enriquecido con información detallada
                var mensaje = $"Se ha creado una nueva orden de conteo: \"{titulo}\"\n" +
                            $"Creada por: {nombresUsuarios.CreadoPor}";
                
            // Información del almacén
            if (!string.IsNullOrEmpty(codigoAlmacen))
            {
                mensaje += $"\nAlmacén: {codigoAlmacen}";
            }
                
                // Tipo de conteo
                if (!string.IsNullOrEmpty(alcance))
                {
                    var tipoConteo = alcance switch
                    {
                        "ALMACEN" => "Conteo por Almacén",
                        "UBICACION" => "Conteo por Ubicación",
                        "ARTICULO" => "Conteo por Artículo",
                        "MULTIARTICULO" => "Conteo por Múltiples Artículos",
                        "PASILLO" => "Conteo por Pasillo",
                        "ZONA" => "Conteo por Zona",
                        _ => $"Conteo por {alcance}"
                    };
                    mensaje += $"\nTipo: {tipoConteo}";
                }
                
            // Información específica según el tipo de conteo
            if (!string.IsNullOrEmpty(alcance))
            {
                switch (alcance.ToUpper())
                {
                    case "PASILLO":
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nPasillo: {codigoUbicacion}";
                        }
                        break;
                    case "UBICACION":
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nUbicación: {codigoUbicacion}";
                        }
                        break;
                    case "ARTICULO":
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            mensaje += $"\nArtículo: {codigoArticulo}";
                        }
                        break;
                    case "MULTIARTICULO":
                        // Obtener FiltrosJson de la orden para extraer múltiples artículos
                        var orden = await _context.OrdenesConteo
                            .FirstOrDefaultAsync(o => o.GuidID == ordenId);
                        if (orden != null && !string.IsNullOrEmpty(orden.FiltrosJson))
                        {
                            var codigosArticulos = ExtraerArticulosDelFiltro(orden.FiltrosJson);
                            if (codigosArticulos != null && codigosArticulos.Any())
                            {
                                mensaje += $"\nArtículos ({codigosArticulos.Count}): {string.Join(", ", codigosArticulos)}";
                            }
                        }
                        else if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            mensaje += $"\nArtículo: {codigoArticulo}";
                        }
                        break;
                    case "ZONA":
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nZona: {codigoUbicacion}";
                        }
                        break;
                    default:
                        // Para otros tipos, mostrar ubicación si está disponible
                        if (!string.IsNullOrEmpty(codigoUbicacion))
                        {
                            mensaje += $"\nUbicación: {codigoUbicacion}";
                        }
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            mensaje += $"\nArtículo: {codigoArticulo}";
                        }
                        break;
                }
            }
                
                // Operario asignado
                if (!string.IsNullOrEmpty(codigoOperario))
                {
                    mensaje += $"\nAsignado a: {nombresUsuarios.Operario}";
                }
                
            // Supervisor
            if (!string.IsNullOrEmpty(supervisorCodigo))
            {
                mensaje += $"\nSupervisor: {nombresUsuarios.Supervisor}";
            }
            
            // Prioridad
            var nivelPrioridad = prioridad switch
            {
                1 => "Muy Baja",
                2 => "Baja", 
                3 => "Normal",
                4 => "Alta",
                5 => "Crítica",
                _ => "Normal"
            };
            mensaje += $"\nPrioridad: {nivelPrioridad} ({prioridad}/5)";

                // 1. Notificar al operario asignado (si existe y NO es supervisor ni admin)
                int? operarioAsignadoId = null;
                if (!string.IsNullOrEmpty(codigoOperario) && int.TryParse(codigoOperario, out int operarioId))
                {
                    operarioAsignadoId = operarioId;
                    try
                    {
                        // Verificar el rol del operario asignado
                        var operarioAsignado = await _context.Usuarios
                            .Where(u => u.IdUsuario == operarioId)
                            .Select(u => new { u.IdRol })
                            .FirstOrDefaultAsync();

                        if (operarioAsignado != null)
                        {
                            // Solo notificar como operario asignado si es operario puro (IdRol == 1)
                            // Si es supervisor o admin, se le notificará más adelante con su rol correspondiente
                            if (operarioAsignado.IdRol == 1)
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    operarioId,
                                    "ORDEN_CREADA",
                                    "Nueva Orden de Conteo",
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogInformation("Notificación enviada al operario asignado {OperarioId}", operarioId);
                            }
                            else
                            {
                                _logger.LogDebug("Operario asignado {OperarioId} es {Rol}, se notificará con su rol correspondiente", 
                                    operarioId, operarioAsignado.IdRol == 2 ? "supervisor" : "administrador");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar al operario asignado {OperarioId}", operarioId);
                    }
                }

                // 2. Notificar a supervisores que tengan permiso para el almacén
                if (!string.IsNullOrEmpty(codigoAlmacen))
                {
                    try
                    {
                        // Primero obtener el CodigoEmpresa de la orden
                        var orden = await _context.OrdenesConteo
                            .Where(o => o.GuidID == ordenId)
                            .Select(o => new { o.CodigoEmpresa })
                            .FirstOrDefaultAsync();

                        if (orden != null)
                        {
                            // Obtener IDs de operarios con acceso al almacén desde OperariosAlmacenes
                            // Filtrar por CodigoEmpresa Y CodigoAlmacen
                            var operariosConAcceso = await _sageContext.OperariosAlmacenes
                                .Where(oa => oa.CodigoAlmacen == codigoAlmacen && 
                                             oa.CodigoEmpresa == orden.CodigoEmpresa)
                                .Select(oa => oa.Operario)
                                .Distinct()
                                .ToListAsync();

                            if (operariosConAcceso.Any())
                            {
                                // Filtrar solo supervisores (IdRol = 2) y excluir al operario asignado si existe
                                var supervisoresIds = await _context.Usuarios
                                    .Where(u => u.IdRol == 2 && 
                                                operariosConAcceso.Contains(u.IdUsuario) &&
                                                (operarioAsignadoId == null || u.IdUsuario != operarioAsignadoId))
                                    .Select(u => u.IdUsuario)
                                    .ToListAsync();

                                if (supervisoresIds.Any())
                                {
                                    _logger.LogInformation("Notificando a {Cantidad} supervisores con acceso al almacén {Almacen} de empresa {Empresa}", 
                                        supervisoresIds.Count, codigoAlmacen, orden.CodigoEmpresa);

                                    // Notificar a cada supervisor
                                    foreach (var supervisorId in supervisoresIds)
                                    {
                                        try
                                        {
                                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                                supervisorId,
                                                "ORDEN_CREADA",
                                                "Nueva Orden de Conteo",
                                                mensaje,
                                                ordenId,
                                                null,
                                                null,
                                                "info");
                                            
                                            _logger.LogDebug("Notificación enviada a supervisor {SupervisorId} para orden {OrdenId}", supervisorId, ordenId);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} para orden {OrdenId}", supervisorId, ordenId);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.LogDebug("No se encontraron supervisores con acceso al almacén {Almacen} de empresa {Empresa}", codigoAlmacen, orden.CodigoEmpresa);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("No se encontraron operarios con acceso al almacén {Almacen} de empresa {Empresa}", codigoAlmacen, orden.CodigoEmpresa);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No se encontró la orden {OrdenId} para obtener CodigoEmpresa", ordenId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al buscar supervisores para almacén {Almacen}", codigoAlmacen);
                    }
                }

                // 3. Notificar a todos los administradores (IdRol = 3)
                try
                {
                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3)
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    _logger.LogInformation("Encontrados {Cantidad} administradores en la base de datos", administradoresIds.Count);

                    if (administradoresIds.Any())
                    {
                        _logger.LogInformation("Notificando a {Cantidad} administradores", administradoresIds.Count);

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "ORDEN_CREADA",
                                    "Nueva Orden de Conteo",
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogDebug("Notificación enviada a administrador {AdminId} para orden {OrdenId}", adminId, ordenId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} para orden {OrdenId}", adminId, ordenId);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No se encontraron administradores (IdRol = 3) en la base de datos");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al buscar administradores para orden {OrdenId}", ordenId);
                }

                _logger.LogInformation("Notificación de orden creada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden creada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se asigna un operario a una orden de conteo
        /// </summary>
        public async Task NotificarOperarioAsignadoAsync(Guid ordenId, string codigoOperario, string? supervisorCodigo = null, string? codigoAlmacen = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync("", supervisorCodigo, codigoOperario);
                
                var titulo = "Operario Asignado";
                var mensaje = $"El operario {nombresUsuarios.Operario} ha sido asignado a una orden de conteo";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $"\nSupervisor responsable: {nombresUsuarios.Supervisor}";
                }

                // 1. Notificar al operario asignado (si existe y NO es supervisor ni admin)
                int? operarioAsignadoId = null;
                if (!string.IsNullOrEmpty(codigoOperario) && int.TryParse(codigoOperario, out int operarioId))
                {
                    operarioAsignadoId = operarioId;
                    try
                    {
                        // Verificar el rol del operario asignado
                        var operarioAsignado = await _context.Usuarios
                            .Where(u => u.IdUsuario == operarioId)
                            .Select(u => new { u.IdRol })
                            .FirstOrDefaultAsync();

                        if (operarioAsignado != null)
                        {
                            // Solo notificar como operario asignado si es operario puro (IdRol == 1)
                            // Si es supervisor o admin, se le notificará más adelante con su rol correspondiente
                            if (operarioAsignado.IdRol == 1)
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    operarioId,
                                    "OPERARIO_ASIGNADO",
                                    titulo,
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogInformation("Notificación enviada al operario asignado {OperarioId}", operarioId);
                            }
                            else
                            {
                                _logger.LogDebug("Operario asignado {OperarioId} es {Rol}, se notificará con su rol correspondiente", 
                                    operarioId, operarioAsignado.IdRol == 2 ? "supervisor" : "administrador");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar al operario asignado {OperarioId}", operarioId);
                    }
                }

                // 2. Notificar a supervisores que tengan permiso para el almacén
                if (!string.IsNullOrEmpty(codigoAlmacen))
                {
                    try
                    {
                        // Obtener IDs de operarios con acceso al almacén desde OperariosAlmacenes
                        var operariosConAcceso = await _sageContext.OperariosAlmacenes
                            .Where(oa => oa.CodigoAlmacen == codigoAlmacen)
                            .Select(oa => oa.Operario)
                            .Distinct()
                            .ToListAsync();

                        if (operariosConAcceso.Any())
                        {
                            // Filtrar solo supervisores (IdRol = 2) y excluir al operario asignado si existe
                            var supervisoresIds = await _context.Usuarios
                                .Where(u => u.IdRol == 2 && 
                                            operariosConAcceso.Contains(u.IdUsuario) &&
                                            (operarioAsignadoId == null || u.IdUsuario != operarioAsignadoId))
                                .Select(u => u.IdUsuario)
                                .ToListAsync();

                            if (supervisoresIds.Any())
                            {
                                _logger.LogInformation("Notificando a {Cantidad} supervisores con acceso al almacén {Almacen} sobre operario asignado", 
                                    supervisoresIds.Count, codigoAlmacen);

                                // Notificar a cada supervisor
                                foreach (var supervisorId in supervisoresIds)
                                {
                                    try
                                    {
                                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                            supervisorId,
                                            "OPERARIO_ASIGNADO",
                                            titulo,
                                            mensaje,
                                            ordenId,
                                            null,
                                            null,
                                            "info");
                                        
                                        _logger.LogDebug("Notificación enviada a supervisor {SupervisorId} para orden {OrdenId}", supervisorId, ordenId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} para orden {OrdenId}", supervisorId, ordenId);
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogDebug("No se encontraron supervisores con acceso al almacén {Almacen} para operario asignado", codigoAlmacen);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("No se encontraron operarios con acceso al almacén {Almacen} para operario asignado", codigoAlmacen);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al buscar supervisores para almacén {Almacen} en operario asignado", codigoAlmacen);
                    }
                }

                // 3. Notificar a todos los administradores (IdRol = 3), excluyendo al operario asignado si existe
                try
                {
                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3 && (operarioAsignadoId == null || u.IdUsuario != operarioAsignadoId))
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    if (administradoresIds.Any())
                    {
                        _logger.LogInformation("Notificando a {Cantidad} administradores sobre operario asignado", administradoresIds.Count);

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "OPERARIO_ASIGNADO",
                                    titulo,
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogDebug("Notificación enviada a administrador {AdminId} para orden {OrdenId}", adminId, ordenId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} para orden {OrdenId}", adminId, ordenId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al buscar administradores para orden {OrdenId}", ordenId);
                }

                _logger.LogInformation("Notificación de operario asignado enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de operario asignado para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se inicia una orden de conteo
        /// </summary>
        public async Task NotificarOrdenIniciadaAsync(Guid ordenId, string codigoOperario, string? supervisorCodigo = null, string? codigoAlmacen = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync("", supervisorCodigo, codigoOperario);
                
                var titulo = "Conteo Iniciado";
                var mensaje = $"El operario {nombresUsuarios.Operario} ha comenzado a realizar el conteo";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $"\nSupervisor responsable: {nombresUsuarios.Supervisor}";
                }

                // 1. Notificar a supervisores que tengan permiso para el almacén
                if (!string.IsNullOrEmpty(codigoAlmacen))
                {
                    try
                    {
                        // Obtener IDs de operarios con acceso al almacén desde OperariosAlmacenes
                        var operariosConAcceso = await _sageContext.OperariosAlmacenes
                            .Where(oa => oa.CodigoAlmacen == codigoAlmacen)
                            .Select(oa => oa.Operario)
                            .Distinct()
                            .ToListAsync();

                        if (operariosConAcceso.Any())
                        {
                            // Filtrar solo supervisores (IdRol = 2)
                            var supervisoresIds = await _context.Usuarios
                                .Where(u => u.IdRol == 2 && operariosConAcceso.Contains(u.IdUsuario))
                                .Select(u => u.IdUsuario)
                                .ToListAsync();

                            if (supervisoresIds.Any())
                            {
                                _logger.LogInformation("Notificando a {Cantidad} supervisores con acceso al almacén {Almacen} sobre orden iniciada", 
                                    supervisoresIds.Count, codigoAlmacen);

                                // Notificar a cada supervisor
                                foreach (var supervisorId in supervisoresIds)
                                {
                                    try
                                    {
                                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                            supervisorId,
                                            "ORDEN_INICIADA",
                                            titulo,
                                            mensaje,
                                            ordenId,
                                            null,
                                            null,
                                            "info");
                                        
                                        _logger.LogDebug("Notificación enviada a supervisor {SupervisorId} para orden iniciada {OrdenId}", supervisorId, ordenId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} para orden iniciada {OrdenId}", supervisorId, ordenId);
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogDebug("No se encontraron supervisores con acceso al almacén {Almacen} para orden iniciada", codigoAlmacen);
                            }
                        }
                        else
                        {
                            _logger.LogDebug("No se encontraron operarios con acceso al almacén {Almacen} para orden iniciada", codigoAlmacen);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al buscar supervisores para almacén {Almacen} en orden iniciada", codigoAlmacen);
                    }
                }

                // 2. Notificar a todos los administradores (IdRol = 3), excluyendo a los que ya son supervisores
                try
                {
                    // Primero obtener los supervisores que se notificaron para excluirlos de admins
                    var supervisoresNotificados = new List<int>();
                    if (!string.IsNullOrEmpty(codigoAlmacen))
                    {
                        try
                        {
                            var operariosConAcceso = await _sageContext.OperariosAlmacenes
                                .Where(oa => oa.CodigoAlmacen == codigoAlmacen)
                                .Select(oa => oa.Operario)
                                .Distinct()
                                .ToListAsync();

                            if (operariosConAcceso.Any())
                            {
                                supervisoresNotificados = await _context.Usuarios
                                    .Where(u => u.IdRol == 2 && operariosConAcceso.Contains(u.IdUsuario))
                                    .Select(u => u.IdUsuario)
                                    .ToListAsync();
                            }
                        }
                        catch
                        {
                            // Si hay error obteniendo supervisores, continuar sin excluirlos
                        }
                    }

                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3 && !supervisoresNotificados.Contains(u.IdUsuario))
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    if (administradoresIds.Any())
                    {
                        _logger.LogInformation("Notificando a {Cantidad} administradores sobre orden iniciada", administradoresIds.Count);

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "ORDEN_INICIADA",
                                    titulo,
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogDebug("Notificación enviada a administrador {AdminId} para orden iniciada {OrdenId}", adminId, ordenId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} para orden iniciada {OrdenId}", adminId, ordenId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al buscar administradores para orden iniciada {OrdenId}", ordenId);
                }

                _logger.LogInformation("Notificación de orden iniciada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden iniciada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se completa una orden de conteo
        /// </summary>
        public async Task NotificarOrdenCompletadaAsync(Guid ordenId, string codigoOperario, int totalLecturas, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Conteo Completado";
                var mensaje = $"Operario {codigoOperario} ha completado la orden de conteo {ordenId} con {totalLecturas} lecturas";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "ORDEN_COMPLETADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "success");

                _logger.LogInformation("Notificación de orden completada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden completada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se cierra una orden de conteo
        /// </summary>
        public async Task NotificarOrdenCerradaAsync(Guid ordenId, string? creadoPorCodigo = null, int? totalResultados = null)
        {
            try
            {
                // Obtener la orden para obtener el título (más amigable que el GUID)
                var orden = await _context.OrdenesConteo
                    .Where(o => o.GuidID == ordenId)
                    .Select(o => new { o.Titulo })
                    .FirstOrDefaultAsync();

                var titulo = "Conteo Cerrado";
                var mensaje = $"La orden de conteo \"{orden?.Titulo ?? "N/A"}\" ha sido cerrada";
                
                if (totalResultados.HasValue)
                {
                    mensaje += $" con {totalResultados} resultados generados";
                }

                // 1. Notificar al supervisor que creó la orden (si es supervisor)
                int? creadorIdExcluido = null;
                if (!string.IsNullOrEmpty(creadoPorCodigo) && int.TryParse(creadoPorCodigo, out int creadorId))
                {
                    creadorIdExcluido = creadorId;
                    try
                    {
                        // Verificar si el creador es supervisor (IdRol = 2)
                        var creadorEsSupervisor = await _context.Usuarios
                            .Where(u => u.IdUsuario == creadorId && u.IdRol == 2)
                            .AnyAsync();

                        if (creadorEsSupervisor)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                creadorId,
                                "ORDEN_CERRADA",
                                titulo,
                                mensaje,
                                ordenId,
                                null,
                                null,
                                "info");
                            
                            _logger.LogInformation("Notificación enviada al supervisor creador {CreadorId} para orden cerrada", creadorId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar al supervisor creador {CreadorId} para orden cerrada", creadorId);
                    }
                }

                // 2. Notificar a todos los administradores (IdRol = 3), excluyendo al supervisor creador si existe
                try
                {
                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3 && (creadorIdExcluido == null || u.IdUsuario != creadorIdExcluido))
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    if (administradoresIds.Any())
                    {
                        _logger.LogInformation("Notificando a {Cantidad} administradores sobre orden cerrada", administradoresIds.Count);

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "ORDEN_CERRADA",
                                    titulo,
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogDebug("Notificación enviada a administrador {AdminId} para orden cerrada {OrdenId}", adminId, ordenId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} para orden cerrada {OrdenId}", adminId, ordenId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al buscar administradores para orden cerrada {OrdenId}", ordenId);
                }

                _logger.LogInformation("Notificación de orden cerrada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden cerrada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se crea una nueva lectura de conteo
        /// </summary>
        public async Task NotificarLecturaCreadaAsync(Guid ordenId, string codigoOperario, string codigoArticulo, decimal cantidad, string? creadoPorCodigo = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync("", null, codigoOperario);
                
                var titulo = "Nueva Lectura de Conteo";
                var mensaje = $"Operario {nombresUsuarios.Operario} registró {cantidad} unidades del artículo {codigoArticulo}";

                // 1. Notificar al supervisor que creó la orden (si es supervisor)
                int? creadorIdExcluido = null;
                if (!string.IsNullOrEmpty(creadoPorCodigo) && int.TryParse(creadoPorCodigo, out int creadorId))
                {
                    creadorIdExcluido = creadorId;
                    try
                    {
                        // Verificar si el creador es supervisor (IdRol = 2)
                        var creadorEsSupervisor = await _context.Usuarios
                            .Where(u => u.IdUsuario == creadorId && u.IdRol == 2)
                            .AnyAsync();

                        if (creadorEsSupervisor)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                creadorId,
                                "LECTURA_CREADA",
                                titulo,
                                mensaje,
                                ordenId,
                                null,
                                null,
                                "info");
                            
                            _logger.LogInformation("Notificación enviada al supervisor creador {CreadorId} para lectura creada", creadorId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar al supervisor creador {CreadorId} para lectura creada", creadorId);
                    }
                }

                // 2. Notificar a todos los administradores (IdRol = 3), excluyendo al supervisor creador si existe
                try
                {
                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3 && (creadorIdExcluido == null || u.IdUsuario != creadorIdExcluido))
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    if (administradoresIds.Any())
                    {
                        _logger.LogInformation("Notificando a {Cantidad} administradores sobre lectura creada", administradoresIds.Count);

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "LECTURA_CREADA",
                                    titulo,
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "info");
                                
                                _logger.LogDebug("Notificación enviada a administrador {AdminId} para lectura creada {OrdenId}", adminId, ordenId);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} para lectura creada {OrdenId}", adminId, ordenId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al buscar administradores para lectura creada {OrdenId}", ordenId);
                }

                _logger.LogInformation("Notificación de lectura creada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de lectura creada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se reasigna una línea de conteo
        /// </summary>
        public async Task NotificarLineaReasignadaAsync(Guid ordenId, string codigoArticulo, string nuevoOperario, string? supervisorCodigo = null, string? codigoAlmacen = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync("", supervisorCodigo, nuevoOperario);
                
                // Obtener información de la orden (almacén y empresa)
                var orden = await _context.OrdenesConteo
                    .Where(o => o.GuidID == ordenId)
                    .Select(o => new { o.CodigoAlmacen, o.CodigoEmpresa, o.CodigoArticulo })
                    .FirstOrDefaultAsync();
                
                // Usar codigoArticulo del parámetro o de la orden si está disponible
                var articuloFinal = codigoArticulo != "N/A" ? codigoArticulo : (orden?.CodigoArticulo ?? "Artículo desconocido");
                var almacenFinal = codigoAlmacen ?? orden?.CodigoAlmacen;
                
                var titulo = "Línea de Conteo Reasignada";
                var mensaje = $"Línea del artículo {articuloFinal} reasignada al operario {nombresUsuarios.Operario}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $"\nSupervisor responsable: {nombresUsuarios.Supervisor}";
                }

                // 1. Notificar al nuevo operario asignado (si no es supervisor ni admin)
                int? operarioAsignadoId = null;
                if (!string.IsNullOrEmpty(nuevoOperario) && int.TryParse(nuevoOperario, out int operarioId))
                {
                    operarioAsignadoId = operarioId;
                    try
                    {
                        var operarioAsignado = await _context.Usuarios
                            .Where(u => u.IdUsuario == operarioId)
                            .Select(u => new { u.IdRol })
                            .FirstOrDefaultAsync();

                        if (operarioAsignado != null && operarioAsignado.IdRol == 1)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                operarioId,
                                "LINEA_REASIGNADA",
                                titulo,
                                mensaje,
                                ordenId,
                                null,
                                null,
                                "warning");
                            
                            _logger.LogInformation("Notificación enviada al operario asignado {OperarioId} para línea reasignada", operarioId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar al operario asignado {OperarioId}", operarioId);
                    }
                }

                // 2. Notificar a supervisores con acceso al almacén
                if (!string.IsNullOrEmpty(almacenFinal) && orden != null)
                {
                    try
                    {
                        // Obtener IDs de operarios con acceso al almacén desde OperariosAlmacenes
                        var operariosConAcceso = await _sageContext.OperariosAlmacenes
                            .Where(oa => oa.CodigoAlmacen == almacenFinal && 
                                        oa.CodigoEmpresa == orden.CodigoEmpresa)
                            .Select(oa => oa.Operario)
                            .Distinct()
                            .ToListAsync();

                        if (operariosConAcceso.Any())
                        {
                            // Filtrar solo supervisores (IdRol = 2) y excluir al operario asignado si existe
                            var supervisoresIds = await _context.Usuarios
                                .Where(u => u.IdRol == 2 && 
                                           operariosConAcceso.Contains(u.IdUsuario) &&
                                           (operarioAsignadoId == null || u.IdUsuario != operarioAsignadoId))
                                .Select(u => u.IdUsuario)
                                .ToListAsync();

                            if (supervisoresIds.Any())
                            {
                                _logger.LogInformation("Notificando a {Cantidad} supervisores con acceso al almacén {Almacen} para línea reasignada {OrdenId}", 
                                    supervisoresIds.Count, almacenFinal, ordenId);

                                foreach (var supervisorId in supervisoresIds)
                                {
                                    try
                                    {
                                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                            supervisorId,
                                            "LINEA_REASIGNADA",
                                            titulo,
                                            mensaje,
                                            ordenId,
                                            null,
                                            null,
                                            "warning");
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error al notificar supervisor {SupervisorId} para línea reasignada {OrdenId}", 
                                            supervisorId, ordenId);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar supervisores para línea reasignada {OrdenId}", ordenId);
                    }
                }

                // 3. Notificar a todos los administradores (IdRol == 3)
                try
                {
                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3 && (operarioAsignadoId == null || u.IdUsuario != operarioAsignadoId))
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    if (administradoresIds.Any())
                    {
                        _logger.LogInformation("Notificando a {Cantidad} administradores para línea reasignada {OrdenId}", 
                            administradoresIds.Count, ordenId);

                        foreach (var adminId in administradoresIds)
                        {
                            try
                            {
                                await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                    adminId,
                                    "LINEA_REASIGNADA",
                                    titulo,
                                    mensaje,
                                    ordenId,
                                    null,
                                    null,
                                    "warning");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al notificar administrador {AdminId} para línea reasignada {OrdenId}", 
                                    adminId, ordenId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al notificar administradores para línea reasignada {OrdenId}", ordenId);
                }

                _logger.LogInformation("Notificación de línea reasignada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de línea reasignada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando se actualiza un aprobador de resultado de conteo
        /// </summary>
        public async Task NotificarAprobadorActualizadoAsync(Guid resultadoId, string codigoAprobador, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Aprobador Actualizado";
                var mensaje = $"Aprobador {codigoAprobador} asignado al resultado de conteo";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "APROBADOR_ACTUALIZADO",
                    titulo,
                    mensaje,
                    resultadoId,
                    null,
                    null,
                    "info");

                _logger.LogInformation("Notificación de aprobador actualizado enviada para resultado {ResultadoId}", resultadoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de aprobador actualizado para {ResultadoId}", resultadoId);
            }
        }

        /// <summary>
        /// Notifica eventos críticos que requieren atención inmediata
        /// </summary>
        public async Task NotificarEventoCriticoAsync(string tipoEvento, string titulo, string mensaje, object? datosAdicionales = null)
        {
            try
            {
                var roles = new List<string> { "ADMIN" };
                
                // También notificar a supervisores si es relevante
                if (tipoEvento.Contains("CONTEOS") || tipoEvento.Contains("INVENTARIO"))
                {
                    roles.Add("SUPERVISOR");
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    roles.ToArray(),
                    tipoEvento,
                    titulo,
                    mensaje,
                    null,
                    null,
                    null,
                    roles.Contains("SUPERVISOR") ? "warning" : "error");

                _logger.LogInformation("Notificación de evento crítico enviada: {TipoEvento}", tipoEvento);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de evento crítico: {TipoEvento}", tipoEvento);
            }
        }

        /// <summary>
        /// Notifica cuando una orden de conteo se cancela
        /// </summary>
        public async Task NotificarOrdenCanceladaAsync(Guid ordenId, string motivo, string usuarioCodigo, string? supervisorCodigo = null)
        {
            try
            {
                var titulo = "Orden de Conteo Cancelada";
                var mensaje = $"Orden {ordenId} cancelada por {usuarioCodigo}. Motivo: {motivo}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $" - Supervisor: {supervisorCodigo}";
                }

                // Crear y enviar notificación unificada (BD + SignalR)
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "SUPERVISOR", "ADMIN" },
                    "ORDEN_CANCELADA",
                    titulo,
                    mensaje,
                    ordenId,
                    null,
                    null,
                    "warning");

                _logger.LogInformation("Notificación de orden cancelada enviada para orden {OrdenId}", ordenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de orden cancelada para {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Notifica cuando un conteo se envía a supervisión
        /// </summary>
        public async Task NotificarConteoSupervisionAsync(Guid resultadoGuid, string codigoArticulo, decimal cantidad, string operarioCodigo, string? supervisorCodigo = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync("", supervisorCodigo, operarioCodigo);
                
                // Obtener información del resultado y orden (almacén y empresa)
                var resultado = await _context.ResultadosConteo
                    .Where(r => r.GuidID == resultadoGuid)
                    .Select(r => new { r.CodigoAlmacen, r.OrdenGuid })
                    .FirstOrDefaultAsync();
                
                string? codigoAlmacen = null;
                int? codigoEmpresa = null;
                
                if (resultado != null)
                {
                    codigoAlmacen = resultado.CodigoAlmacen;
                    
                    // Obtener empresa de la orden
                    var orden = await _context.OrdenesConteo
                        .Where(o => o.GuidID == resultado.OrdenGuid)
                        .Select(o => new { o.CodigoEmpresa })
                        .FirstOrDefaultAsync();
                    
                    if (orden != null)
                    {
                        codigoEmpresa = orden.CodigoEmpresa;
                    }
                }

                var titulo = "Conteo Enviado a Supervisión";
                var mensaje = $"Conteo del artículo {codigoArticulo} (Cantidad: {cantidad}) enviado a supervisión por {nombresUsuarios.Operario}";
                
                if (!string.IsNullOrEmpty(supervisorCodigo))
                {
                    mensaje += $"\nSupervisor responsable: {nombresUsuarios.Supervisor}";
                }

                // 1. Notificar al operario (si no es supervisor ni admin)
                int? operarioId = null;
                if (!string.IsNullOrEmpty(operarioCodigo) && int.TryParse(operarioCodigo, out int opId))
                {
                    operarioId = opId;
                    var operario = await _context.Usuarios
                        .Where(u => u.IdUsuario == opId)
                        .Select(u => new { u.IdRol })
                        .FirstOrDefaultAsync();

                    if (operario != null && operario.IdRol == 1)
                    {
                        await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                            opId,
                            "CONTEO_SUPERVISION",
                            titulo,
                            mensaje,
                            resultadoGuid,
                            null,
                            null,
                            "warning");
                    }
                }

                // 2. Notificar a supervisores con acceso al almacén
                if (!string.IsNullOrEmpty(codigoAlmacen) && codigoEmpresa.HasValue)
                {
                    try
                    {
                        var operariosConAcceso = await _sageContext.OperariosAlmacenes
                            .Where(oa => oa.CodigoAlmacen == codigoAlmacen && 
                                        oa.CodigoEmpresa == codigoEmpresa.Value)
                            .Select(oa => oa.Operario)
                            .Distinct()
                            .ToListAsync();

                        if (operariosConAcceso.Any())
                        {
                            var supervisoresIds = await _context.Usuarios
                                .Where(u => u.IdRol == 2 && 
                                           operariosConAcceso.Contains(u.IdUsuario) &&
                                           (operarioId == null || u.IdUsuario != operarioId))
                                .Select(u => u.IdUsuario)
                                .ToListAsync();

                            if (supervisoresIds.Any())
                            {
                                foreach (var supervisorId in supervisoresIds)
                                {
                                    await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                        supervisorId,
                                        "CONTEO_SUPERVISION",
                                        titulo,
                                        mensaje,
                                        resultadoGuid,
                                        null,
                                        null,
                                        "warning");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al notificar supervisores para conteo en supervisión {ResultadoGuid}", resultadoGuid);
                    }
                }

                // 3. Notificar a todos los administradores
                try
                {
                    var administradoresIds = await _context.Usuarios
                        .Where(u => u.IdRol == 3 && (operarioId == null || u.IdUsuario != operarioId))
                        .Select(u => u.IdUsuario)
                        .ToListAsync();

                    if (administradoresIds.Any())
                    {
                        foreach (var adminId in administradoresIds)
                        {
                            await _notificacionesUnificadas.CrearYEnviarNotificacionUsuarioAsync(
                                adminId,
                                "CONTEO_SUPERVISION",
                                titulo,
                                mensaje,
                                resultadoGuid,
                                null,
                                null,
                                "warning");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al notificar administradores para conteo en supervisión {ResultadoGuid}", resultadoGuid);
                }

                _logger.LogInformation("Notificación de conteo en supervisión enviada para resultado {ResultadoGuid}", resultadoGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de conteo en supervisión para {ResultadoGuid}", resultadoGuid);
            }
        }

        /// <summary>
        /// Convierte IdRol a nombre de rol
        /// </summary>
        private string GetRolNombre(int? idRol)
        {
            return idRol switch
            {
                1 => "OPERARIO",
                2 => "SUPERVISOR",
                3 => "ADMIN",
                _ => "OPERARIO"
            };
        }

        /// <summary>
        /// Obtiene los nombres reales de los usuarios desde la vista vUsuariosConNombre
        /// </summary>
        private async Task<NombresUsuarios> ObtenerNombresUsuariosAsync(string creadoPorCodigo, string? supervisorCodigo, string? codigoOperario)
        {
            var nombres = new NombresUsuarios
            {
                CreadoPor = creadoPorCodigo, // Por defecto usar el código
                Supervisor = supervisorCodigo ?? "No asignado",
                Operario = codigoOperario ?? "No asignado"
            };

            try
            {
                // Obtener todos los códigos que necesitamos buscar
                var codigos = new List<string>();
                if (!string.IsNullOrEmpty(creadoPorCodigo)) codigos.Add(creadoPorCodigo);
                if (!string.IsNullOrEmpty(supervisorCodigo)) codigos.Add(supervisorCodigo);
                if (!string.IsNullOrEmpty(codigoOperario)) codigos.Add(codigoOperario);

                if (codigos.Any())
                {
                    // Buscar en la vista vUsuariosConNombre
                    var usuarios = await _context.vUsuariosConNombre
                        .Where(u => codigos.Contains(u.UsuarioId.ToString()))
                        .ToListAsync();

                    // Mapear los nombres encontrados
                    foreach (var usuario in usuarios)
                    {
                        var codigo = usuario.UsuarioId.ToString();
                        if (codigo == creadoPorCodigo)
                            nombres.CreadoPor = usuario.NombreOperario;
                        if (codigo == supervisorCodigo)
                            nombres.Supervisor = usuario.NombreOperario;
                        if (codigo == codigoOperario)
                            nombres.Operario = usuario.NombreOperario;
                    }
                }

                _logger.LogDebug("Nombres obtenidos - CreadoPor: {CreadoPor}, Supervisor: {Supervisor}, Operario: {Operario}", 
                    nombres.CreadoPor, nombres.Supervisor, nombres.Operario);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener nombres de usuarios, usando códigos por defecto");
            }

            return nombres;
        }

        /// <summary>
        /// Notifica cuando cambia el estado de una orden de conteo
        /// </summary>
        public async Task NotificarCambioEstadoAsync(Guid ordenId, string estadoAnterior, string estadoNuevo, string? codigoOperario = null, string? supervisorCodigo = null)
        {
            try
            {
                // Obtener nombres reales de usuarios
                var nombresUsuarios = await ObtenerNombresUsuariosAsync(codigoOperario ?? "", supervisorCodigo, null);
                
                // Determinar el tipo de notificación y mensaje según el cambio de estado
                var (titulo, mensaje, tipoVisual) = DeterminarNotificacionCambioEstado(estadoAnterior, estadoNuevo, nombresUsuarios);
                
                // Crear y enviar notificación unificada (BD + SignalR)
                var tipoEvento = $"ESTADO_{estadoNuevo.ToUpper()}";
                await _notificacionesUnificadas.CrearYEnviarNotificacionRolesAsync(
                    new[] { "OPERARIO", "SUPERVISOR", "ADMIN" },
                    tipoEvento,
                    titulo,
                    mensaje,
                    ordenId,
                    estadoAnterior,
                    estadoNuevo,
                    tipoVisual);

                _logger.LogInformation("Notificación de cambio de estado enviada para orden {OrdenId}: {EstadoAnterior} → {EstadoNuevo}", 
                    ordenId, estadoAnterior, estadoNuevo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de cambio de estado para orden {OrdenId}", ordenId);
            }
        }

        /// <summary>
        /// Determina el tipo de notificación y mensaje según el cambio de estado
        /// </summary>
        private (string titulo, string mensaje, string tipoVisual) DeterminarNotificacionCambioEstado(string estadoAnterior, string estadoNuevo, NombresUsuarios nombresUsuarios)
        {
            var operario = !string.IsNullOrEmpty(nombresUsuarios.Operario) ? nombresUsuarios.Operario : "Sistema";
            
            return (estadoAnterior, estadoNuevo) switch
            {
                (_, "ASIGNADO") => (
                    "Orden Asignada",
                    $"La orden de conteo ha sido asignada a {operario}",
                    "info"
                ),
                (_, "EN_PROGRESO") => (
                    "Orden Iniciada", 
                    $"La orden de conteo ha sido iniciada por {operario}",
                    "info"
                ),
                (_, "COMPLETADO") => (
                    "Orden Completada",
                    $"La orden de conteo ha sido completada por {operario}",
                    "success"
                ),
                (_, "CERRADO") => (
                    "Orden Cerrada",
                    $"La orden de conteo ha sido cerrada",
                    "success"
                ),
                (_, "CANCELADO") => (
                    "Orden Cancelada",
                    $"La orden de conteo ha sido cancelada por {operario}",
                    "error"
                ),
                _ => (
                    "Estado Actualizado",
                    $"El estado de la orden ha cambiado de {estadoAnterior} a {estadoNuevo}",
                    "info"
                )
            };
        }

        /// <summary>
        /// Extrae lista de artículos del FiltrosJson (soporta formato nuevo y antiguo)
        /// </summary>
        private List<string>? ExtraerArticulosDelFiltro(string? filtrosJson)
        {
            if (string.IsNullOrEmpty(filtrosJson)) return null;
            try
            {
                var filtros = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(filtrosJson);
                
                // Priorizar formato nuevo: array de artículos
                if (filtros?.ContainsKey("articulos") == true)
                {
                    var articulos = filtros["articulos"];
                    if (articulos.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        return articulos.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();
                    }
                }
                
                // Compatibilidad: formato antiguo con un solo artículo
                if (filtros?.ContainsKey("articulo") == true)
                {
                    var articulo = filtros["articulo"];
                    if (articulo.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var codigoArticulo = articulo.GetString();
                        if (!string.IsNullOrEmpty(codigoArticulo))
                        {
                            return new List<string> { codigoArticulo };
                        }
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

    }
}
