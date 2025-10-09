using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.UsuarioConf;
using SGA_Api.Models.RolesSga;

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
                IdRol = usuario.IdRol,
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
                IdRol = nuevo.IdRol,
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

            if (dto.IdRol != null)
                usuario.IdRol = dto.IdRol;

            if (dto.Etiqueta != null)
                usuario.Etiqueta = dto.Etiqueta;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Asigna un rol SGA a un usuario
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <param name="rolId">ID del rol a asignar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPut("{id}/rol")]
        public async Task<IActionResult> AsignarRol(int id, [FromBody] int rolId)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                // Validar que el rol existe (esto se podría hacer a través de un servicio)
                // Por ahora, solo validamos que sea un número positivo
                if (rolId <= 0)
                    return BadRequest(new { message = "ID de rol inválido" });

                usuario.IdRol = rolId;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Rol asignado correctamente", usuarioId = id, rolId = rolId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene el rol asignado a un usuario
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <returns>Información del rol del usuario</returns>
        [HttpGet("{id}/rol")]
        public async Task<ActionResult<object>> GetRolUsuario(int id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(new { usuarioId = id, rolId = usuario.IdRol });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }
    }
}
