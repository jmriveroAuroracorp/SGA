using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;
using SGA_Api.Models.Login;
using SGA_Api.Services;

namespace SGA_Api.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class OperariosConfiguracionController : ControllerBase
    {
        private readonly SageDbContext _context;
        private readonly AuroraSgaDbContext _auroraContext;
        private readonly IRolesSgaService _rolesSgaService;

        public OperariosConfiguracionController(SageDbContext context, AuroraSgaDbContext auroraContext, IRolesSgaService rolesSgaService)
        {
            _context = context;
            _auroraContext = auroraContext;
            _rolesSgaService = rolesSgaService;
        }

        /// <summary>
        /// Obtiene lista de operarios que tienen acceso al SGA
        /// </summary>
        [HttpGet("disponibles")]
        public async Task<ActionResult<List<OperarioDisponibleDto>>> GetOperariosDisponibles([FromQuery] bool? soloActivos = true)
        {
            // Construir la consulta base
            var query = from o in _context.Operarios
                        join a in _context.AccesosOperarios on o.Id equals a.Operario
                        where a.CodigoEmpresa == 1 && a.MRH_CodigoAplicacion >= 7 // Operarios con permisos >= 7
                        select o;

            // Aplicar filtro por estado según el parámetro
            if (soloActivos.HasValue)
            {
                if (soloActivos.Value)
                {
                    // Solo operarios activos (FechaBaja == null)
                    query = query.Where(o => o.FechaBaja == null);
                }
                else
                {
                    // Solo operarios inactivos (FechaBaja != null)
                    query = query.Where(o => o.FechaBaja != null);
                }
            }
            // Si soloActivos es null, no aplicar filtro de estado (mostrar todos)

            // Ejecutar la consulta
            var operarios = await query
                .Select(o => new OperarioDisponibleDto
                {
                    Id = o.Id,
                    Nombre = o.Nombre ?? "Sin nombre",
                    CodigoCentro = o.CodigoCentro,
                    FechaBaja = o.FechaBaja,
                    Activo = o.FechaBaja == null,
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
                    @"SELECT e.Empresa 
                      FROM MRH_SGAOperariosEmpresas oe 
                      INNER JOIN Empresas e ON e.CodigoEmpresa = oe.EmpresaOrigen 
                      WHERE oe.Operario = {0} 
                      ORDER BY e.Empresa", operario.Id)
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

                // Información del rol SGA (tabla Usuarios)
                var usuario = await _auroraContext.Usuarios
                    .Where(u => u.IdUsuario == operario.Id)
                    .Select(u => new { u.IdRol })
                    .FirstOrDefaultAsync();
                
                if (usuario?.IdRol.HasValue == true)
                {
                    // Obtener información del rol
                    var rolInfo = await ObtenerInformacionRolAsync(usuario.IdRol.Value);
                    if (rolInfo != null)
                    {
                        operario.RolNombre = rolInfo.Nombre;
                        operario.NivelJerarquico = rolInfo.NivelJerarquico;
                    }
                }
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
                    Almacenes = string.Empty, // Se llenará después
                    LimiteImporte = o.MRH_LimiteInventarioEuros,
                    LimiteUnidades = o.MRH_LimiteInventarioUnidades
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
                    @"SELECT e.Empresa 
                      FROM MRH_SGAOperariosEmpresas oe 
                      INNER JOIN Empresas e ON e.CodigoEmpresa = oe.EmpresaOrigen 
                      WHERE oe.Operario = {0} 
                      ORDER BY e.Empresa", operario.Id)
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

                // Información del rol SGA (tabla Usuarios)
                var usuario = await _auroraContext.Usuarios
                    .Where(u => u.IdUsuario == operario.Id)
                    .Select(u => new { u.IdUsuario, u.IdRol })
                    .FirstOrDefaultAsync();
                
                if (usuario?.IdRol.HasValue == true)
                {
                    // Obtener información del rol
                    var rolInfo = await ObtenerInformacionRolAsync(usuario.IdRol.Value);
                    if (rolInfo != null)
                    {
                        operario.RolNombre = rolInfo.Nombre;
                        operario.NivelJerarquico = rolInfo.NivelJerarquico;
                    }
                }

                // Calcular resumen de límites
                
                if (operario.LimiteImporte.HasValue && operario.LimiteImporte.Value > 0.0000000m)
                {
                    operario.LimitesResumen = $"{operario.LimiteImporte.Value:C} / {operario.LimiteUnidades ?? 0:N0} unidades";
                }
                else
                {
                    operario.LimitesResumen = "Sin límites";
                }
                
                System.Diagnostics.Debug.WriteLine($"Operario {operario.Id}: LimitesResumen='{operario.LimitesResumen}'");
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

                // Obtener plantilla aplicada
                var plantillaAplicada = await _auroraContext.OperariosConfiguracionesAplicadas
                    .Where(oca => oca.OperarioId == id && oca.Activa)
                    .Select(oca => oca.ConfiguracionPredefinidaNombre)
                    .FirstOrDefaultAsync();

                // Obtener información del rol SGA
                var usuario = await _auroraContext.Usuarios
                    .Where(u => u.IdUsuario == id)
                    .Select(u => new { u.IdRol })
                    .FirstOrDefaultAsync();

                int? idRol = null;
                string? rolNombre = null;
                int? nivelJerarquico = null;

                if (usuario?.IdRol.HasValue == true)
                {
                    idRol = usuario.IdRol.Value;
                    var rolInfo = await ObtenerInformacionRolAsync(usuario.IdRol.Value);
                    if (rolInfo != null)
                    {
                        rolNombre = rolInfo.Nombre;
                        nivelJerarquico = rolInfo.NivelJerarquico;
                    }
                }

                var configuracion = new OperarioConfiguracionDto
                {
                    Id = operario.Id,
                    Nombre = operario.Nombre,
                    Contraseña = operario.Contraseña,
                    FechaBaja = operario.FechaBaja,
                    CodigoCentro = operario.CodigoCentro,
                    LimiteInventarioEuros = operario.MRH_LimiteInventarioEuros,
                    LimiteInventarioUnidades = operario.MRH_LimiteInventarioUnidades,
                    IdRol = idRol,
                    RolNombre = rolNombre,
                    NivelJerarquico = nivelJerarquico,
                    Permisos = permisos,
                    Empresas = empresas,
                    Almacenes = almacenes,
                    PlantillaAplicada = plantillaAplicada
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

                bool huboCambios = false;

                // ⚠️ IMPORTANTE: Solo actualizar estos dos campos de la tabla operarios
                // NO tocar ningún otro campo para evitar romper el ERP
                // Usar SQL directo para evitar problemas con triggers
                if (dto.LimiteInventarioEuros.HasValue && operario.MRH_LimiteInventarioEuros != dto.LimiteInventarioEuros.Value)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE operarios SET MRH_LimiteInventarioEuros = {0} WHERE Operario = {1}",
                        dto.LimiteInventarioEuros.Value, id);
                    huboCambios = true;
                }
                if (dto.LimiteInventarioUnidades.HasValue && operario.MRH_LimiteInventarioUnidades != dto.LimiteInventarioUnidades.Value)
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE operarios SET MRH_LimiteInventarioUnidades = {0} WHERE Operario = {1}",
                        dto.LimiteInventarioUnidades.Value, id);
                    huboCambios = true;
                }

                // Gestionar permisos y detectar cambios
                var cambiosPermisos = await GestionarPermisos(id, dto.PermisosAsignar, dto.PermisosQuitar);
                if (cambiosPermisos) huboCambios = true;

                // Gestionar empresas y detectar cambios
                var cambiosEmpresas = await GestionarEmpresas(id, dto.EmpresasAsignar, dto.EmpresasQuitar);
                if (cambiosEmpresas) huboCambios = true;

                // Gestionar almacenes y detectar cambios
                var cambiosAlmacenes = await GestionarAlmacenes(id, dto.AlmacenesAsignar, dto.AlmacenesQuitar);
                if (cambiosAlmacenes) huboCambios = true;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (huboCambios)
                {
                    return Ok(new { message = "Configuración actualizada correctamente", huboCambios = true });
                }
                else
                {
                    return Ok(new { message = "No se detectaron cambios en la configuración", huboCambios = false });
                }
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
                        EmpresaOrigen = e.CodigoEmpresa, // El código real de la empresa
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
                        EmpresaOrigen = e.CodigoEmpresa, // El código real de la empresa
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
                        EmpresaOrigen = e.CodigoEmpresa, // El código real de la empresa
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

        private async Task<bool> GestionarPermisos(int operarioId, List<short> asignar, List<short> quitar)
        {
            bool huboCambios = false;

            // Quitar permisos - usar consultas individuales para evitar OPENJSON
            // IMPORTANTE: Nunca quitar permisos < 10 porque son del ERP
            foreach (var permisoId in quitar.Where(p => p >= 10))
            {
                var permisosAQuitar = await _context.AccesosOperarios
                    .Where(a => a.Operario == operarioId && a.MRH_CodigoAplicacion == permisoId && a.CodigoEmpresa == 1)
                    .ToListAsync();

                if (permisosAQuitar.Any())
                {
                    _context.AccesosOperarios.RemoveRange(permisosAQuitar);
                    huboCambios = true;
                }
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
                })
                .ToList();

            if (nuevosPermisos.Any())
            {
                _context.AccesosOperarios.AddRange(nuevosPermisos);
                huboCambios = true;
            }

            return huboCambios;
        }

        private async Task<bool> GestionarEmpresas(int operarioId, List<EmpresaOperarioDto> asignar, List<short> quitar)
        {
            bool huboCambios = false;

            // Quitar empresas - usar consultas individuales para evitar OPENJSON
            foreach (var empresaOrigen in quitar)
            {
                var empresasAQuitar = await _context.OperariosEmpresas
                    .Where(e => e.Operario == operarioId && e.EmpresaOrigen == empresaOrigen)
                    .ToListAsync();

                if (empresasAQuitar.Any())
                {
                    _context.OperariosEmpresas.RemoveRange(empresasAQuitar);
                    huboCambios = true;
                }
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
                })
                .ToList();

            if (nuevasEmpresas.Any())
            {
                _context.OperariosEmpresas.AddRange(nuevasEmpresas);
                huboCambios = true;
            }

            return huboCambios;
        }

        private async Task<bool> GestionarAlmacenes(int operarioId, List<AlmacenOperarioDto> asignar, List<string> quitar)
        {
            bool huboCambios = false;

            // Obtener almacenes actuales del operario
            var almacenesActuales = await _context.OperariosAlmacenes
                .Where(a => a.Operario == operarioId)
                .Select(a => a.CodigoAlmacen ?? "")
                .ToListAsync();

            // Obtener códigos de almacenes deseados
            var almacenesDeseados = asignar
                .Select(a => a.CodigoAlmacen)
                .ToList();

            // Calcular qué almacenes quitar (están actuales pero no deseados)
            var almacenesAQuitar = almacenesActuales
                .Where(actual => !almacenesDeseados.Contains(actual))
                .ToList();

            // Calcular qué almacenes agregar (están deseados pero no actuales)
            var almacenesAAgregar = almacenesDeseados
                .Where(deseado => !almacenesActuales.Contains(deseado))
                .ToList();

            // Quitar almacenes que ya no son necesarios
            if (almacenesAQuitar.Any())
            {
                // Usar consultas individuales para evitar problemas con Contains
                foreach (var codigoAlmacen in almacenesAQuitar)
                {
                    var almacenesParaEliminar = await _context.OperariosAlmacenes
                        .Where(a => a.Operario == operarioId && a.CodigoAlmacen == codigoAlmacen)
                        .ToListAsync();

                    if (almacenesParaEliminar.Any())
                    {
                        _context.OperariosAlmacenes.RemoveRange(almacenesParaEliminar);
                        huboCambios = true;
                    }
                }
            }

            // Agregar solo los almacenes nuevos
            if (almacenesAAgregar.Any())
            {
                var nuevosAlmacenes = almacenesAAgregar
                    .Select(codigoAlmacen => new OperarioAlmacen
                    {
                        Operario = operarioId,
                        CodigoEmpresa = 1, // Siempre 1 para SGA
                        CodigoAlmacen = codigoAlmacen
                    });

                _context.OperariosAlmacenes.AddRange(nuevosAlmacenes);
                huboCambios = true;
            }

            return huboCambios;
        }

        #endregion

        // DTOs auxiliares para las consultas
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

        /// <summary>
        /// Da de baja a un operario estableciendo su FechaBaja
        /// </summary>
        [HttpPost("{operarioId}/dar-de-baja")]
        public async Task<IActionResult> DarDeBajaOperario(int operarioId)
        {
            try
            {
                // Verificar que el operario existe
                var operario = await _context.Operarios
                    .Where(o => o.Id == operarioId)
                    .Select(o => new { o.Id, o.Nombre })
                    .FirstOrDefaultAsync();
                    
                if (operario == null)
                    return NotFound($"Operario con ID {operarioId} no encontrado");

                // Usar SQL raw para evitar problemas con triggers
                var fechaBaja = DateTime.Now;
                var sql = "UPDATE operarios SET FechaBaja = @fechaBaja WHERE Operario = @operarioId";
                
                await _context.Database.ExecuteSqlRawAsync(sql, 
                    new Microsoft.Data.SqlClient.SqlParameter("@fechaBaja", fechaBaja),
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                // Quitar plantilla asociada (marcar como inactiva)
                await _auroraContext.Database.ExecuteSqlRawAsync(
                    "UPDATE OperariosConfiguracionesAplicadas SET Activa = 0 WHERE OperarioId = @operarioId",
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                // Eliminar permisos específicos (7, 8, 9) pero mantener básicos (1-6) y otros (10+)
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM MRH_accesosOperariosSGA WHERE Operario = @operarioId AND MRH_CodigoAplicacion IN (7, 8, 9)",
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                // Eliminar todas las empresas asociadas
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM MRH_SGAOperariosEmpresas WHERE Operario = @operarioId",
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                // Eliminar todos los almacenes asociados
                await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM MRH_OperariosAlmacenes WHERE Operario = @operarioId",
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                // Eliminar límites del operario (poner en 0.0000000)
                await _context.Database.ExecuteSqlRawAsync(
                    "UPDATE operarios SET MRH_LimiteInventarioEuros = 0.0000000, MRH_LimiteInventarioUnidades = 0.0000000 WHERE Operario = @operarioId",
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                // Asegurar que tenga el permiso básico 10 (insertar si no existe)
                await _context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT 1 FROM MRH_accesosOperariosSGA WHERE Operario = @operarioId AND MRH_CodigoAplicacion = 10) " +
                    "INSERT INTO MRH_accesosOperariosSGA (Operario, MRH_CodigoAplicacion, CodigoEmpresa) VALUES (@operarioId, 10, 1)",
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                return Ok(new { 
                    success = true, 
                    message = $"Operario {operario.Nombre} dado de baja correctamente. Se han eliminado permisos 7-9, plantilla, empresas, almacenes y límites, manteniendo permisos básicos (1-6) y SGA (10+).",
                    fechaBaja = fechaBaja
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    success = false, 
                    message = $"Error al dar de baja al operario: {ex.Message}" 
                });
            }
        }

        /// <summary>
        /// Da de alta a un operario estableciendo su FechaBaja como null
        /// </summary>
        [HttpPost("{operarioId}/dar-de-alta")]
        public async Task<IActionResult> DarDeAltaOperario(int operarioId)
        {
            try
            {
                // Verificar que el operario existe
                var operario = await _context.Operarios
                    .Where(o => o.Id == operarioId)
                    .Select(o => new { o.Id, o.Nombre })
                    .FirstOrDefaultAsync();
                    
                if (operario == null)
                    return NotFound($"Operario con ID {operarioId} no encontrado");

                // Usar SQL raw para evitar problemas con triggers
                var sql = "UPDATE operarios SET FechaBaja = NULL WHERE Operario = @operarioId";
                
                await _context.Database.ExecuteSqlRawAsync(sql, 
                    new Microsoft.Data.SqlClient.SqlParameter("@operarioId", operarioId));

                return Ok(new { 
                    success = true, 
                    message = $"Operario {operario.Nombre} dado de alta correctamente",
                    fechaBaja = (DateTime?)null
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { 
                    success = false, 
                    message = $"Error al dar de alta al operario: {ex.Message}" 
                });
            }
        }

        /// <summary>
        /// Obtiene empleados disponibles para dar de alta en SGA:
        /// 1. Empleados de VAuxiliarEmpleado que NO están en operarios
        /// 2. Operarios que están en operarios pero NO tienen permisos SGA
        /// </summary>
        [HttpGet("empleados-disponibles")]
        public async Task<ActionResult<List<EmpleadoDisponibleDto>>> GetEmpleadosDisponibles()
        {
            try
            {
                var empleadosDisponibles = new List<EmpleadoDisponibleDto>();

                // 1. Empleados activos de VAuxiliarEmpleado que no están en operarios
                var empleadosVAuxiliar = await (from emp in _context.VAuxiliarEmpleados
                                               where emp.StatusActivo == 0 && emp.CodigoEmpleado >= 1000
                                               && !_context.Operarios.Any(op => op.Id == emp.CodigoEmpleado)
                                               select new EmpleadoDisponibleDto
                                               {
                                                   CodigoEmpleado = emp.CodigoEmpleado,
                                                   Nombre = emp.MRH_RazonSocialEmpleado,
                                                   Tipo = "Empleado"
                                               })
                                               .ToListAsync();

                // 2. Operarios que están en operarios pero NO tienen permisos SGA (>= 7)
                // Usar consulta más simple para evitar problemas con LINQ
                var operariosActivos = await _context.Operarios
                    .Where(op => op.FechaBaja == null)
                    .ToListAsync();

                var operariosSinPermisos = new List<EmpleadoDisponibleDto>();

                foreach (var op in operariosActivos)
                {
                    var tienePermisosSGA = await _context.AccesosOperarios
                        .AnyAsync(a => a.Operario == op.Id && a.CodigoEmpresa == 1 && a.MRH_CodigoAplicacion >= 7);

                    if (!tienePermisosSGA)
                    {
                        operariosSinPermisos.Add(new EmpleadoDisponibleDto
                        {
                            CodigoEmpleado = op.Id,
                            Nombre = op.Nombre ?? "Sin nombre",
                            Tipo = "Operario sin permisos"
                        });
                    }
                }

                // Combinar ambas listas
                empleadosDisponibles.AddRange(empleadosVAuxiliar);
                empleadosDisponibles.AddRange(operariosSinPermisos);

                // Ordenar por nombre
                empleadosDisponibles = empleadosDisponibles
                    .OrderBy(e => e.Nombre)
                    .ToList();

                return Ok(empleadosDisponibles);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener empleados disponibles: {ex.Message}");
            }
        }


        /// <summary>
        /// Da de alta un empleado/operario en el SGA:
        /// - Si es empleado de VAuxiliarEmpleado: lo crea en operarios
        /// - Si es operario sin permisos: solo le asigna permisos SGA
        /// </summary>
        [HttpPost("dar-alta-empleado")]
        public async Task<IActionResult> DarAltaEmpleado([FromBody] DarAltaEmpleadoDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // PRIMERO: Verificar si YA es un operario existente (prioridad)
                var operarioExistente = await _context.Operarios
                    .Where(o => o.Id == dto.CodigoEmpleado && o.FechaBaja == null)
                    .FirstOrDefaultAsync();

                // SEGUNDO: Verificar si es un empleado de VAuxiliarEmpleado
                var empleado = await _context.VAuxiliarEmpleados
                    .Where(e => e.CodigoEmpleado == dto.CodigoEmpleado && e.StatusActivo == 0)
                    .FirstOrDefaultAsync();

                // Debe existir al menos en una de las dos tablas
                if (empleado == null && operarioExistente == null)
                    return NotFound($"No se encontró empleado con código {dto.CodigoEmpleado} en VAuxiliarEmpleado ni operario activo en operarios");

                // Si NO es operario existente pero SÍ es empleado de VAuxiliar, crear en operarios
                if (operarioExistente == null && empleado != null)
                {
                    // Crear nuevo operario usando SQL raw para evitar problemas con triggers
                    var fechaAlta = DateTime.Now;
                    var contraseña = dto.Contraseña ?? dto.CodigoEmpleado.ToString();
                    
                    var sql = @"INSERT INTO operarios (CodigoEmpresa, Operario, NombreOperario, FechaAlta, Contraseña, CodigoCentro, FechaBaja, MRH_LimiteInventarioEuros, MRH_LimiteInventarioUnidades)
                               VALUES (1, @operarioId, @nombre, @fechaAlta, @contraseña, @codigoCentro, NULL, 0, 0)";

                    await _context.Database.ExecuteSqlRawAsync(sql,
                        new Microsoft.Data.SqlClient.SqlParameter("@operarioId", dto.CodigoEmpleado),
                        new Microsoft.Data.SqlClient.SqlParameter("@nombre", dto.Nombre),
                        new Microsoft.Data.SqlClient.SqlParameter("@fechaAlta", fechaAlta),
                        new Microsoft.Data.SqlClient.SqlParameter("@contraseña", contraseña),
                        new Microsoft.Data.SqlClient.SqlParameter("@codigoCentro", dto.CodigoCentro ?? ""));
                }
                // Si YA es operario existente, solo se le asignarán permisos/empresas/almacenes más adelante

                // Verificar y asignar permisos sin duplicar
                var permisosExistentes = await _context.AccesosOperarios
                    .Where(a => a.Operario == dto.CodigoEmpleado && a.CodigoEmpresa == 1)
                    .Select(a => a.MRH_CodigoAplicacion)
                    .ToListAsync();

                // Asignar permiso básico SGA (código 10) por defecto si no existe
                if (!permisosExistentes.Contains(10))
                {
                    var permisoBasico = new AccesoOperario
                    {
                        CodigoEmpresa = 1,
                        Operario = dto.CodigoEmpleado,
                        MRH_CodigoAplicacion = 10 // Permiso básico SGA
                    };
                    _context.AccesosOperarios.Add(permisoBasico);
                }

                // Asignar permisos adicionales si se especifican
                if (dto.PermisosIniciales.Any())
                {
                    foreach (var permiso in dto.PermisosIniciales)
                    {
                        // Solo agregar si no existe ya
                        if (!permisosExistentes.Contains(permiso))
                        {
                            var nuevoPermiso = new AccesoOperario
                            {
                                CodigoEmpresa = 1,
                                Operario = dto.CodigoEmpleado,
                                MRH_CodigoAplicacion = permiso
                            };
                            _context.AccesosOperarios.Add(nuevoPermiso);
                        }
                    }
                }

                // Verificar empresas existentes
                var empresasExistentes = await _context.OperariosEmpresas
                    .Where(oe => oe.Operario == dto.CodigoEmpleado)
                    .Select(oe => oe.EmpresaOrigen)
                    .ToListAsync();

                // Asignar empresas iniciales si se especifican
                if (dto.EmpresasIniciales.Any())
                {
                    foreach (var empresaId in dto.EmpresasIniciales)
                    {
                        // Solo agregar si no existe ya
                        if (!empresasExistentes.Contains(empresaId))
                        {
                            var empresa = await _context.Empresas
                                .Where(e => e.CodigoEmpresa == empresaId)
                                .FirstOrDefaultAsync();

                            if (empresa != null)
                            {
                                var nuevaEmpresa = new OperarioEmpresa
                                {
                                    Operario = dto.CodigoEmpleado,
                                    CodigoEmpresa = empresaId,
                                    EmpresaOrigen = empresaId,
                                    Empresa = empresa.EmpresaNombre ?? ""
                                };
                                _context.OperariosEmpresas.Add(nuevaEmpresa);
                            }
                        }
                    }
                }

                // Verificar almacenes existentes
                var almacenesExistentes = await _context.OperariosAlmacenes
                    .Where(oa => oa.Operario == dto.CodigoEmpleado && oa.CodigoEmpresa == 1)
                    .Select(oa => oa.CodigoAlmacen)
                    .ToListAsync();

                // Asignar almacenes iniciales si se especifican
                if (dto.AlmacenesIniciales.Any())
                {
                    foreach (var codigoAlmacen in dto.AlmacenesIniciales)
                    {
                        // Solo agregar si no existe ya
                        if (!almacenesExistentes.Contains(codigoAlmacen))
                        {
                            var nuevoAlmacen = new OperarioAlmacen
                            {
                                Operario = dto.CodigoEmpleado,
                                CodigoEmpresa = 1,
                                CodigoAlmacen = codigoAlmacen
                            };
                            _context.OperariosAlmacenes.Add(nuevoAlmacen);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Determinar qué tipo de operación se hizo
                string mensaje;
                if (operarioExistente != null)
                {
                    // Era un operario existente sin permisos, se le asignaron permisos
                    mensaje = $"Operario {dto.Nombre} configurado correctamente con acceso al SGA";
                }
                else
                {
                    // Era un empleado nuevo que se creó en operarios
                    mensaje = $"Empleado {dto.Nombre} dado de alta correctamente en el SGA";
                }

                return Ok(new { 
                    success = true, 
                    message = mensaje,
                    operarioId = dto.CodigoEmpleado
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { 
                    success = false, 
                    message = $"Error al dar de alta al empleado: {ex.Message}" 
                });
            }
        }

        /// <summary>
        /// Helper method para obtener información de un rol por ID
        /// </summary>
        private async Task<dynamic?> ObtenerInformacionRolAsync(int rolId)
        {
            // Usar el servicio de roles SGA para obtener la información del rol
            try
            {
                var rol = await _rolesSgaService.GetRolSgaByIdAsync(rolId);
                if (rol != null)
                {
                    return new { Id = rol.Id, Nombre = rol.Nombre, NivelJerarquico = rol.NivelJerarquico };
                }
            }
            catch (Exception ex)
            {
                // Log error silently
            }
            return null;
        }
    }
}