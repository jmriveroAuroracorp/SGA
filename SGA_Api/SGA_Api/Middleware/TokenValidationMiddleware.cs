namespace SGA_Api.Middleware
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using SGA_Api.Data;
    using System.Linq;
    using System.Threading.Tasks;

    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AuroraSgaDbContext db)
        {
            var path = context.Request.Path.ToString().ToLower();

            bool rutaExenta =
                path.Contains("/swagger") ||
                path.Contains("/login") ||
                path.Contains("/dispositivo/registrar") ||
                path.Contains("/dispositivo/activo") ||
                path.StartsWith("/api/version");

            if (!rutaExenta)
            {
                if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                    !authHeader.ToString().StartsWith("Bearer "))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Token requerido.");
                    return;
                }

                var token = authHeader.ToString().Substring("Bearer ".Length);

                var tokenValido = await db.Dispositivos
                    .AnyAsync(d => d.SessionToken == token && d.Activo == -1);

                if (!tokenValido)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Token inválido o sesión cerrada.");
                    return;
                }
            }

            await _next(context);
        }
    }
}
