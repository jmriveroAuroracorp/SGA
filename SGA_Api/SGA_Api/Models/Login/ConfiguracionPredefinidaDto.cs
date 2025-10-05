namespace SGA_Api.Models.Login
{
    /// <summary>
    /// DTO para listado de configuraciones predefinidas
    /// </summary>
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

    /// <summary>
    /// DTO para configuración predefinida completa
    /// </summary>
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
        
        // Listas detalladas
        public List<PermisoDisponibleDto> Permisos { get; set; } = new List<PermisoDisponibleDto>();
        public List<EmpresaConfiguracionDto> Empresas { get; set; } = new List<EmpresaConfiguracionDto>();
        public List<AlmacenConfiguracionDto> Almacenes { get; set; } = new List<AlmacenConfiguracionDto>();
    }

    /// <summary>
    /// DTO para crear/actualizar configuración predefinida
    /// </summary>
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

    /// <summary>
    /// DTO para aplicar configuración predefinida a un operario
    /// </summary>
    public class AplicarConfiguracionPredefinidaDto
    {
        public int OperarioId { get; set; }
        public int ConfiguracionId { get; set; }
        public bool ReemplazarExistente { get; set; } = false; // Si true, elimina configuración actual antes de aplicar
    }
}
