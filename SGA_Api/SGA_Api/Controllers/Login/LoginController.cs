using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models;
using SGA_Api.Models.Login;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly SageDbContext _context;
        private readonly AuroraSgaDbContext _auroraSgaContext;
        public LoginController(SageDbContext context, AuroraSgaDbContext auroraSgaContext)
        {
            _context = context;
            _auroraSgaContext = auroraSgaContext;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto login)
        {
            var operario = await _context.Operarios
                .FirstOrDefaultAsync(o => o.Id == login.Operario
                                       && o.Contraseña == login.Contraseña
                                       && o.FechaBaja == null);

            if (operario == null)
                return Unauthorized("Operario o contraseña incorrectos.");

            // ✅ Verifica si ya tiene sesión activa en otro dispositivo
            var sesionActiva = await _auroraSgaContext.Dispositivos
                .Where(d => d.Activo == -1)
                .Join(_auroraSgaContext.LogEventos,
                      d => d.Id,
                      l => l.IdDispositivo,
                      (d, l) => new { Dispositivo = d, Log = l })
                .AnyAsync(x => x.Log.IdUsuario == operario.Id && x.Dispositivo.Id != login.IdDispositivo);

            if (sesionActiva)
                return Conflict("El operario ya ha iniciado sesión en otro dispositivo.");

            // ✅ Si todo bien, marcar el dispositivo actual como activo (por si no lo está)
            var dispositivo = await _auroraSgaContext.Dispositivos.FindAsync(login.IdDispositivo);
            if (dispositivo != null)
            {
                dispositivo.Activo = -1;
                await _auroraSgaContext.SaveChangesAsync();
            } 

            var accesos = await _context.AccesosOperarios
                .Where(a => a.Operario == operario.Id)
                .Select(a => a.MRH_CodigoAplicacion)
                .ToListAsync();

            return Ok(new LoginResponseDto
            {
                Operario = operario.Id,
                NombreOperario = operario.Nombre,
                CodigosAplicacion = accesos
            });
        }
    }
}
