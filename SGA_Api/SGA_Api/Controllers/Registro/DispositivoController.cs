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
            if (request.IdUsuario == null)
                return BadRequest("ID de usuario requerido.");

            // Comprobamos si el usuario ya tiene otro dispositivo activo distinto
            var otroDispositivoActivo = await _context.Dispositivos
                .FirstOrDefaultAsync(d =>
                    d.IdUsuario == request.IdUsuario &&
                    d.Activo == -1 &&
                    d.Id != request.Id);

            if (otroDispositivoActivo != null)
            {
                return Conflict("Este usuario ya tiene una sesión activa en otro dispositivo.");
            }

            var dispositivo = await _context.Dispositivos.FirstOrDefaultAsync(d => d.Id == request.Id);

            if (dispositivo == null)
            {
                dispositivo = new Dispositivo
                {
                    Id = request.Id,
                    Tipo = request.Tipo,
                    Activo = -1,
                    IdUsuario = request.IdUsuario
                };

                _context.Dispositivos.Add(dispositivo);
            }
            else
            {
                if (dispositivo.Activo == -1 && dispositivo.IdUsuario != request.IdUsuario)
                {
                    return Conflict("Este dispositivo ya está en uso por otro usuario.");
                }

                dispositivo.Activo = -1;
                dispositivo.IdUsuario = request.IdUsuario;

                _context.Dispositivos.Update(dispositivo);
            }

            await _context.SaveChangesAsync();

            return Ok();
        }



        [HttpPost("desactivar")]
        public async Task<IActionResult> DesactivarDispositivo([FromBody] ObtenerDispositivoDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
                return BadRequest("ID de dispositivo requerido.");

            var dispositivo = await _context.Dispositivos.FirstOrDefaultAsync(d => d.Id == request.Id);

            if (dispositivo == null)
                return NotFound($"No se encontró el dispositivo con ID '{request.Id}'.");

            dispositivo.Activo = 0;

            _context.Dispositivos.Update(dispositivo);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("activo")]
        public async Task<ActionResult<DispositivoActivoDto>> ObtenerDispositivoActivo([FromQuery] int idUsuario)
        {
            var dispositivo = await _context.Dispositivos
                .FirstOrDefaultAsync(d => d.IdUsuario == idUsuario && d.Activo == -1);

            if (dispositivo == null)
                return NotFound("No hay ningún dispositivo activo para este usuario.");

            return Ok(new DispositivoActivoDto
            {
                Id = dispositivo.Id,
                Tipo = dispositivo.Tipo
            });
        }
    }
}