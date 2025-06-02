using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Login;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class OperariosAccesoController : ControllerBase
    {
        private readonly SageDbContext _context;

        public OperariosAccesoController(SageDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetOperariosConAcceso()
        {
            var resultado = await (from o in _context.Operarios
                                   join a in _context.AccesosOperarios
                                       on o.Id equals a.Operario
                                   where o.FechaBaja == null && a.MRH_CodigoAplicacion == 7
                                   select new OperariosAccesoDto
                                   {
                                       Operario = o.Id,
                                       NombreOperario = o.Nombre!,
                                       Contraseña = o.Contraseña,
                                       MRH_CodigoAplicacion = a.MRH_CodigoAplicacion
                                   }).ToListAsync();

            return Ok(resultado);
        }
    }
}
