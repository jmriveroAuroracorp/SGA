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

            _logger.LogDebug("Cliente {ConnectionId} conectado exitosamente", Context.ConnectionId);
            await base.OnConnectedAsync();
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
