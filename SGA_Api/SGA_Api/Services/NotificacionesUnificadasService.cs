using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Notificaciones;

namespace SGA_Api.Services
{
    /// <summary>
    /// Servicio unificado que garantiza que todas las notificaciones se guarden en BD y se envíen por SignalR
    /// </summary>
    public class NotificacionesUnificadasService : INotificacionesUnificadasService
    {
        private readonly INotificacionesService _notificacionesBdService;
        private readonly INotificacionesTraspasosService _notificacionesSignalRService;
        private readonly AuroraSgaDbContext _context;
        private readonly ILogger<NotificacionesUnificadasService> _logger;

        public NotificacionesUnificadasService(
            INotificacionesService notificacionesBdService,
            INotificacionesTraspasosService notificacionesSignalRService,
            AuroraSgaDbContext context,
            ILogger<NotificacionesUnificadasService> logger)
        {
            _notificacionesBdService = notificacionesBdService;
            _notificacionesSignalRService = notificacionesSignalRService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Crea una notificación para un usuario específico, guardándola en BD y enviándola por SignalR
        /// </summary>
        public async Task<Notificacion> CrearYEnviarNotificacionUsuarioAsync(
            int usuarioId,
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid? procesoId = null,
            string? estadoAnterior = null,
            string? estadoActual = null,
            string tipoVisual = "info")
        {
            Notificacion? notificacionBd = null;
            bool bdGuardada = false;
            bool signalREnviado = false;

            try
            {
                // PASO 1: Guardar en BD
                var crearDto = new CrearNotificacionDto
                {
                    CodigoEmpresa = 1,
                    TipoNotificacion = tipoNotificacion,
                    ProcesoId = procesoId,
                    Titulo = titulo,
                    Mensaje = mensaje,
                    EstadoAnterior = estadoAnterior,
                    EstadoActual = estadoActual,
                    EsGrupal = false,
                    UsuarioIds = new List<int> { usuarioId }
                };

                notificacionBd = await _notificacionesBdService.CrearNotificacionAsync(crearDto);
                bdGuardada = true;
                _logger.LogInformation("✅ Notificación guardada en BD para usuario {UsuarioId}: {Titulo}", usuarioId, titulo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al guardar notificación en BD para usuario {UsuarioId}", usuarioId);
                // Continuar con SignalR aunque falle la BD
            }

            try
            {
                // PASO 2: Enviar por SignalR
                _logger.LogInformation("🔔 [Unificadas] Intentando enviar notificación por SignalR a usuario {UsuarioId}: {Titulo}", usuarioId, titulo);
                await _notificacionesSignalRService.NotificarPopupUsuarioAsync(
                    usuarioId, 
                    titulo, 
                    mensaje, 
                    tipoVisual,
                    tipoNotificacion,  // Pasar el tipo real (INVENTARIO_CIERRE, etc.)
                    procesoId,         // Pasar el procesoId
                    notificacionBd?.CodigoEmpresa ?? 1);  // Pasar CodigoEmpresa
                signalREnviado = true;
                _logger.LogInformation("✅ [Unificadas] Notificación enviada por SignalR a usuario {UsuarioId}: {Titulo}", usuarioId, titulo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Unificadas] Error al enviar notificación por SignalR a usuario {UsuarioId}: {Error}", usuarioId, ex.Message);
                _logger.LogError(ex, "❌ [Unificadas] StackTrace: {StackTrace}", ex.StackTrace);
                // Si falla SignalR pero BD está guardada, la notificación sigue disponible
            }

            if (!bdGuardada && !signalREnviado)
            {
                _logger.LogError("❌ CRÍTICO: No se pudo guardar ni enviar la notificación para usuario {UsuarioId}", usuarioId);
                throw new Exception($"No se pudo crear la notificación para usuario {usuarioId}");
            }

            // Si BD falló pero SignalR funcionó, crear una notificación mínima para retornar
            if (!bdGuardada && signalREnviado)
            {
                _logger.LogWarning("⚠️ Notificación enviada por SignalR pero no guardada en BD para usuario {UsuarioId}", usuarioId);
                // Retornar null o crear una notificación temporal - por ahora retornamos null
                return null!;
            }

            return notificacionBd!;
        }

        /// <summary>
        /// Crea notificaciones para usuarios con roles específicos, guardándolas en BD y enviándolas por SignalR
        /// </summary>
        public async Task<List<Notificacion>> CrearYEnviarNotificacionRolesAsync(
            string[] roles,
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid? procesoId = null,
            string? estadoAnterior = null,
            string? estadoActual = null,
            string tipoVisual = "info")
        {
            List<Notificacion> notificacionesCreadas = new();
            bool bdGuardada = false;
            bool signalREnviado = false;

            try
            {
                // Obtener IDs de usuarios con los roles especificados
                var usuarioIds = await _context.Usuarios
                    .Where(u => u.IdRol.HasValue && (
                        (u.IdRol == 3 && roles.Contains("OPERARIO")) ||
                        (u.IdRol == 10 && roles.Contains("OPERARIO")) ||
                        (u.IdRol == 20 && roles.Contains("SUPERVISOR")) ||
                        (u.IdRol == 30 && roles.Contains("ADMIN"))
                    ))
                    .Select(u => u.IdUsuario)
                    .ToListAsync();

                if (usuarioIds.Any())
                {
                    // PASO 1: Guardar en BD
                    var crearDto = new CrearNotificacionDto
                    {
                        CodigoEmpresa = 1,
                        TipoNotificacion = tipoNotificacion,
                        ProcesoId = procesoId,
                        Titulo = titulo,
                        Mensaje = mensaje,
                        EstadoAnterior = estadoAnterior,
                        EstadoActual = estadoActual,
                        EsGrupal = true,
                        GrupoDestino = string.Join(",", roles),
                        UsuarioIds = usuarioIds
                    };

                    var notificacionBd = await _notificacionesBdService.CrearNotificacionAsync(crearDto);
                    notificacionesCreadas.Add(notificacionBd);
                    bdGuardada = true;
                    _logger.LogInformation("✅ Notificación guardada en BD para {Cantidad} usuarios con roles {Roles}", usuarioIds.Count, string.Join(",", roles));
                }
                else
                {
                    _logger.LogWarning("⚠️ No se encontraron usuarios con roles {Roles}", string.Join(",", roles));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al guardar notificación en BD para roles {Roles}", string.Join(",", roles));
                // Continuar con SignalR aunque falle la BD
            }

            try
            {
                // PASO 2: Enviar por SignalR a cada rol
                foreach (var rol in roles)
                {
                    await _notificacionesSignalRService.NotificarRolAsync(rol, titulo, mensaje, tipoVisual);
                }
                signalREnviado = true;
                _logger.LogInformation("✅ Notificación enviada por SignalR a roles {Roles}", string.Join(",", roles));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al enviar notificación por SignalR a roles {Roles}", string.Join(",", roles));
                // Si falla SignalR pero BD está guardada, la notificación sigue disponible
            }

            if (!bdGuardada && !signalREnviado)
            {
                _logger.LogError("❌ CRÍTICO: No se pudo guardar ni enviar la notificación para roles {Roles}", string.Join(",", roles));
            }

            return notificacionesCreadas;
        }

        /// <summary>
        /// Crea notificaciones para usuarios con nivel jerárquico igual o superior, guardándolas en BD y enviándolas por SignalR
        /// </summary>
        public async Task<List<Notificacion>> CrearYEnviarNotificacionNivelAsync(
            int nivelMinimo,
            string tipoNotificacion,
            string titulo,
            string mensaje,
            Guid? procesoId = null,
            string tipoVisual = "info")
        {
            // Determinar qué roles incluir según el nivel mínimo
            var roles = new List<string>();
            var niveles = new[] { 10, 20, 30 };
            var nombresRoles = new[] { "OPERARIO", "SUPERVISOR", "ADMIN" };

            for (int i = 0; i < niveles.Length; i++)
            {
                if (niveles[i] >= nivelMinimo)
                {
                    roles.Add(nombresRoles[i]);
                }
            }

            if (!roles.Any())
            {
                _logger.LogWarning("⚠️ No hay roles con nivel >= {NivelMinimo}", nivelMinimo);
                return new List<Notificacion>();
            }

            // Usar el método de roles para crear y enviar
            return await CrearYEnviarNotificacionRolesAsync(
                roles.ToArray(),
                tipoNotificacion,
                titulo,
                mensaje,
                procesoId,
                null,
                null,
                tipoVisual);
        }
    }
}

