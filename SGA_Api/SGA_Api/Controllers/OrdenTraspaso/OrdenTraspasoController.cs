using Microsoft.AspNetCore.Mvc;
using SGA_Api.Models.OrdenTraspaso;
using SGA_Api.Services;

namespace SGA_Api.Controllers.OrdenTraspaso
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenTraspasoController : ControllerBase
    {
        private readonly IOrdenTraspasoService _ordenTraspasoService;

        public OrdenTraspasoController(IOrdenTraspasoService ordenTraspasoService)
        {
            _ordenTraspasoService = ordenTraspasoService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenTraspasoDto>>> GetOrdenesTraspaso(
            [FromQuery] short? codigoEmpresa = null,
            [FromQuery] string? estado = null,
            [FromQuery] int? usuarioAsignado = null)
        {
            var ordenes = await _ordenTraspasoService.GetOrdenesTraspasoAsync(codigoEmpresa, estado, usuarioAsignado);
            return Ok(ordenes);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrdenTraspasoDto>> GetOrdenTraspaso(Guid id)
        {
            var orden = await _ordenTraspasoService.GetOrdenTraspasoAsync(id);
            if (orden == null)
                return NotFound();

            return Ok(orden);
        }

        [HttpPost]
        public async Task<ActionResult<OrdenTraspasoDto>> CrearOrdenTraspaso(CrearOrdenTraspasoDto dto)
        {
            var orden = await _ordenTraspasoService.CrearOrdenTraspasoAsync(dto);
            return CreatedAtAction(nameof(GetOrdenTraspaso), new { id = orden.IdOrdenTrabajo }, orden);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarOrdenTraspaso(Guid id, ActualizarOrdenTraspasoDto dto)
        {
            var result = await _ordenTraspasoService.ActualizarOrdenTraspasoAsync(id, dto);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPut("linea/{id}")]
        public async Task<IActionResult> ActualizarLineaOrdenTraspaso(Guid id, ActualizarLineaOrdenTraspasoDto dto)
        {
            var result = await _ordenTraspasoService.ActualizarLineaOrdenTraspasoAsync(id, dto);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/completar")]
        public async Task<IActionResult> CompletarOrdenTraspaso(Guid id)
        {
            var result = await _ordenTraspasoService.CompletarOrdenTraspasoAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/cancelar")]
        public async Task<IActionResult> CancelarOrdenTraspaso(Guid id)
        {
            var result = await _ordenTraspasoService.CancelarOrdenTraspasoAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarOrdenTraspaso(Guid id)
        {
            var result = await _ordenTraspasoService.EliminarOrdenTraspasoAsync(id);
            if (!result)
                return NotFound();

            return NoContent();
        }

        [HttpPost("movimiento")]
        public async Task<IActionResult> RegistrarMovimiento(RegistrarMovimientoDto dto)
        {
            var result = await _ordenTraspasoService.RegistrarMovimientoAsync(dto);
            if (!result)
                return BadRequest();

            return Ok();
        }
    }
} 