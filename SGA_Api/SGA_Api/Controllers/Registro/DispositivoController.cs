using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Registro;

namespace SGA_Api.Controllers.Registro
{
    [ApiController]
    [Route("api/[controller]")]
    public class DispositivoController : ControllerBase
    {
        private readonly AuroraSgaDbContext _context;

        public DispositivoController(AuroraSgaDbContext context)
        {
            _context = context;
        }

        [HttpPost("registrar")]
        public async Task<IActionResult> RegistrarDispositivo([FromBody] ObtenerDispositivoDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
                return BadRequest("ID de dispositivo requerido.");

            var dispositivo = await _context.Dispositivos.FirstOrDefaultAsync(d => d.Id == request.Id);

            if (dispositivo == null)
            {
                dispositivo = new Dispositivo
                {
                    Id = request.Id,
                    Nombre = request.NombreOperario,
                    Tipo = request.Tipo,
                    Activo = -1 
                };

                _context.Dispositivos.Add(dispositivo);
            }
            else
            {
                dispositivo.Activo = -1; // Siempre establecer -1

                _context.Dispositivos.Update(dispositivo);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}