using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models;
using SGA_Api.Models.Login;
using SGA_Api.Models.UsuarioConf;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly SageDbContext _context;
        private readonly AuroraSgaDbContext _auroraSgaContext;
        public LoginController(SageDbContext context, AuroraSgaDbContext auroraSgaContext)
        {
            _context = context;
            _auroraSgaContext = auroraSgaContext;
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

            // 🔐 Generar nuevo token
            var nuevoToken = Guid.NewGuid().ToString();

            // 🔁 Desactivar todos los dispositivos activos de este usuario
            var dispositivosPrevios = await _auroraSgaContext.Dispositivos
                .Where(d => d.IdUsuario == operario.Id && d.Activo == -1)
                .ToListAsync();

            foreach (var d in dispositivosPrevios)
            {
                d.Activo = 0;
                d.SessionToken = null;
            }

            // ✅ Activar el nuevo dispositivo
            var dispositivoActual = await _auroraSgaContext.Dispositivos
                .FirstOrDefaultAsync(d => d.Id == login.IdDispositivo);

            if (dispositivoActual != null)
            {
                dispositivoActual.Activo = -1;
                dispositivoActual.IdUsuario = operario.Id;
                dispositivoActual.SessionToken = nuevoToken;
            }
            else
            {
                dispositivoActual = new Models.Registro.Dispositivo
                {
                    Id = login.IdDispositivo,
                    Tipo = dispositivoActual.Tipo, // "Android", // o lo que corresponda
                    Activo = -1,
                    IdUsuario = operario.Id,
                    SessionToken = nuevoToken
                };

                _auroraSgaContext.Dispositivos.Add(dispositivoActual);
            }

            await _auroraSgaContext.SaveChangesAsync();

            // Obtener accesos
            var accesos = await _context.AccesosOperarios
                .Where(a => a.Operario == operario.Id)
                .Select(a => a.MRH_CodigoAplicacion)
                .ToListAsync();

            var almacenes = await _context.OperariosAlmacenes
                .Where(a => a.Operario == operario.Id)
                .Select(a => a.CodigoAlmacen)
                .ToListAsync();

            // ✅ Insertar configuración por defecto si no existe en tabla Usuarios
            var existeUsuario = await _auroraSgaContext.Usuarios.AnyAsync(u => u.IdUsuario == operario.Id);
            if (!existeUsuario)
            {
                _auroraSgaContext.Usuarios.Add(new Usuario
                {
                    IdUsuario = operario.Id,
                    IdEmpresa = null, // valor por defecto
                    Impresora = null, // o null si la columna lo permite
                    Etiqueta = null   // lo mismo aquí
                });

                await _auroraSgaContext.SaveChangesAsync(); // importante guardar aquí
            } 

            return Ok(new LoginResponseDto
            {
                Operario = operario.Id,
                NombreOperario = operario.Nombre,
                CodigosAplicacion = accesos,
                CodigosAlmacen = almacenes,
                Token = nuevoToken 
            });


        }

    }
}
