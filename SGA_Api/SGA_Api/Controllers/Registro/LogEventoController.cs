using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Registro;

namespace SGA_Api.Controllers.Registro
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogEventoController : ControllerBase
    {
        private readonly AuroraSgaDbContext _context;

        public LogEventoController(AuroraSgaDbContext context)
        {
            _context = context;
        }

        [HttpPost("crear")]
        public async Task<IActionResult> CrearEvento([FromBody] CrearLogEventoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.IdDispositivo))
                return BadRequest("ID de dispositivo requerido.");

            var log = new LogEvento
            {
                Fecha = dto.Fecha ?? DateTime.Now,
                IdUsuario = dto.IdUsuario,
                Tipo = dto.Tipo,
                Origen = dto.Origen,
                Descripcion = dto.Descripcion,
                Detalle = dto.Detalle,
                IdDispositivo = dto.IdDispositivo
            };

            _context.LogEventos.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { log.Id });
        }
    }
}

