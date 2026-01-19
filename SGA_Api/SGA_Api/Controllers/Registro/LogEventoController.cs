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
            // Validar IdDispositivo
            if (string.IsNullOrWhiteSpace(dto.IdDispositivo))
                return BadRequest("ID de dispositivo requerido.");

            // Validar que el dispositivo exista
            var dispositivoExiste = await _context.Dispositivos
                .AnyAsync(d => d.Id == dto.IdDispositivo);
            
            if (!dispositivoExiste)
                return BadRequest($"Dispositivo con ID '{dto.IdDispositivo}' no encontrado.");

            // Validar Tipo (recomendado pero no obligatorio)
            if (string.IsNullOrWhiteSpace(dto.Tipo))
            {
                // Si no se proporciona Tipo, usar un valor por defecto
                dto.Tipo = "EVENTO_GENERICO";
            }

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

