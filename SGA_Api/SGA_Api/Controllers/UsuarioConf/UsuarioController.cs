using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.UsuarioConf;

namespace SGA_Api.Controllers.UsuarioConf
{
	[ApiController]
	[Route("api/[controller]")]
	public class UsuariosController : ControllerBase

    {
        private readonly AuroraSgaDbContext _context;

        public UsuariosController(AuroraSgaDbContext context)
        {
            _context = context;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UsuarioDto>> GetUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            return new UsuarioDto
            {
                IdUsuario = usuario.IdUsuario,
                IdEmpresa = usuario.IdEmpresa,
                Impresora = usuario.Impresora,
                Etiqueta = usuario.Etiqueta
            };
        }

        [HttpPost]
        public async Task<IActionResult> CrearUsuario([FromBody] UsuarioDto nuevo)
        {
            var existe = await _context.Usuarios.AnyAsync(u => u.IdUsuario == nuevo.IdUsuario);
            if (existe)
                return Conflict("El usuario ya existe.");

            _context.Usuarios.Add(new Usuario
            {
                IdUsuario = nuevo.IdUsuario,
                IdEmpresa = nuevo.IdEmpresa,
                Impresora = nuevo.Impresora,
                Etiqueta = nuevo.Etiqueta
            });

            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetUsuario), new { id = nuevo.IdUsuario }, nuevo);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> ActualizarUsuarioParcial(int id, [FromBody] UsuarioUpdateDto dto)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            if (dto.IdEmpresa != null)
                usuario.IdEmpresa = dto.IdEmpresa;

            if (dto.Impresora != null)
                usuario.Impresora = dto.Impresora;

            if (dto.Etiqueta != null)
                usuario.Etiqueta = dto.Etiqueta;

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
