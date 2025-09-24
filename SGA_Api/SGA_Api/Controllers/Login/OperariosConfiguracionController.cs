using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Login;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class OperariosConfiguracionController : ControllerBase
    {
        private readonly SageDbContext _context;
        private readonly AuroraSgaDbContext _auroraContext;

        public OperariosConfiguracionController(SageDbContext context, AuroraSgaDbContext auroraContext)
        {
            _context = context;
            _auroraContext = auroraContext;
        }

        /// <summary>
        /// Obtiene lista de operarios que tienen acceso al SGA
        /// </summary>
        [HttpGet("disponibles")]
        public async Task<ActionResult<List<OperarioDisponibleDto>>> GetOperariosDisponibles()
        {
            // Primero obtener los operarios básicos
            var operarios = await (from o in _context.Operarios
                                   join a in _context.AccesosOperarios on o.Id equals a.Operario
                                   where o.FechaBaja == null && a.CodigoEmpresa == 1 && a.MRH_CodigoAplicacion >= 7 // Solo operarios activos con permisos >= 7
                                   select new OperarioDisponibleDto
                                   {
                                       Id = o.Id,
                                       Nombre = o.Nombre ?? "Sin nombre",
                                       CodigoCentro = o.CodigoCentro,
                                       Permisos = string.Empty, // Se llenará después
                                       Empresas = string.Empty, // Se llenará después
                                       Almacenes = string.Empty // Se llenará después
                                   })
                                 .Distinct()
                                 .OrderBy(o => o.Nombre)
                                 .ToListAsync();

            // Llenar listas para cada operario
            foreach (var operario in operarios)
            {
                // Permisos SGA (tabla MRH_accesosOperariosSGA) - solo códigos por ahora
                var permisos = await _context.Database.SqlQueryRaw<string>(
                    "SELECT CAST(MRH_CodigoAplicacion AS VARCHAR) FROM MRH_accesosOperariosSGA WHERE Operario = {0} ORDER BY MRH_CodigoAplicacion", operario.Id)
                    .ToListAsync();
                operario.Permisos = string.Join(", ", permisos);

                // Contar solo permisos >= 7
                var permisosFiltrados = permisos.Where(p => int.TryParse(p, out int codigo) && codigo >= 7).ToList();
                operario.CantidadPermisos = permisosFiltrados.Count;

                // Empresas (tabla MRH_SGAOperariosEmpresas) - solo nombres
                var empresas = await _context.Database.SqlQueryRaw<string>(
                    "SELECT Empresa FROM MRH_SGAOperariosEmpresas WHERE Operario = {0} ORDER BY Empresa", operario.Id)
                    .ToListAsync();
                operario.Empresas = string.Join(", ", empresas);

                // Almacenes (tabla MRH_OperariosAlmacenes) - solo códigos por ahora
                var almacenes = await _context.Database.SqlQueryRaw<string>(
                    "SELECT CodigoAlmacen FROM MRH_OperariosAlmacenes WHERE Operario = {0} ORDER BY CodigoAlmacen", operario.Id)
                    .ToListAsync();
                operario.Almacenes = string.Join(", ", almacenes);
                operario.CantidadAlmacenes = almacenes.Count;

                // Plantilla aplicada (tabla OperariosConfiguracionesAplicadas)
                var plantillaAplicada = await _auroraContext.OperariosConfiguracionesAplicadas
                    .Where(oca => oca.OperarioId == operario.Id && oca.Activa)
                    .Select(oca => oca.ConfiguracionPredefinidaNombre)
                    .FirstOrDefaultAsync();
                operario.PlantillaAplicada = plantillaAplicada;
            }

            return Ok(operarios);
        }

        /// <summary>
        /// Obtiene lista de todos los operarios con información básica
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<OperarioListaDto>>> GetOperarios()
        {
            // Primero obtener los operarios básicos (todos los activos, no solo los que ya tienen permisos SGA)
            var operarios = await _context.Operarios
                .Where(o => o.FechaBaja == null) // Solo operarios activos
                .Select(o => new OperarioListaDto
                {
                    Id = o.Id, // El Id está mapeado a la columna Operario de la BD
                    Nombre = o.Nombre,
                    CodigoCentro = o.CodigoCentro,
                    Activo = o.FechaBaja == null,
                    Permisos = string.Empty, // Se llenará después
                    Empresas = string.Empty, // Se llenará después
                    Almacenes = string.Empty // Se llenará después
                })
                .OrderBy(o => o.Nombre)
                .ToListAsync();

            // Llenar listas para cada operario
            foreach (var operario in operarios)
            {
                // Permisos SGA (tabla MRH_accesosOperariosSGA) - solo códigos por ahora
                var permisos = await _context.Database.SqlQueryRaw<string>(
                    "SELECT CAST(MRH_CodigoAplicacion AS VARCHAR) FROM MRH_accesosOperariosSGA WHERE Operario = {0} ORDER BY MRH_CodigoAplicacion", operario.Id)
                    .ToListAsync();
                operario.Permisos = string.Join(", ", permisos);

                // Contar solo permisos >= 7
                var permisosFiltrados = permisos.Where(p => int.TryParse(p, out int codigo) && codigo >= 7).ToList();
                operario.CantidadPermisos = permisosFiltrados.Count;

                // Empresas (tabla MRH_SGAOperariosEmpresas) - solo nombres
                var empresas = await _context.Database.SqlQueryRaw<string>(
                    "SELECT Empresa FROM MRH_SGAOperariosEmpresas WHERE Operario = {0} ORDER BY Empresa", operario.Id)
                    .ToListAsync();
                operario.Empresas = string.Join(", ", empresas);

                // Almacenes (tabla MRH_OperariosAlmacenes) - solo códigos por ahora
                var almacenes = await _context.Database.SqlQueryRaw<string>(
                    "SELECT CodigoAlmacen FROM MRH_OperariosAlmacenes WHERE Operario = {0} ORDER BY CodigoAlmacen", operario.Id)
                    .ToListAsync();
                operario.Almacenes = string.Join(", ", almacenes);
                operario.CantidadAlmacenes = almacenes.Count;

                // Plantilla aplicada (tabla OperariosConfiguracionesAplicadas)
                var plantillaAplicada = await _auroraContext.OperariosConfiguracionesAplicadas
                    .Where(oca => oca.OperarioId == operario.Id && oca.Activa)
                    .OrderByDescending(oca => oca.FechaAplicacion)
                    .Select(oca => oca.ConfiguracionPredefinidaNombre)
                    .FirstOrDefaultAsync();
                operario.PlantillaAplicada = plantillaAplicada;
            }

            return Ok(operarios);
        }

        /// <summary>
        /// Endpoint temporal para verificar si un operario específico está en la lista
        /// </summary>
        [HttpGet("debug-operario/{operarioId}")]
        public async Task<ActionResult> DebugOperario(int operarioId)
        {
            // Verificar si el operario existe en la tabla Operarios
            var operario = await _context.Operarios
                .Where(o => o.Id == operarioId && o.FechaBaja == null)
                .Select(o => new { o.Id, o.Nombre, o.FechaBaja })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                OperarioId = operarioId,
                Existe = operario != null,
                Operario = operario
            });
        }

        /// <summary>
        /// Endpoint temporal para debuggear contadores
        /// </summary>
        [HttpGet("debug-contadores/{operarioId}")]
        public async Task<ActionResult> DebugContadores(int operarioId)
        {
            // Consulta SQL directa para verificar
            var permisosSQL = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) FROM MRH_accesosOperariosSGA WHERE Operario = {0}", operarioId)
                .FirstOrDefaultAsync();

            var empresasSQL = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) FROM MRH_SGAOperariosEmpresas WHERE Operario = {0}", operarioId)
                .FirstOrDefaultAsync();

            var almacenesSQL = await _context.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) FROM MRH_OperariosAlmacenes WHERE Operario = {0}", operarioId)
                .FirstOrDefaultAsync();

            // También probar con Entity Framework
            var permisosEF = await _context.AccesosOperarios
                .Where(a => a.Operario == operarioId)
                .ToListAsync();

            // Obtener los códigos de permisos específicos
            var permisosCodigos = await _context.Database.SqlQueryRaw<string>(
                "SELECT CAST(MRH_CodigoAplicacion AS VARCHAR) FROM MRH_accesosOperariosSGA WHERE Operario = {0} ORDER BY MRH_CodigoAplicacion", operarioId)
                .ToListAsync();

            // Filtrar permisos >= 7
            var permisosFiltrados = permisosCodigos.Where(p => int.TryParse(p, out int codigo) && codigo >= 7).ToList();

            var empresasEF = await _context.OperariosEmpresas
                .Where(e => e.Operario == operarioId)
                .ToListAsync();

            var almacenesEF = await _context.OperariosAlmacenes
                .Where(a => a.Operario == operarioId)
                .ToListAsync();

            return Ok(new
            {
                OperarioId = operarioId,
                SQL_Directo = new
                {
                    Permisos = permisosSQL,
                    Empresas = empresasSQL,
                    Almacenes = almacenesSQL
                },
                EntityFramework = new
                {
                    Permisos = permisosEF.Count,
                    Empresas = empresasEF.Count,
                    Almacenes = almacenesEF.Count
                },
                DetallePermisos = permisosEF.Select(p => new { p.CodigoEmpresa, p.MRH_CodigoAplicacion }),
                PermisosCodigos = permisosCodigos,
                PermisosFiltrados = permisosFiltrados,
                CantidadPermisosFiltrados = permisosFiltrados.Count
            });
        }

        /// <summary>
        /// Obtiene configuración completa de un operario específico
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<OperarioConfiguracionDto>> GetOperarioConfiguracion(int id)
        {
            try
            {
                var operario = await _context.Operarios.FindAsync(id);
                if (operario == null)
                    return NotFound($"Operario con ID {id} no encontrado");

                // Obtener permisos SGA (solo empresa 1 y códigos >= 10)
                var permisos = await _context.AccesosOperarios
                    .Where(a => a.Operario == id && a.CodigoEmpresa == 1 && a.MRH_CodigoAplicacion >= 10)
                    .Select(a => a.MRH_CodigoAplicacion)
                    .ToListAsync();

                // Obtener empresas
                var empresas = await _context.OperariosEmpresas
                    .Where(e => e.Operario == id)
                    .Select(e => new EmpresaOperarioDto
                    {
                        CodigoEmpresa = e.CodigoEmpresa,
                        EmpresaOrigen = e.EmpresaOrigen,
                        Empresa = e.Empresa
                    })
                    .ToListAsync();

                // Obtener almacenes con descripción y nombre de empresa
                var almacenes = await (from oa in _context.OperariosAlmacenes
                                       join a in _context.Almacenes on new { oa.CodigoEmpresa, oa.CodigoAlmacen }
                                           equals new { CodigoEmpresa = (short)a.CodigoEmpresa, a.CodigoAlmacen }
                                       join e in _context.Empresas on oa.CodigoEmpresa equals e.CodigoEmpresa
                                       where oa.Operario == id && oa.CodigoEmpresa == 1
                                       select new AlmacenOperarioDto
                                       {
                                           CodigoEmpresa = oa.CodigoEmpresa,
                                           CodigoAlmacen = oa.CodigoAlmacen ?? "",
                                           DescripcionAlmacen = a.Almacen ?? "",
                                           NombreEmpresa = e.EmpresaNombre ?? ""
                                       }).ToListAsync();

                var configuracion = new OperarioConfiguracionDto
                {
                    Id = operario.Id,
                    Nombre = operario.Nombre,
                    Contraseña = operario.Contraseña,
                    FechaBaja = operario.FechaBaja,
                    CodigoCentro = operario.CodigoCentro,
                    LimiteInventarioEuros = operario.MRH_LimiteInventarioEuros,
                    LimiteInventarioUnidades = operario.MRH_LimiteInventarioUnidades,
                    Permisos = permisos,
                    Empresas = empresas,
                    Almacenes = almacenes
                };

                return Ok(configuracion);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener configuración del operario: {ex.Message}");
            }
        }

        /// <summary>
        /// Actualiza la configuración completa de un operario
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOperarioConfiguracion(int id, [FromBody] OperarioUpdateDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var operario = await _context.Operarios.FindAsync(id);
                if (operario == null)
                    return NotFound($"Operario con ID {id} no encontrado");

                // ⚠️ IMPORTANTE: Solo actualizar estos dos campos de la tabla operarios
                // NO tocar ningún otro campo para evitar romper el ERP
                // Usar EF Core directamente para evitar problemas de transacción
                if (dto.LimiteInventarioEuros.HasValue)
                {
                    operario.MRH_LimiteInventarioEuros = dto.LimiteInventarioEuros.Value;
                }
                if (dto.LimiteInventarioUnidades.HasValue)
                {
                    operario.MRH_LimiteInventarioUnidades = dto.LimiteInventarioUnidades.Value;
                }

                // Gestionar permisos
                await GestionarPermisos(id, dto.PermisosAsignar, dto.PermisosQuitar);

                // Gestionar empresas
                await GestionarEmpresas(id, dto.EmpresasAsignar, dto.EmpresasQuitar);

                // Gestionar almacenes
                await GestionarAlmacenes(id, dto.AlmacenesAsignar, dto.AlmacenesQuitar);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok("Configuración actualizada correctamente");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"Error al actualizar configuración: {ex.Message}");
            }
        }


        /// <summary>
        /// Obtiene todos los permisos disponibles
        /// </summary>
        [HttpGet("permisos-disponibles")]
        public async Task<ActionResult<List<PermisoDisponibleDto>>> GetPermisosDisponibles()
        {
            try
            {
                var permisos = await _context.AplicacionesSGA
                    .Where(a => a.CodigoEmpresa == 1 && a.MRH_CodigoAplicacion >= 10) // Solo empresa 1 y códigos >= 10
                    .Select(a => new PermisoDisponibleDto
                    {
                        Codigo = a.MRH_CodigoAplicacion,
                        Descripcion = a.Descripcion
                    })
                    .OrderBy(a => a.Codigo)
                    .ToListAsync();

                return Ok(permisos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene las empresas asignadas a un operario específico con información del operario
        /// </summary>
        [HttpGet("{operarioId}/empresas")]
        public async Task<ActionResult<EmpresasOperarioResponseDto>> GetEmpresasOperario(int operarioId)
        {
            try
            {
                // Obtener información del operario
                var operario = await _context.Operarios
                    .Where(o => o.Id == operarioId)
                    .Select(o => new { o.Id, o.Nombre, o.CodigoCentro })
                    .FirstOrDefaultAsync();

                if (operario == null)
                    return NotFound($"Operario con ID {operarioId} no encontrado");

                // Obtener empresas asignadas
                var empresas = await _context.OperariosEmpresas
                    .Where(e => e.Operario == operarioId)
                    .Select(e => new EmpresaOperarioDto
                    {
                        CodigoEmpresa = e.CodigoEmpresa,
                        EmpresaOrigen = e.EmpresaOrigen,
                        Empresa = e.Empresa
                    })
                    .OrderBy(e => e.Empresa)
                    .ToListAsync();

                var response = new EmpresasOperarioResponseDto
                {
                    OperarioId = operario.Id,
                    OperarioNombre = operario.Nombre ?? "Sin nombre",
                    CodigoCentro = operario.CodigoCentro,
                    Empresas = empresas
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener empresas del operario {operarioId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Asigna una empresa a un operario
        /// </summary>
        [HttpPost("{operarioId}/empresas")]
        public async Task<IActionResult> AsignarEmpresaOperario(int operarioId, [FromBody] AsignarEmpresaDto dto)
        {
            try
            {
                // Verificar si el operario existe
                var operarioExiste = await _context.Operarios.AnyAsync(o => o.Id == operarioId);
                if (!operarioExiste)
                    return NotFound($"Operario con ID {operarioId} no encontrado");

                // Buscar la empresa en la tabla Empresas para obtener el nombre
                var empresa = await _context.Empresas
                    .FirstOrDefaultAsync(e => e.CodigoEmpresa == dto.CodigoEmpresa);

                if (empresa == null)
                    return NotFound($"Empresa con código {dto.CodigoEmpresa} no encontrada");

                // Verificar si ya existe la asignación
                var existeAsignacion = await _context.OperariosEmpresas
                    .AnyAsync(oe => oe.Operario == operarioId &&
                                   oe.CodigoEmpresa == dto.CodigoEmpresa &&
                                   oe.EmpresaOrigen == dto.EmpresaOrigen);

                if (existeAsignacion)
                    return Conflict("La empresa ya está asignada al operario");

                // Verificar si el operario ya tiene acceso SGA, si no, dárselo automáticamente
                var tieneAcceso = await _context.AccesosOperarios
                    .AnyAsync(a => a.Operario == operarioId);

                if (!tieneAcceso)
                {
                    var nuevoAcceso = new AccesoOperario
                    {
                        CodigoEmpresa = 1, // Siempre empresa 1 para permisos SGA
                        Operario = operarioId,
                        MRH_CodigoAplicacion = 7 // Código para SGA
                    };
                    _context.AccesosOperarios.Add(nuevoAcceso);
                }

                // Crear nueva asignación con el nombre obtenido de la tabla Empresas
                var nuevaAsignacion = new OperarioEmpresa
                {
                    Operario = operarioId,
                    CodigoEmpresa = dto.CodigoEmpresa,
                    EmpresaOrigen = dto.EmpresaOrigen,
                    Empresa = empresa.EmpresaNombre  // Tomar el nombre de la tabla Empresas
                };

                _context.OperariosEmpresas.Add(nuevaAsignacion);
                await _context.SaveChangesAsync();

                return Ok("Empresa asignada correctamente al operario");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al asignar empresa: {ex.Message}");
            }
        }

        /// <summary>
        /// Elimina la asignación de una empresa a un operario
        /// </summary>
        [HttpDelete("{operarioId}/empresas/{codigoEmpresa}/{empresaOrigen}")]
        public async Task<IActionResult> EliminarEmpresaOperario(int operarioId, short codigoEmpresa, short empresaOrigen)
        {
            try
            {
                var asignacion = await _context.OperariosEmpresas
                    .FirstOrDefaultAsync(oe => oe.Operario == operarioId &&
                                              oe.CodigoEmpresa == codigoEmpresa &&
                                              oe.EmpresaOrigen == empresaOrigen);

                if (asignacion == null)
                    return NotFound("Asignación de empresa no encontrada");

                _context.OperariosEmpresas.Remove(asignacion);
                await _context.SaveChangesAsync();

                return Ok("Empresa desasignada correctamente del operario");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al eliminar asignación de empresa: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene las empresas disponibles (solo códigos 1, 3 y 999)
        /// </summary>
        [HttpGet("empresas-disponibles")]
        public async Task<ActionResult<List<EmpresaConfiguracionDto>>> GetEmpresasDisponibles()
        {
            try
            {
                // Usar consultas individuales para evitar problemas con OPENJSON
                var empresas = new List<EmpresaConfiguracionDto>();

                // Buscar empresa con código 1
                var empresa1 = await _context.Empresas
                    .Where(e => e.CodigoEmpresa == 1)
                    .Select(e => new EmpresaConfiguracionDto
                    {
                        CodigoEmpresa = e.CodigoEmpresa,
                        Nombre = e.EmpresaNombre
                    })
                    .FirstOrDefaultAsync();

                if (empresa1 != null)
                    empresas.Add(empresa1);

                // Buscar empresa con código 3
                var empresa3 = await _context.Empresas
                    .Where(e => e.CodigoEmpresa == 3)
                    .Select(e => new EmpresaConfiguracionDto
                    {
                        CodigoEmpresa = e.CodigoEmpresa,
                        Nombre = e.EmpresaNombre
                    })
                    .FirstOrDefaultAsync();

                if (empresa3 != null)
                    empresas.Add(empresa3);

                // Buscar empresa con código 999
                var empresa999 = await _context.Empresas
                    .Where(e => e.CodigoEmpresa == 999)
                    .Select(e => new EmpresaConfiguracionDto
                    {
                        CodigoEmpresa = e.CodigoEmpresa,
                        Nombre = e.EmpresaNombre
                    })
                    .FirstOrDefaultAsync();

                if (empresa999 != null)
                    empresas.Add(empresa999);

                return Ok(empresas.OrderBy(e => e.CodigoEmpresa).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene todos los almacenes disponibles por empresa
        /// </summary>
        [HttpGet("almacenes-disponibles/{codigoEmpresa}")]
        public async Task<ActionResult<List<AlmacenConfiguracionDto>>> GetAlmacenesDisponibles(short codigoEmpresa)
        {
            var almacenes = await (from a in _context.Almacenes
                                   join e in _context.Empresas on a.CodigoEmpresa equals e.CodigoEmpresa
                                   where a.CodigoEmpresa == codigoEmpresa
                                   select new AlmacenConfiguracionDto
                                   {
                                       CodigoEmpresa = a.CodigoEmpresa ?? 0,
                                       CodigoAlmacen = a.CodigoAlmacen ?? "",
                                       Descripcion = a.Almacen,
                                       NombreEmpresa = e.EmpresaNombre ?? ""
                                   })
                                 .OrderBy(a => a.Descripcion)
                                 .ToListAsync();

            return Ok(almacenes);
        }

        #region Métodos privados

        private async Task GestionarPermisos(int operarioId, List<short> asignar, List<short> quitar)
        {
            // Quitar permisos - usar consultas individuales para evitar OPENJSON
            foreach (var permisoId in quitar)
            {
                var permisosAQuitar = await _context.AccesosOperarios
                    .Where(a => a.Operario == operarioId && a.MRH_CodigoAplicacion == permisoId && a.CodigoEmpresa == 1)
                    .ToListAsync();

                _context.AccesosOperarios.RemoveRange(permisosAQuitar);
            }

            // Asignar nuevos permisos (evitar duplicados)
            var permisosExistentes = await _context.AccesosOperarios
                .Where(a => a.Operario == operarioId && a.CodigoEmpresa == 1)
                .Select(a => a.MRH_CodigoAplicacion)
                .ToListAsync();

            var nuevosPermisos = asignar
                .Where(p => !permisosExistentes.Contains(p))
                .Select(p => new AccesoOperario
                {
                    CodigoEmpresa = 1, // Siempre empresa 1 para permisos SGA
                    Operario = operarioId,
                    MRH_CodigoAplicacion = p
                });

            _context.AccesosOperarios.AddRange(nuevosPermisos);
        }

        private async Task GestionarEmpresas(int operarioId, List<EmpresaOperarioDto> asignar, List<short> quitar)
        {
            // Quitar empresas - usar consultas individuales para evitar OPENJSON
            foreach (var empresaOrigen in quitar)
            {
                var empresasAQuitar = await _context.OperariosEmpresas
                    .Where(e => e.Operario == operarioId && e.EmpresaOrigen == empresaOrigen)
                    .ToListAsync();

                _context.OperariosEmpresas.RemoveRange(empresasAQuitar);
            }

            // Asignar nuevas empresas
            var empresasExistentes = await _context.OperariosEmpresas
                .Where(e => e.Operario == operarioId)
                .Select(e => e.EmpresaOrigen)
                .ToListAsync();

            var nuevasEmpresas = asignar
                .Where(e => !empresasExistentes.Contains(e.EmpresaOrigen))
                .Select(e => new OperarioEmpresa
                {
                    Operario = operarioId,
                    CodigoEmpresa = e.CodigoEmpresa,
                    EmpresaOrigen = e.EmpresaOrigen,
                    Empresa = e.Empresa
                });

            _context.OperariosEmpresas.AddRange(nuevasEmpresas);
        }

        private async Task GestionarAlmacenes(int operarioId, List<AlmacenOperarioDto> asignar, List<string> quitar)
        {
            // Quitar almacenes - usar consultas individuales para evitar OPENJSON
            foreach (var almacenId in quitar)
            {
                var almacenesAQuitar = await _context.OperariosAlmacenes
                    .Where(a => a.Operario == operarioId && (a.CodigoAlmacen ?? "") == almacenId)
                    .ToListAsync();

                _context.OperariosAlmacenes.RemoveRange(almacenesAQuitar);
            }

            // Asignar nuevos almacenes - como estamos quitando todos y asignando todos,
            // no necesitamos verificar duplicados
            var nuevosAlmacenes = asignar
                .Select(a => new OperarioAlmacen
                {
                    Operario = operarioId,
                    CodigoEmpresa = 1, // Siempre 1 para SGA
                    CodigoAlmacen = a.CodigoAlmacen
                });

            _context.OperariosAlmacenes.AddRange(nuevosAlmacenes);
        }

        #endregion

        // DTOs auxiliares para las consultas
        public class EmpresaConfiguracionDto
        {
            public short CodigoEmpresa { get; set; }
            public string Nombre { get; set; } = string.Empty;
        }

        public class AlmacenConfiguracionDto
        {
            public short CodigoEmpresa { get; set; }
            public string CodigoAlmacen { get; set; } = string.Empty;
            public string? Descripcion { get; set; }
            public string NombreEmpresa { get; set; } = string.Empty;

            // Propiedad calculada para mostrar en el formato "Código - Descripción (Nombre Empresa)"
            public string DisplayText => $"{CodigoAlmacen} - {Descripcion} ({NombreEmpresa})";
        }

        /// <summary>
        /// DTO para asignar una empresa a un operario
        /// </summary>
        public class AsignarEmpresaDto
        {
            public short CodigoEmpresa { get; set; }
            public short EmpresaOrigen { get; set; }
            // El nombre de la empresa se obtiene automáticamente de la tabla Empresas
        }


        /// <summary>
        /// Guarda la aplicación de una plantilla a un operario
        /// </summary>
        [HttpPost("aplicar-plantilla")]
        public async Task<ActionResult> AplicarPlantilla([FromBody] AplicarPlantillaDto dto)
        {
            try
            {
                // Desactivar plantillas anteriores del operario
                var plantillasAnteriores = await _auroraContext.OperariosConfiguracionesAplicadas
                    .Where(oca => oca.OperarioId == dto.OperarioId && oca.Activa)
                    .ToListAsync();

                foreach (var plantilla in plantillasAnteriores)
                {
                    plantilla.Activa = false;
                }

                // Crear nueva entrada
                var nuevaAplicacion = new OperarioConfiguracionAplicada
                {
                    OperarioId = dto.OperarioId,
                    ConfiguracionPredefinidaId = dto.ConfiguracionPredefinidaId,
                    ConfiguracionPredefinidaNombre = dto.ConfiguracionPredefinidaNombre,
                    FechaAplicacion = DateTime.Now,
                    UsuarioAplicacion = dto.UsuarioAplicacion,
                    Activa = true
                };

                _auroraContext.OperariosConfiguracionesAplicadas.Add(nuevaAplicacion);
                await _auroraContext.SaveChangesAsync();

                return Ok(new { message = "Plantilla aplicada correctamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error al aplicar plantilla: {ex.Message}" });
            }
        }

        /// <summary>
        /// DTO para aplicar plantilla
        /// </summary>
        public class AplicarPlantillaDto
        {
            public int OperarioId { get; set; }
            public int ConfiguracionPredefinidaId { get; set; }
            public string ConfiguracionPredefinidaNombre { get; set; } = string.Empty;
            public int UsuarioAplicacion { get; set; }
        }

        /// <summary>
        /// DTO de respuesta que incluye información del operario y sus empresas
        /// </summary>
        public class EmpresasOperarioResponseDto
        {
            public int OperarioId { get; set; }
            public string OperarioNombre { get; set; } = string.Empty;
            public string? CodigoCentro { get; set; }
            public List<EmpresaOperarioDto> Empresas { get; set; } = new List<EmpresaOperarioDto>();
        }
    }
}