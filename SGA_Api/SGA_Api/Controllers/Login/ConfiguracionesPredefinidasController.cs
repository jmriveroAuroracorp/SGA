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
            
        // Primero obtener las configuraciones
        var configuraciones = await _auroraContext.ConfiguracionesPredefinidas
            .Where(c => c.Activa)
            .Include(c => c.Permisos)
            .Include(c => c.Empresas)
            .Include(c => c.Almacenes)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        // Luego calcular el contador de operarios para cada configuración
        var configuracionesConContador = new List<ConfiguracionPredefinidaListaDto>();
        foreach (var config in configuraciones)
        {
            var operariosUsando = await _auroraContext.OperariosConfiguracionesAplicadas
                .CountAsync(oca => oca.ConfiguracionPredefinidaId == config.Id && oca.Activa);

            configuracionesConContador.Add(new ConfiguracionPredefinidaListaDto
            {
                Id = config.Id,
                Nombre = config.Nombre,
                Descripcion = config.Descripcion,
                FechaCreacion = config.FechaCreacion,
                FechaModificacion = config.FechaModificacion,
                Activa = config.Activa,
                CantidadPermisos = config.Permisos.Count,
                CantidadEmpresas = config.Empresas.Count,
                CantidadAlmacenes = config.Almacenes.Count,
                LimiteEuros = config.LimiteEuros,
                LimiteUnidades = config.LimiteUnidades,
                OperariosUsando = operariosUsando
            });
        }

        // DEBUG: Log de configuraciones y sus contadores
        Console.WriteLine($"=== DEBUG: Configuraciones cargadas ===");
        foreach (var config in configuracionesConContador)
        {
            Console.WriteLine($"Configuración: {config.Nombre} (ID: {config.Id}) - Operarios usando: {config.OperariosUsando}");
        }
        
        // DEBUG: Verificar datos en OperariosConfiguracionesAplicadas
        var operariosAplicadas = await _auroraContext.OperariosConfiguracionesAplicadas
            .Where(oca => oca.Activa)
            .ToListAsync();
        Console.WriteLine($"=== DEBUG: Total operarios con plantillas aplicadas: {operariosAplicadas.Count}");
        foreach (var oca in operariosAplicadas)
        {
            Console.WriteLine($"Operario ID: {oca.OperarioId}, Config ID: {oca.ConfiguracionPredefinidaId}, Nombre: {oca.ConfiguracionPredefinidaNombre}, Activa: {oca.Activa}");
        }

        return Ok(configuracionesConContador);
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
                    CodigoEmpresa = 1, // Siempre 1 para SGA
                    EmpresaOrigen = e.EmpresaOrigen,
                    Nombre = _sageContext.Empresas
                        .Where(emp => emp.CodigoEmpresa == e.EmpresaOrigen)
                        .Select(emp => emp.EmpresaNombre)
                        .FirstOrDefault() ?? $"Empresa {e.EmpresaOrigen}"
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
                        CodigoEmpresa = 1, // Siempre 1 (empresa SGA)
                        EmpresaOrigen = empresa // El código real de la empresa
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
                        CodigoEmpresa = 1, // Siempre 1 (empresa SGA)
                        EmpresaOrigen = empresa // El código real de la empresa
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
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Buscar operarios que tienen esta plantilla aplicada (después de la transacción)
            var operariosIds = await _auroraContext.OperariosConfiguracionesAplicadas
                .Where(oca => oca.ConfiguracionPredefinidaId == id && oca.Activa)
                .Select(oca => new { oca.OperarioId, oca.ConfiguracionPredefinidaNombre })
                .ToListAsync();

            // Obtener nombres de operarios usando consultas separadas
            var operariosAfectados = new List<object>();
            foreach (var operario in operariosIds)
            {
                try
                {
                    // Intentar obtener el nombre del operario desde Sage
                    var operarioSage = await _sageContext.Operarios
                        .Where(o => o.Id == operario.OperarioId)
                        .Select(o => o.Nombre)
                        .FirstOrDefaultAsync();

                    var nombreOperario = !string.IsNullOrEmpty(operarioSage) 
                        ? operarioSage 
                        : $"Operario {operario.OperarioId}";

                    operariosAfectados.Add(new
                    {
                        OperarioId = operario.OperarioId,
                        OperarioNombre = nombreOperario,
                        ConfiguracionPredefinidaNombre = operario.ConfiguracionPredefinidaNombre
                    });
                }
                catch
                {
                    // Si hay error, usar nombre por defecto
                    operariosAfectados.Add(new
                    {
                        OperarioId = operario.OperarioId,
                        OperarioNombre = $"Operario {operario.OperarioId}",
                        ConfiguracionPredefinidaNombre = operario.ConfiguracionPredefinidaNombre
                    });
                }
            }

            // Si hay operarios afectados, devolver información para que el Desktop muestre el diálogo
            if (operariosAfectados.Any())
            {
                return Ok(new { 
                    success = true,
                    message = "Configuración actualizada correctamente",
                    OperariosAfectados = operariosAfectados
                });
            }

            return Ok(new { success = true, message = "Configuración actualizada correctamente" });
        }

        /// <summary>
        /// Verifica si una configuración tiene operarios asociados
        /// </summary>
        [HttpGet("{id}/operarios-asociados")]
        public async Task<ActionResult> GetOperariosAsociados(int id)
        {
            Console.WriteLine($"=== LLAMADA A GetOperariosAsociados con ID: {id} ===");
            try
            {
                var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                    .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

                if (configuracion == null)
                    return NotFound();

                // Usar consulta simple por ahora (sin nombres)
                var operariosAsociados = await _auroraContext.OperariosConfiguracionesAplicadas
                    .Where(oca => oca.ConfiguracionPredefinidaId == id && oca.Activa)
                    .Select(oca => new { 
                        OperarioId = oca.OperarioId, 
                        OperarioNombre = $"Operario {oca.OperarioId}", // Nombre temporal
                        ConfiguracionPredefinidaNombre = oca.ConfiguracionPredefinidaNombre 
                    })
                    .ToListAsync();

                // Debug: Log detallado de lo que devuelve la consulta
                Console.WriteLine($"=== DEBUG GetOperariosAsociados para configuración {id} ===");
                Console.WriteLine($"Operarios encontrados: {operariosAsociados.Count}");
                foreach (var op in operariosAsociados)
                {
                    Console.WriteLine($"  - OperarioId: {op.OperarioId}, OperarioNombre: '{op.OperarioNombre}', ConfiguracionNombre: '{op.ConfiguracionPredefinidaNombre}'");
                }
                Console.WriteLine("=== FIN DEBUG ===");

                return Ok(new 
                { 
                    tieneOperariosAsociados = operariosAsociados.Any(),
                    cantidadOperarios = operariosAsociados.Count,
                    OperariosAfectados = operariosAsociados,
                    Operarios = operariosAsociados.Select(o => new { 
                        OperarioId = o.OperarioId, 
                        OperarioNombre = o.OperarioNombre,
                        ConfiguracionPredefinidaNombre = o.ConfiguracionPredefinidaNombre 
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error al verificar operarios asociados: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración predefinida (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> EliminarConfiguracionPredefinida(int id)
        {
            try
            {
                var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                    .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

                if (configuracion == null)
                    return NotFound();

                // Verificar si hay operarios asociados a esta configuración
                var operariosAsociados = await _auroraContext.OperariosConfiguracionesAplicadas
                    .Where(oca => oca.ConfiguracionPredefinidaId == id && oca.Activa)
                    .Select(oca => new { oca.OperarioId, oca.ConfiguracionPredefinidaNombre })
                    .ToListAsync();

                // Si hay operarios asociados, desasociarlos automáticamente
                if (operariosAsociados.Any())
                {
                    foreach (var operarioAsociado in operariosAsociados)
                    {
                        var configuracionAplicada = await _auroraContext.OperariosConfiguracionesAplicadas
                            .FirstOrDefaultAsync(oca => oca.OperarioId == operarioAsociado.OperarioId && 
                                                       oca.ConfiguracionPredefinidaId == id && 
                                                       oca.Activa);
                        
                        if (configuracionAplicada != null)
                        {
                            configuracionAplicada.Activa = false;
                            configuracionAplicada.FechaAplicacion = DateTime.Now;
                        }
                    }
                }

                // Marcar la configuración como inactiva
                configuracion.Activa = false;
                configuracion.FechaModificacion = DateTime.Now;

                await _auroraContext.SaveChangesAsync();

                // Usar la vista vUsuariosConNombre para obtener operarios desasociados con nombres
                var operariosConNombres = await _auroraContext.Database.SqlQueryRaw<dynamic>(
                    @"SELECT 
                        oca.OperarioId,
                        ISNULL(v.NombreOperario, 'Operario ' + CAST(oca.OperarioId AS VARCHAR)) AS OperarioNombre,
                        oca.ConfiguracionPredefinidaNombre
                      FROM OperariosConfiguracionesAplicadas oca
                      LEFT JOIN vUsuariosConNombre v ON oca.OperarioId = v.UsuarioId
                      WHERE oca.ConfiguracionPredefinidaId = {0} AND oca.Activa = 0", id)
                    .ToListAsync();

                // Devolver información sobre los operarios que fueron desasociados
                return Ok(new 
                { 
                    success = true, 
                    message = "Configuración eliminada correctamente",
                    operariosDesasociados = operariosAsociados.Count,
                    OperariosAfectados = operariosConNombres
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
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
                    // IMPORTANTE: Nunca eliminar permisos < 10 porque son del ERP
                    var permisosActuales = await _sageContext.AccesosOperarios
                        .Where(a => a.Operario == dto.OperarioId && a.CodigoEmpresa == 1 && a.MRH_CodigoAplicacion >= 10)
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
                        EmpresaOrigen = empresa.EmpresaOrigen, // Usar el EmpresaOrigen de la plantilla
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

                // Guardar registro de plantilla aplicada
                var plantillaAplicada = new OperarioConfiguracionAplicada
                {
                    OperarioId = dto.OperarioId,
                    ConfiguracionPredefinidaId = dto.ConfiguracionId,
                    ConfiguracionPredefinidaNombre = configuracion.Nombre,
                    FechaAplicacion = DateTime.Now,
                    UsuarioAplicacion = 1, // TODO: Obtener del contexto de usuario
                    Activa = true
                };

                _auroraContext.OperariosConfiguracionesAplicadas.Add(plantillaAplicada);

                await _auroraContext.SaveChangesAsync();
                await _sageContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Configuración aplicada exitosamente" });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Aplica una plantilla actualizada a operarios específicos
        /// </summary>
        [HttpPost("{id}/aplicar-a-operarios")]
        public async Task<ActionResult> AplicarPlantillaAOperarios(int id, [FromBody] AplicarPlantillaAOperariosDto dto)
        {
            try
            {
                // Obtener la configuración predefinida
                var configuracion = await _auroraContext.ConfiguracionesPredefinidas
                    .Include(c => c.Permisos)
                    .Include(c => c.Empresas)
                    .Include(c => c.Almacenes)
                    .FirstOrDefaultAsync(c => c.Id == id && c.Activa);

                if (configuracion == null)
                    return NotFound("Configuración predefinida no encontrada");

                // Para cada operario seleccionado, aplicar la plantilla
                foreach (var operarioId in dto.OperarioIds)
                {
                    // Aplicar la plantilla al operario (permisos, empresas, almacenes, límites)
                    await AplicarPlantillaAOperario(operarioId, configuracion);
                    
                    // Actualizar la fecha de aplicación
                    var aplicacion = await _auroraContext.OperariosConfiguracionesAplicadas
                        .FirstOrDefaultAsync(oca => oca.OperarioId == operarioId && oca.ConfiguracionPredefinidaId == id && oca.Activa);

                    if (aplicacion != null)
                    {
                        aplicacion.FechaAplicacion = DateTime.Now;
                        aplicacion.UsuarioAplicacion = dto.UsuarioAplicacion;
                    }
                }

                await _auroraContext.SaveChangesAsync();
                await _sageContext.SaveChangesAsync();

                return Ok(new { message = $"Plantilla aplicada a {dto.OperarioIds.Count} operarios" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error al aplicar plantilla: {ex.Message}" });
            }
        }

        /// <summary>
        /// Aplica una plantilla predefinida a un operario específico
        /// </summary>
        private async Task AplicarPlantillaAOperario(int operarioId, ConfiguracionPredefinida configuracion)
        {
            // Obtener permisos actuales del operario
            var permisosActuales = await _sageContext.AccesosOperarios
                .Where(a => a.Operario == operarioId)
                .ToListAsync();

            // Obtener códigos de permisos de la plantilla
            var codigosPermisosPlantilla = configuracion.Permisos.Select(p => p.MRH_CodigoAplicacion).ToList();

            // Eliminar permisos que ya no están en la plantilla
            // IMPORTANTE: Nunca eliminar permisos < 10 porque son del ERP
            var permisosAEliminar = permisosActuales
                .Where(p => !codigosPermisosPlantilla.Contains(p.MRH_CodigoAplicacion) && p.MRH_CodigoAplicacion >= 10)
                .ToList();

            foreach (var permisoAEliminar in permisosAEliminar)
            {
                _sageContext.AccesosOperarios.Remove(permisoAEliminar);
            }

            // Agregar permisos nuevos de la plantilla
            foreach (var permiso in configuracion.Permisos)
            {
                // Verificar si el operario ya tiene este permiso
                var permisoExistente = permisosActuales
                    .FirstOrDefault(a => a.MRH_CodigoAplicacion == permiso.MRH_CodigoAplicacion);

                if (permisoExistente == null)
                {
                    // Agregar nuevo permiso
                    _sageContext.AccesosOperarios.Add(new AccesoOperario
                    {
                        Operario = operarioId,
                        MRH_CodigoAplicacion = permiso.MRH_CodigoAplicacion,
                        CodigoEmpresa = 1 // SGA siempre usa empresa 1
                    });
                }
            }

            // Aplicar empresas
            var empresasActuales = await _sageContext.OperariosEmpresas
                .Where(e => e.Operario == operarioId)
                .ToListAsync();

            var codigosEmpresasPlantilla = configuracion.Empresas.Select(e => e.EmpresaOrigen.ToString()).ToList();

            // Eliminar empresas que ya no están en la plantilla
            var empresasAEliminar = empresasActuales
                .Where(e => !codigosEmpresasPlantilla.Contains(e.Empresa))
                .ToList();

            foreach (var empresaAEliminar in empresasAEliminar)
            {
                _sageContext.OperariosEmpresas.Remove(empresaAEliminar);
            }

            // Agregar empresas nuevas de la plantilla
            foreach (var empresa in configuracion.Empresas)
            {
                var empresaExistente = empresasActuales
                    .FirstOrDefault(e => e.Empresa == empresa.EmpresaOrigen.ToString());

                if (empresaExistente == null)
                {
                    // Obtener el nombre real de la empresa
                    var nombreEmpresa = await _sageContext.Empresas
                        .Where(e => e.CodigoEmpresa == empresa.EmpresaOrigen)
                        .Select(e => e.EmpresaNombre)
                        .FirstOrDefaultAsync();

                    _sageContext.OperariosEmpresas.Add(new OperarioEmpresa
                    {
                        CodigoEmpresa = 1, // Siempre 1 para SGA
                        Operario = operarioId,
                        Empresa = nombreEmpresa ?? empresa.EmpresaOrigen.ToString(), // Nombre real o código como fallback
                        EmpresaOrigen = empresa.EmpresaOrigen // El código real de la empresa
                    });
                }
            }

            // Aplicar almacenes
            var almacenesActuales = await _sageContext.OperariosAlmacenes
                .Where(a => a.Operario == operarioId)
                .ToListAsync();

            var codigosAlmacenesPlantilla = configuracion.Almacenes.Select(a => a.CodigoAlmacen).ToList();

            // Eliminar almacenes que ya no están en la plantilla
            var almacenesAEliminar = almacenesActuales
                .Where(a => !codigosAlmacenesPlantilla.Contains(a.CodigoAlmacen))
                .ToList();

            foreach (var almacenAEliminar in almacenesAEliminar)
            {
                _sageContext.OperariosAlmacenes.Remove(almacenAEliminar);
            }

            // Agregar almacenes nuevos de la plantilla
            foreach (var almacen in configuracion.Almacenes)
            {
                var almacenExistente = almacenesActuales
                    .FirstOrDefault(a => a.CodigoAlmacen == almacen.CodigoAlmacen);

                if (almacenExistente == null)
                {
                    _sageContext.OperariosAlmacenes.Add(new OperarioAlmacen
                    {
                        Operario = operarioId,
                        CodigoAlmacen = almacen.CodigoAlmacen,
                        CodigoEmpresa = 1 // SGA siempre usa empresa 1
                    });
                }
            }

            // Aplicar límites usando SQL directo para evitar problemas con triggers
            if (configuracion.LimiteEuros.HasValue || configuracion.LimiteUnidades.HasValue)
            {
                var sql = "UPDATE operarios SET ";
                var parametros = new List<object>();
                var condiciones = new List<string>();

                if (configuracion.LimiteEuros.HasValue)
                {
                    condiciones.Add("MRH_LimiteInventarioEuros = {0}");
                    parametros.Add(configuracion.LimiteEuros.Value);
                }

                if (configuracion.LimiteUnidades.HasValue)
                {
                    condiciones.Add("MRH_LimiteInventarioUnidades = {1}");
                    parametros.Add(configuracion.LimiteUnidades.Value);
                }

                sql += string.Join(", ", condiciones) + " WHERE Operario = {" + parametros.Count + "}";
                parametros.Add(operarioId);

                await _sageContext.Database.ExecuteSqlRawAsync(sql, parametros.ToArray());
            }
        }

        /// <summary>
        /// Desasocia a un operario de su plantilla actual
        /// </summary>
        [HttpDelete("desasociar/{operarioId}")]
        public async Task<ActionResult> DesasociarPlantilla(int operarioId)
        {
            try
            {
                // Buscar la configuración aplicada activa del operario
                var configuracionAplicada = await _auroraContext.OperariosConfiguracionesAplicadas
                    .FirstOrDefaultAsync(oca => oca.OperarioId == operarioId && oca.Activa);

                if (configuracionAplicada != null)
                {
                    // Marcar como inactiva (soft delete)
                    configuracionAplicada.Activa = false;
                    configuracionAplicada.FechaAplicacion = DateTime.Now; // Actualizar fecha
                    
                    await _auroraContext.SaveChangesAsync();
                    
                    return Ok(new { success = true, message = "Plantilla desasociada correctamente" });
                }
                else
                {
                    return NotFound(new { success = false, message = "No se encontró una plantilla aplicada para este operario" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = $"Error al desasociar plantilla: {ex.Message}" });
            }
        }
    }

    /// <summary>
    /// DTO para aplicar plantilla a operarios específicos
    /// </summary>
    public class AplicarPlantillaAOperariosDto
    {
        public List<int> OperarioIds { get; set; } = new List<int>();
        public int UsuarioAplicacion { get; set; }
    }
}
