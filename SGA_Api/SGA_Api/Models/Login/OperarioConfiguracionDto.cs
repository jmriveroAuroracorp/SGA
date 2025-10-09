namespace SGA_Api.Models.Login
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
        
        // Rol SGA asignado
        public int? IdRol { get; set; }
        public string? RolNombre { get; set; }
        public int? NivelJerarquico { get; set; }
        
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
        
        // Rol SGA asignado
        public string? RolNombre { get; set; }
        public int? NivelJerarquico { get; set; }
    }

    public class EmpresaConfiguracionDto
    {
        public short CodigoEmpresa { get; set; }
        public short EmpresaOrigen { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class AlmacenConfiguracionDto
    {
        public short CodigoEmpresa { get; set; }
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string NombreEmpresa { get; set; } = string.Empty;
    }

    public class EmpresasOperarioResponseDto
    {
        public int OperarioId { get; set; }
        public string OperarioNombre { get; set; } = string.Empty;
        public string? CodigoCentro { get; set; }
        public List<EmpresaOperarioDto> Empresas { get; set; } = new List<EmpresaOperarioDto>();
    }

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
        
        // Rol SGA asignado
        public string? RolNombre { get; set; }
        public int? NivelJerarquico { get; set; }
    }

    public class PermisoDisponibleDto
    {
        public short Codigo { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }
}
