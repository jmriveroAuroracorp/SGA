using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace SGA_Api.Hubs
{
    /// <summary>
    /// Hub de SignalR para notificaciones en tiempo real sobre cambios de estado de traspasos
    /// </summary>
    public class NotificacionesTraspasosHub : Hub
    {
        private readonly AuroraSgaDbContext _context;
        private readonly ILogger<NotificacionesTraspasosHub> _logger;

        public NotificacionesTraspasosHub(AuroraSgaDbContext context, ILogger<NotificacionesTraspasosHub> logger)
        {
            _context = context;
            _logger = logger;
        }
        /// <summary>
        /// Método para unirse a un grupo específico de traspaso
        /// </summary>
        /// <param name="traspasoId">ID del traspaso al que se quiere suscribir</param>
        public async Task UnirseAGrupoTraspaso(string traspasoId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Traspaso_{traspasoId}");
            _logger.LogDebug("Cliente {ConnectionId} se unió al grupo Traspaso_{TraspasoId}", Context.ConnectionId, traspasoId);
        }

        /// <summary>
        /// Método para salir de un grupo específico de traspaso
        /// </summary>
        /// <param name="traspasoId">ID del traspaso del que se quiere salir</param>
        public async Task SalirDeGrupoTraspaso(string traspasoId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Traspaso_{traspasoId}");
            _logger.LogDebug("Cliente {ConnectionId} salió del grupo Traspaso_{TraspasoId}", Context.ConnectionId, traspasoId);
        }

        /// <summary>
        /// Método para unirse a un grupo unipersonal de usuario
        /// </summary>
        /// <param name="usuarioId">ID del usuario para crear grupo unipersonal</param>
        public async Task UnirseAGrupoUsuario(int usuarioId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Usuario_{usuarioId}");
            _logger.LogDebug("Cliente {ConnectionId} se unió al grupo Usuario_{UsuarioId}", Context.ConnectionId, usuarioId);
        }

        /// <summary>
        /// Método para salir de un grupo unipersonal de usuario
        /// </summary>
        /// <param name="usuarioId">ID del usuario para salir del grupo unipersonal</param>
        public async Task SalirDeGrupoUsuario(int usuarioId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Usuario_{usuarioId}");
            _logger.LogDebug("Cliente {ConnectionId} salió del grupo Usuario_{UsuarioId}", Context.ConnectionId, usuarioId);
        }

        /// <summary>
        /// Método para unirse a un grupo de rol específico
        /// </summary>
        /// <param name="rolNombre">Nombre del rol (OPERARIO, SUPERVISOR, ADMIN)</param>
        public async Task UnirseAGrupoRol(string rolNombre)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Rol_{rolNombre}");
            _logger.LogDebug("Cliente {ConnectionId} se unió al grupo Rol_{RolNombre}", Context.ConnectionId, rolNombre);
        }

        /// <summary>
        /// Método para salir de un grupo de rol específico
        /// </summary>
        /// <param name="rolNombre">Nombre del rol (OPERARIO, SUPERVISOR, ADMIN)</param>
        public async Task SalirDeGrupoRol(string rolNombre)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Rol_{rolNombre}");
            _logger.LogDebug("Cliente {ConnectionId} salió del grupo Rol_{RolNombre}", Context.ConnectionId, rolNombre);
        }

        /// <summary>
        /// Se ejecuta cuando un cliente se conecta
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Cliente {ConnectionId} intentando conectar", Context.ConnectionId);
            
            // Validar token de autenticación
            if (!await ValidarTokenAsync())
            {
                _logger.LogWarning("Cliente {ConnectionId} rechazado - token inválido", Context.ConnectionId);
                Context.Abort();
                return;
            }

            // Unirse automáticamente a grupos de rol basado en el token
            await UnirseAGruposDeRolAsync();

            _logger.LogDebug("Cliente {ConnectionId} conectado exitosamente", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Se une automáticamente a los grupos de rol del usuario
        /// </summary>
        private async Task UnirseAGruposDeRolAsync()
        {
            try
            {
                var httpContext = Context.GetHttpContext();
                if (httpContext == null) return;

                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    return;

                var token = authHeader.Substring("Bearer ".Length).Trim();
                
                // Obtener el rol del usuario desde el token
                var dispositivo = await _context.Dispositivos
                    .FirstOrDefaultAsync(d => d.SessionToken == token && d.Activo == -1);

                if (dispositivo?.IdUsuario != null)
                {
                    // Obtener el rol del usuario desde la tabla roles_sga
                    var usuario = await _context.Usuarios
                        .Where(u => u.IdUsuario == dispositivo.IdUsuario)
                        .Select(u => new { u.IdRol })
                        .FirstOrDefaultAsync();

                    if (usuario?.IdRol != null)
                    {
                        // Obtener el nombre del rol desde roles_sga
                        var rol = await _context.RolesSga
                            .Where(r => r.IdRol == usuario.IdRol)
                            .Select(r => r.NombreRol)
                            .FirstOrDefaultAsync();

                        if (!string.IsNullOrEmpty(rol))
                        {
                            // Mapear el nombre del rol de la BD a nombres estándar para SignalR
                            string rolSignalR = rol switch
                            {
                                "Administrador" => "ADMIN",
                                "Supervisor" => "SUPERVISOR", 
                                "Operario" => "OPERARIO",
                                _ => rol.ToUpper() // Por defecto, convertir a mayúsculas
                            };
                            
                            await Groups.AddToGroupAsync(Context.ConnectionId, $"Rol_{rolSignalR}");
                            _logger.LogDebug("Cliente {ConnectionId} se unió automáticamente al grupo Rol_{RolSignalR} (RolBD: {RolBD}, IdRol: {IdRol})", Context.ConnectionId, rolSignalR, rol, usuario.IdRol);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al unirse a grupos de rol para cliente {ConnectionId}", Context.ConnectionId);
            }
        }

        /// <summary>
        /// Valida el token de autenticación del usuario
        /// </summary>
        private async Task<bool> ValidarTokenAsync()
        {
            try
            {
                var httpContext = Context.GetHttpContext();
                if (httpContext == null) return false;

                // Obtener el token del header Authorization (mismo que todos los endpoints)
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return false;
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                var tokenValido = await _context.Dispositivos
                    .AnyAsync(d => d.SessionToken == token && d.Activo == -1);

                return tokenValido;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Se ejecuta cuando un cliente se desconecta
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning("Cliente {ConnectionId} desconectado con error: {Error}", Context.ConnectionId, exception.Message);
            }
            else
            {
                _logger.LogDebug("Cliente {ConnectionId} desconectado correctamente", Context.ConnectionId);
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}
