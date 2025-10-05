namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO completo para configuración de operarios desde Aurora
    /// </summary>
    public class OperarioConfiguracionDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Contraseña { get; set; }
        public DateTime? FechaBaja { get; set; }
        public string? CodigoCentro { get; set; }
        public decimal? LimiteInventarioEuros { get; set; }
        public decimal? LimiteInventarioUnidades { get; set; }
        
        // Permisos del operario
        public List<short> Permisos { get; set; } = new List<short>();
        
        // Empresas asignadas
        public List<EmpresaOperarioDto> Empresas { get; set; } = new List<EmpresaOperarioDto>();
        
        // Almacenes asignados
        public List<AlmacenOperarioDto> Almacenes { get; set; } = new List<AlmacenOperarioDto>();
        
        // Plantilla aplicada
        public string? PlantillaAplicada { get; set; } // Nombre de la plantilla aplicada
    }

    /// <summary>
    /// DTO para empresas asignadas a un operario
    /// </summary>
    public class EmpresaOperarioDto
    {
        public short CodigoEmpresa { get; set; }
        public short EmpresaOrigen { get; set; }
        public string Empresa { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para almacenes asignados a un operario
    /// </summary>
    public class AlmacenOperarioDto
    {
        public short CodigoEmpresa { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? DescripcionAlmacen { get; set; }
        public string NombreEmpresa { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para crear/actualizar operario
    /// </summary>
    public class OperarioUpdateDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Contraseña { get; set; }
        public string? CodigoCentro { get; set; }
        public decimal? LimiteInventarioEuros { get; set; }
        public decimal? LimiteInventarioUnidades { get; set; }
        public bool Activo { get; set; } = true;
        
        // IDs de permisos a asignar/quitar
        public List<short> PermisosAsignar { get; set; } = new List<short>();
        public List<short> PermisosQuitar { get; set; } = new List<short>();
        
        // Empresas a asignar/quitar
        public List<EmpresaOperarioDto> EmpresasAsignar { get; set; } = new List<EmpresaOperarioDto>();
        public List<short> EmpresasQuitar { get; set; } = new List<short>();
        
        // Almacenes a asignar/quitar
        public List<AlmacenOperarioDto> AlmacenesAsignar { get; set; } = new List<AlmacenOperarioDto>();
        public List<string> AlmacenesQuitar { get; set; } = new List<string>();
    }

    /// <summary>
    /// DTO para listado de operarios con información básica
    /// </summary>
    public class OperarioListaDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? CodigoCentro { get; set; }
        public DateTime? FechaBaja { get; set; } // Fecha de baja del operario
        public bool Activo { get; set; }
        public string Permisos { get; set; } = string.Empty; // Lista de permisos separados por comas
        public string Empresas { get; set; } = string.Empty; // Lista de empresas separadas por comas
        public string Almacenes { get; set; } = string.Empty; // Lista de almacenes separados por comas
        public int CantidadPermisos { get; set; } // Número de permisos asignados
        public int CantidadAlmacenes { get; set; } // Número de almacenes asignados
        public string? PlantillaAplicada { get; set; } // Nombre de la plantilla aplicada
        public decimal? LimiteImporte { get; set; } // Límite de importe del operario (MRH_LimiteInventarioEuros)
        public decimal? LimiteUnidades { get; set; } // Límite de unidades del operario (MRH_LimiteInventarioUnidades)
        public string LimitesResumen { get; set; } = string.Empty; // Resumen de límites formateado
    }

    /// <summary>
    /// DTO para empresas disponibles en configuración de operarios
    /// </summary>
    public class EmpresaConfiguracionDto
    {
        public short CodigoEmpresa { get; set; }
        public short EmpresaOrigen { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para almacenes disponibles en configuración de operarios
    /// </summary>
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
    /// DTO para operarios disponibles para seleccionar
    /// </summary>
    public class OperarioDisponibleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? CodigoCentro { get; set; }
        public DateTime? FechaBaja { get; set; } // Fecha de baja del operario
        public bool Activo { get; set; } // Indica si el operario está activo
        public string Permisos { get; set; } = string.Empty; // Lista de permisos separados por comas
        public string Empresas { get; set; } = string.Empty; // Lista de empresas separadas por comas
        public string Almacenes { get; set; } = string.Empty; // Lista de almacenes separados por comas
        public int CantidadPermisos { get; set; } // Número de permisos asignados
        public int CantidadAlmacenes { get; set; } // Número de almacenes asignados
        public string? PlantillaAplicada { get; set; } // Nombre de la plantilla aplicada
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

    public class PermisoDisponibleDto
    {
        public short Codigo { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }

    // DTOs para Configuraciones Predefinidas
    public class ConfiguracionPredefinidaListaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public bool Activa { get; set; }
        public int CantidadPermisos { get; set; }
        public int CantidadEmpresas { get; set; }
        public int CantidadAlmacenes { get; set; }
        
        // Límites
        public decimal? LimiteEuros { get; set; }
        public decimal? LimiteUnidades { get; set; }
        
        // Usuarios de auditoría
        public int? UsuarioCreacion { get; set; }
        public int? UsuarioModificacion { get; set; }
        
        // Contador de operarios usando esta configuración
        public int OperariosUsando { get; set; } = 0;
    }

    public class ConfiguracionPredefinidaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public bool Activa { get; set; }
        public int CantidadPermisos { get; set; }
        public int CantidadEmpresas { get; set; }
        public int CantidadAlmacenes { get; set; }
        
        // Límites
        public decimal? LimiteEuros { get; set; }
        public decimal? LimiteUnidades { get; set; }
        
        // Usuarios de auditoría
        public int? UsuarioCreacion { get; set; }
        public int? UsuarioModificacion { get; set; }
        
        // Propiedades calculadas para mostrar en los cards
        public string PermisosResumen => $"{CantidadPermisos} permisos";
        public string EmpresasResumen => $"{CantidadEmpresas} empresas";
        public string AlmacenesResumen => $"{CantidadAlmacenes} almacenes";
        public string LimitesResumen 
        {
            get
            {
                var limites = new List<string>();
                
                if (LimiteEuros.HasValue && LimiteEuros.Value > 0)
                {
                    limites.Add($"€{LimiteEuros.Value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}");
                }
                
                if (LimiteUnidades.HasValue && LimiteUnidades.Value > 0)
                {
                    limites.Add($"{LimiteUnidades.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} unidades");
                }
                
                return limites.Any() ? string.Join(" / ", limites) : "Sin límites";
            }
        }
        public int OperariosUsando { get; set; } = 0; // TODO: Implementar contador de operarios que usan esta configuración
    }

    public class ConfiguracionPredefinidaCompletaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public bool Activa { get; set; }
        
        // Límites
        public decimal? LimiteEuros { get; set; }
        public decimal? LimiteUnidades { get; set; }
        
        // Usuarios de auditoría
        public int? UsuarioCreacion { get; set; }
        public int? UsuarioModificacion { get; set; }
        
        public List<PermisoDisponibleDto> Permisos { get; set; } = new List<PermisoDisponibleDto>();
        public List<EmpresaConfiguracionDto> Empresas { get; set; } = new List<EmpresaConfiguracionDto>();
        public List<AlmacenConfiguracionDto> Almacenes { get; set; } = new List<AlmacenConfiguracionDto>();
    }

    public class ConfiguracionPredefinidaCrearDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public List<short> Permisos { get; set; } = new List<short>();
        public List<short> Empresas { get; set; } = new List<short>();
        public List<string> Almacenes { get; set; } = new List<string>();
        
        // Límites
        public decimal? LimiteEuros { get; set; }
        public decimal? LimiteUnidades { get; set; }
        
        // Usuario que crea/modifica
        public int? Usuario { get; set; }
    }

    public class AplicarConfiguracionPredefinidaDto
    {
        public int OperarioId { get; set; }
        public int ConfiguracionId { get; set; }
        public bool ReemplazarExistente { get; set; } = false;
    }

    /// <summary>
    /// DTO para empleados disponibles para dar de alta en SGA
    /// </summary>
    public class EmpleadoDisponibleDto
    {
        public int CodigoEmpleado { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false; // Para selección múltiple
    }

    /// <summary>
    /// DTO para dar de alta un empleado en SGA
    /// </summary>
    public class DarAltaEmpleadoDto
    {
        public int CodigoEmpleado { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Contraseña { get; set; }
        public string? CodigoCentro { get; set; }
        public List<short> PermisosIniciales { get; set; } = new List<short>();
        public List<short> EmpresasIniciales { get; set; } = new List<short>();
        public List<string> AlmacenesIniciales { get; set; } = new List<string>();
    }
}
