using SGA_Api.Models.RolesSga;
using Microsoft.EntityFrameworkCore;
using SGA_Api.Data;

namespace SGA_Api.Services
{
    public class RolesSgaService : IRolesSgaService
    {
        private readonly AuroraSgaDbContext _auroraContext;
        private readonly SageDbContext _sageContext;

        public RolesSgaService(AuroraSgaDbContext auroraContext, SageDbContext sageContext)
        {
            _auroraContext = auroraContext;
            _sageContext = sageContext;
        }

        public async Task<IEnumerable<RolSgaDto>> ObtenerRolesSgaAsync()
        {
            // Por ahora, retornamos roles hardcodeados
            // En el futuro, esto vendría de una tabla en la base de datos
            var roles = new List<RolSgaDto>
            {
                new RolSgaDto
                {
                    Id = 1,
                    Nombre = "OPERARIO",
                    Descripcion = "Nivel básico de notificaciones del sistema SGA. Recibe notificaciones esenciales para operaciones estándar.",
                    NivelJerarquico = 10,
                    Activo = true
                },
                new RolSgaDto
                {
                    Id = 2,
                    Nombre = "SUPERVISOR",
                    Descripcion = "Nivel intermedio de notificaciones del sistema SGA. Recibe notificaciones de supervisión y gestión.",
                    NivelJerarquico = 20,
                    Activo = true
                },
                new RolSgaDto
                {
                    Id = 3,
                    Nombre = "ADMIN",
                    Descripcion = "Nivel completo de notificaciones del sistema SGA. Recibe todas las notificaciones del sistema incluyendo configuraciones administrativas.",
                    NivelJerarquico = 30,
                    Activo = true
                }
            };

            return await Task.FromResult(roles);
        }

        public async Task<RolSgaDto?> ObtenerRolSgaPorIdAsync(int id)
        {
            var roles = await ObtenerRolesSgaAsync();
            return roles.FirstOrDefault(r => r.Id == id);
        }

        public async Task<RolSugeridoDto> ObtenerRolSugeridoAsync(int operarioId)
        {
            try
            {
                // Obtener permisos del operario desde la base de datos Sage
                var permisosERP = await _sageContext.AccesosOperarios
                    .Where(a => a.Operario == operarioId && a.CodigoEmpresa == 1) // Asumiendo empresa 1 para SGA
                    .Select(a => a.MRH_CodigoAplicacion)
                    .ToListAsync();

                // Lógica para sugerir rol basado en permisos ERP
                RolSugeridoDto rolSugerido;

                // Si tiene permisos administrativos (códigos altos), sugerir ADMIN
                if (permisosERP.Any(p => p >= 100))
                {
                    rolSugerido = new RolSugeridoDto
                    {
                        RolId = 3,
                        RolNombre = "ADMIN",
                        Descripcion = "Nivel completo de notificaciones del sistema SGA",
                        Justificacion = "El operario tiene permisos administrativos (códigos >= 100)"
                    };
                }
                // Si tiene permisos de supervisión (códigos entre 50-99), sugerir SUPERVISOR
                else if (permisosERP.Any(p => p >= 50 && p < 100))
                {
                    rolSugerido = new RolSugeridoDto
                    {
                        RolId = 2,
                        RolNombre = "SUPERVISOR",
                        Descripcion = "Nivel intermedio de notificaciones del sistema SGA",
                        Justificacion = "El operario tiene permisos de supervisión (códigos 50-99)"
                    };
                }
                // Si solo tiene permisos básicos, sugerir OPERARIO
                else if (permisosERP.Any())
                {
                    rolSugerido = new RolSugeridoDto
                    {
                        RolId = 1,
                        RolNombre = "OPERARIO",
                        Descripcion = "Nivel básico de notificaciones del sistema SGA",
                        Justificacion = "El operario tiene permisos básicos (códigos < 50)"
                    };
                }
                // Si no tiene permisos, no sugerir rol
                else
                {
                    rolSugerido = new RolSugeridoDto
                    {
                        Justificacion = "El operario no tiene permisos asignados"
                    };
                }

                return rolSugerido;
            }
            catch (Exception)
            {
                // En caso de error, retornar sin sugerencia
                return new RolSugeridoDto
                {
                    Justificacion = "No se pudo determinar el rol sugerido debido a un error en el sistema"
                };
            }
        }

        public async Task<RolSugeridoDto?> SuggestRolSgaAsync(int operarioId)
        {
            try
            {
                var resultado = await ObtenerRolSugeridoAsync(operarioId);
                return resultado.Justificacion.Contains("error") ? null : resultado;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IEnumerable<RolSgaDto>> GetRolesSgaAsync()
        {
            return await ObtenerRolesSgaAsync();
        }

        public async Task<RolSgaDto?> GetRolSgaByIdAsync(int id)
        {
            return await ObtenerRolSgaPorIdAsync(id);
        }
    }
}
