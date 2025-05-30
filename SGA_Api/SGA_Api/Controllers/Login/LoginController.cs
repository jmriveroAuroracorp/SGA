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

        public LoginController(SageDbContext context)
        {
            _context = context;
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
