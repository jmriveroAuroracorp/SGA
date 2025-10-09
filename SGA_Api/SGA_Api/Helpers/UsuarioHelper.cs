using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;

namespace SGA_Api.Helpers
{
    public static class UsuarioHelper
    {
        /// <summary>
        /// Obtiene el ID del usuario desde el token de autorización
        /// </summary>
        /// <param name="context">HttpContext de la petición</param>
        /// <param name="dbContext">Contexto de base de datos</param>
        /// <returns>ID del usuario o null si no se encuentra</returns>
        public static async Task<int?> ObtenerUsuarioDesdeTokenAsync(HttpContext context, AuroraSgaDbContext dbContext)
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                !authHeader.ToString().StartsWith("Bearer "))
            {
                return null;
            }

            var token = authHeader.ToString().Substring("Bearer ".Length);

            var dispositivo = await dbContext.Dispositivos
                .FirstOrDefaultAsync(d => d.SessionToken == token && d.Activo == -1);

            return dispositivo?.IdUsuario;
        }
    }
}
