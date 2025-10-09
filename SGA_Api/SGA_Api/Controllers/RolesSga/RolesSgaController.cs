using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.RolesSga;
using SGA_Api.Services;
using System.Collections.Generic;

namespace SGA_Api.Controllers.RolesSga
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesSgaController : ControllerBase
    {
        private readonly IRolesSgaService _rolesSgaService;

        public RolesSgaController(IRolesSgaService rolesSgaService)
        {
            _rolesSgaService = rolesSgaService;
        }

        /// <summary>
        /// Obtiene todos los roles SGA disponibles
        /// </summary>
        /// <returns>Lista de roles SGA</returns>
        [HttpGet("all")]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<RolSgaDto>>> GetRolesSga()
        {
            try
            {
                var roles = await _rolesSgaService.GetRolesSgaAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene un rol SGA por su ID
        /// </summary>
        /// <param name="id">ID del rol</param>
        /// <returns>Rol SGA</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<RolSgaDto>> GetRolSga(int id)
        {
            try
            {
                var rol = await _rolesSgaService.GetRolSgaByIdAsync(id);
                if (rol == null)
                {
                    return NotFound(new { message = "Rol no encontrado" });
                }
                return Ok(rol);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        /// <summary>
        /// Sugiere un rol SGA para un operario basado en sus permisos ERP
        /// </summary>
        /// <param name="operarioId">ID del operario</param>
        /// <returns>Rol sugerido</returns>
        [HttpGet("sugerido/{operarioId}")]
        public async Task<ActionResult<RolSugeridoDto>> GetRolSugerido(int operarioId)
        {
            try
            {
                var rolSugerido = await _rolesSgaService.SuggestRolSgaAsync(operarioId);
                if (rolSugerido == null)
                {
                    return NotFound(new { message = "No se pudo sugerir un rol para el operario." });
                }
                return Ok(rolSugerido);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }
    }
}
