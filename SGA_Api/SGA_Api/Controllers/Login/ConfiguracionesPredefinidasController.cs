using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Login;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfiguracionesPredefinidasController : ControllerBase
    {
        private readonly AuroraSgaDbContext _auroraContext;
        private readonly SageDbContext _sageContext;

        public ConfiguracionesPredefinidasController(AuroraSgaDbContext auroraContext, SageDbContext sageContext)
        {
            _auroraContext = auroraContext;
            _sageContext = sageContext;
        }

        /// <summary>
        /// Obtiene lista de configuraciones predefinidas
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ConfiguracionPredefinidaListaDto>>> GetConfiguracionesPredefinidas()
        {
            Console.WriteLine($"Conectando a base de datos: {_auroraContext.Database.GetConnectionString()}");
            
            var configuraciones = await _auroraContext.ConfiguracionesPredefinidas
                .Where(c => c.Activa)
                .Select(c => new ConfiguracionPredefinidaListaDto
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Descripcion = c.Descripcion,
                    FechaCreacion = c.FechaCreacion,
                    FechaModificacion = c.FechaModificacion,
                    Activa = c.Activa,
                    CantidadPermisos = c.Permisos.Count,
                    CantidadEmpresas = c.Empresas.Count,
                    CantidadAlmacenes = c.Almacenes.Count
                })
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            return Ok(configuraciones);
        }

        /// <summary>
        /// Obtiene una configuración predefinida completa por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ConfiguracionPredefinidaCompletaDto>> GetConfiguracionPredefinida(int id)
        {
            Console.WriteLine($"Cargando configuración {id}");
            
            var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                .Include(c => c.Permisos)
                .Include(c => c.Empresas)
                .Include(c => c.Almacenes)
                .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

            if (configuracion == null)
            {
                Console.WriteLine($"Configuración {id} no encontrada");
                return NotFound();
            }
            
            Console.WriteLine($"Configuración encontrada: {configuracion.Nombre}");
            Console.WriteLine($"Permisos cargados: {configuracion.Permisos.Count}");
            foreach (var permiso in configuracion.Permisos)
            {
                Console.WriteLine($"Permiso cargado: {permiso.MRH_CodigoAplicacion}");
            }

            var resultado = new ConfiguracionPredefinidaCompletaDto
            {
                Id = configuracion.Id,
                Nombre = configuracion.Nombre,
                Descripcion = configuracion.Descripcion,
                FechaCreacion = configuracion.FechaCreacion,
                FechaModificacion = configuracion.FechaModificacion,
                Activa = configuracion.Activa,
                
                // Límites
                LimiteEuros = configuracion.LimiteEuros,
                LimiteUnidades = configuracion.LimiteUnidades,
                
                // Usuarios de auditoría
                UsuarioCreacion = configuracion.UsuarioCreacion,
                UsuarioModificacion = configuracion.UsuarioModificacion,
                Permisos = configuracion.Permisos.Select(p => new PermisoDisponibleDto
                {
                    Codigo = p.MRH_CodigoAplicacion,
                    Descripcion = "" // Se puede obtener de la tabla MRH_AplicacionesSGA si es necesario
                }).ToList(),
                Empresas = configuracion.Empresas.Select(e => new Models.Login.EmpresaConfiguracionDto
                {
                    CodigoEmpresa = e.CodigoEmpresa,
                    EmpresaOrigen = e.EmpresaOrigen,
                    Nombre = _sageContext.Empresas
                        .Where(emp => emp.CodigoEmpresa == e.CodigoEmpresa)
                        .Select(emp => emp.EmpresaNombre)
                        .FirstOrDefault() ?? $"Empresa {e.CodigoEmpresa}"
                }).ToList(),
                Almacenes = configuracion.Almacenes.Select(a => new Models.Login.AlmacenConfiguracionDto
                {
                    CodigoAlmacen = a.CodigoAlmacen,
                    Descripcion = _sageContext.Almacenes
                        .Where(alm => alm.CodigoAlmacen == a.CodigoAlmacen && alm.CodigoEmpresa == 1)
                        .Select(alm => alm.Almacen)
                        .FirstOrDefault() ?? $"Almacén {a.CodigoAlmacen}",
                    CodigoEmpresa = 1,
                    NombreEmpresa = _sageContext.Empresas
                        .Where(emp => emp.CodigoEmpresa == 1)
                        .Select(emp => emp.EmpresaNombre)
                        .FirstOrDefault() ?? "Empresa 1"
                }).ToList()
            };

            return Ok(resultado);
        }

        /// <summary>
        /// Crea una nueva configuración predefinida
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ConfiguracionPredefinidaListaDto>> CrearConfiguracionPredefinida(ConfiguracionPredefinidaCrearDto dto)
        {
            using var transaction = await _auroraContext.Database.BeginTransactionAsync();
            try
            {
                // Crear la configuración principal
                var configuracion = new ConfiguracionPredefinida
                {
                    Nombre = dto.Nombre,
                    Descripcion = dto.Descripcion,
                    FechaCreacion = DateTime.Now,
                    Activa = true,
                    
                    // Límites
                    LimiteEuros = dto.LimiteEuros,
                    LimiteUnidades = dto.LimiteUnidades,
                    
                    // Usuario de creación
                    UsuarioCreacion = dto.Usuario
                };

                _auroraContext.ConfiguracionesPredefinidas.Add(configuracion);
                await _auroraContext.SaveChangesAsync();

                // Agregar permisos
                foreach (var permiso in dto.Permisos)
                {
                    _auroraContext.ConfiguracionesPredefinidasPermisos.Add(new ConfiguracionPredefinidaPermiso
                    {
                        ConfiguracionId = configuracion.Id,
                        MRH_CodigoAplicacion = permiso
                    });
                }

                // Agregar empresas
                foreach (var empresa in dto.Empresas)
                {
                    _auroraContext.ConfiguracionesPredefinidasEmpresas.Add(new ConfiguracionPredefinidaEmpresa
                    {
                        ConfiguracionId = configuracion.Id,
                        CodigoEmpresa = empresa
                    });
                }

                // Agregar almacenes
                foreach (var almacen in dto.Almacenes)
                {
                    _auroraContext.ConfiguracionesPredefinidasAlmacenes.Add(new ConfiguracionPredefinidaAlmacen
                    {
                        ConfiguracionId = configuracion.Id,
                        CodigoAlmacen = almacen
                    });
                }

                await _auroraContext.SaveChangesAsync();
                await transaction.CommitAsync();

                var resultado = new ConfiguracionPredefinidaListaDto
                {
                    Id = configuracion.Id,
                    Nombre = configuracion.Nombre,
                    Descripcion = configuracion.Descripcion,
                    FechaCreacion = configuracion.FechaCreacion,
                    Activa = configuracion.Activa,
                    CantidadPermisos = dto.Permisos.Count,
                    CantidadEmpresas = dto.Empresas.Count,
                    CantidadAlmacenes = dto.Almacenes.Count
                };

                return CreatedAtAction(nameof(GetConfiguracionPredefinida), new { id = configuracion.Id }, resultado);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Actualiza una configuración predefinida
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult> ActualizarConfiguracionPredefinida(int id, ConfiguracionPredefinidaCrearDto dto)
        {
            // Debug: Log de datos recibidos
            Console.WriteLine($"Actualizando configuración {id}");
            Console.WriteLine($"Permisos recibidos: {string.Join(", ", dto.Permisos)}");
            Console.WriteLine($"Empresas recibidas: {string.Join(", ", dto.Empresas)}");
            Console.WriteLine($"Almacenes recibidos: {string.Join(", ", dto.Almacenes)}");
            
            using var transaction = await _auroraContext.Database.BeginTransactionAsync();
            try
            {
                var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                    .Include(c => c.Permisos)
                    .Include(c => c.Empresas)
                    .Include(c => c.Almacenes)
                    .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

                if (configuracion == null)
                    return NotFound();

                // Actualizar datos principales
                configuracion.Nombre = dto.Nombre;
                configuracion.Descripcion = dto.Descripcion;
                configuracion.FechaModificacion = DateTime.Now;
                
                // Actualizar límites
                configuracion.LimiteEuros = dto.LimiteEuros;
                configuracion.LimiteUnidades = dto.LimiteUnidades;
                
                // Actualizar usuario de modificación
                if (dto.Usuario.HasValue && dto.Usuario.Value > 0)
                {
                    configuracion.UsuarioModificacion = dto.Usuario;
                }

                // Debug: Log de permisos existentes
                Console.WriteLine($"Permisos existentes antes de eliminar: {configuracion.Permisos.Count}");
                
                // Eliminar relaciones existentes
                _auroraContext.ConfiguracionesPredefinidasPermisos.RemoveRange(configuracion.Permisos);
                _auroraContext.ConfiguracionesPredefinidasEmpresas.RemoveRange(configuracion.Empresas);
                _auroraContext.ConfiguracionesPredefinidasAlmacenes.RemoveRange(configuracion.Almacenes);

                // Debug: Log de permisos a agregar
                Console.WriteLine($"Agregando {dto.Permisos.Count} permisos nuevos");
                
                // Agregar nuevas relaciones
                foreach (var permiso in dto.Permisos)
                {
                    Console.WriteLine($"Agregando permiso: {permiso}");
                    _auroraContext.ConfiguracionesPredefinidasPermisos.Add(new ConfiguracionPredefinidaPermiso
                    {
                        ConfiguracionId = configuracion.Id,
                        MRH_CodigoAplicacion = permiso
                    });
                }

                foreach (var empresa in dto.Empresas)
                {
                    _auroraContext.ConfiguracionesPredefinidasEmpresas.Add(new ConfiguracionPredefinidaEmpresa
                    {
                        ConfiguracionId = configuracion.Id,
                        CodigoEmpresa = empresa
                    });
                }

                foreach (var almacen in dto.Almacenes)
                {
                    _auroraContext.ConfiguracionesPredefinidasAlmacenes.Add(new ConfiguracionPredefinidaAlmacen
                    {
                        ConfiguracionId = configuracion.Id,
                        CodigoAlmacen = almacen
                    });
                }

                await _auroraContext.SaveChangesAsync();
                
                // Debug: Verificar permisos guardados
                var permisosGuardados = await _auroraContext.ConfiguracionesPredefinidasPermisos
                    .Where(p => p.ConfiguracionId == configuracion.Id)
                    .ToListAsync();
                Console.WriteLine($"Permisos guardados en BD: {permisosGuardados.Count}");
                Console.WriteLine($"Base de datos: {_auroraContext.Database.GetConnectionString()}");
                foreach (var permiso in permisosGuardados)
                {
                    Console.WriteLine($"Permiso guardado: {permiso.MRH_CodigoAplicacion}");
                }
                
                await transaction.CommitAsync();

                return NoContent();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Elimina una configuración predefinida (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> EliminarConfiguracionPredefinida(int id)
        {
            var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

            if (configuracion == null)
                return NotFound();

            configuracion.Activa = false;
            configuracion.FechaModificacion = DateTime.Now;

            await _auroraContext.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Aplica una configuración predefinida a un operario
        /// </summary>
        [HttpPost("aplicar")]
        public async Task<ActionResult> AplicarConfiguracionPredefinida(AplicarConfiguracionPredefinidaDto dto)
        {
            using var transaction = await _auroraContext.Database.BeginTransactionAsync();
            try
            {
                // Verificar que el operario existe
                var operario = await _sageContext.Operarios
                    .FirstOrDefaultAsync(o => o.Id == dto.OperarioId && o.FechaBaja == null);

                if (operario == null)
                    return NotFound("Operario no encontrado");

                // Verificar que la configuración existe
                var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                    .Include(c => c.Permisos)
                    .Include(c => c.Empresas)
                    .Include(c => c.Almacenes)
                    .FirstOrDefaultAsync(c => c.Id == dto.ConfiguracionId && c.Activa);

                if (configuracion == null)
                    return NotFound("Configuración predefinida no encontrada");

                // Si se debe reemplazar, eliminar configuración actual
                if (dto.ReemplazarExistente)
                {
                    // Eliminar permisos actuales
                    var permisosActuales = await _sageContext.AccesosOperarios
                        .Where(a => a.Operario == dto.OperarioId && a.CodigoEmpresa == 1)
                        .ToListAsync();
                    _sageContext.AccesosOperarios.RemoveRange(permisosActuales);

                    // Eliminar empresas actuales
                    var empresasActuales = await _sageContext.OperariosEmpresas
                        .Where(e => e.Operario == dto.OperarioId)
                        .ToListAsync();
                    _sageContext.OperariosEmpresas.RemoveRange(empresasActuales);

                    // Eliminar almacenes actuales
                    var almacenesActuales = await _sageContext.OperariosAlmacenes
                        .Where(a => a.Operario == dto.OperarioId)
                        .ToListAsync();
                    _sageContext.OperariosAlmacenes.RemoveRange(almacenesActuales);
                }

                // Aplicar permisos
                foreach (var permiso in configuracion.Permisos)
                {
                    _sageContext.AccesosOperarios.Add(new AccesoOperario
                    {
                        CodigoEmpresa = 1,
                        Operario = dto.OperarioId,
                        MRH_CodigoAplicacion = permiso.MRH_CodigoAplicacion
                    });
                }

                // Aplicar empresas
                foreach (var empresa in configuracion.Empresas)
                {
                    _sageContext.OperariosEmpresas.Add(new OperarioEmpresa
                    {
                        CodigoEmpresa = 1,
                        Operario = dto.OperarioId,
                        EmpresaOrigen = empresa.CodigoEmpresa,
                        Empresa = empresa.CodigoEmpresa.ToString()
                    });
                }

                // Aplicar almacenes
                foreach (var almacen in configuracion.Almacenes)
                {
                    _sageContext.OperariosAlmacenes.Add(new OperarioAlmacen
                    {
                        CodigoEmpresa = 1,
                        Operario = dto.OperarioId,
                        CodigoAlmacen = almacen.CodigoAlmacen
                    });
                }

                await _auroraContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Configuración aplicada exitosamente" });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
