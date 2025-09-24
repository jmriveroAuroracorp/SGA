using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Login
{
    /// <summary>
    /// Modelo para la tabla ConfiguracionesPredefinidas
    /// </summary>
    public class ConfiguracionPredefinida
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Descripcion { get; set; }
        
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        
        public DateTime? FechaModificacion { get; set; }
        
        public bool Activa { get; set; } = true;
        
        // Límites de la configuración
        public decimal? LimiteEuros { get; set; }
        public decimal? LimiteUnidades { get; set; }
        
        // Usuarios de auditoría
        public int? UsuarioCreacion { get; set; }
        public int? UsuarioModificacion { get; set; }
        
        // Navegación a las tablas relacionadas
        public virtual ICollection<ConfiguracionPredefinidaPermiso> Permisos { get; set; } = new List<ConfiguracionPredefinidaPermiso>();
        public virtual ICollection<ConfiguracionPredefinidaEmpresa> Empresas { get; set; } = new List<ConfiguracionPredefinidaEmpresa>();
        public virtual ICollection<ConfiguracionPredefinidaAlmacen> Almacenes { get; set; } = new List<ConfiguracionPredefinidaAlmacen>();
    }

    /// <summary>
    /// Modelo para la tabla ConfiguracionesPredefinidasPermisos
    /// </summary>
    public class ConfiguracionPredefinidaPermiso
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ConfiguracionId { get; set; }
        
        [Required]
        public short MRH_CodigoAplicacion { get; set; }
        
        // Navegación
        [ForeignKey("ConfiguracionId")]
        public virtual ConfiguracionPredefinida Configuracion { get; set; } = null!;
    }

    /// <summary>
    /// Modelo para la tabla ConfiguracionesPredefinidasEmpresas
    /// </summary>
    public class ConfiguracionPredefinidaEmpresa
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ConfiguracionId { get; set; }
        
        [Required]
        public short CodigoEmpresa { get; set; }
        
        [Required]
        public short EmpresaOrigen { get; set; }
        
        // Navegación
        [ForeignKey("ConfiguracionId")]
        public virtual ConfiguracionPredefinida Configuracion { get; set; } = null!;
    }

    /// <summary>
    /// Modelo para la tabla ConfiguracionesPredefinidasAlmacenes
    /// </summary>
    public class ConfiguracionPredefinidaAlmacen
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ConfiguracionId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string CodigoAlmacen { get; set; } = string.Empty;
        
        // Navegación
        [ForeignKey("ConfiguracionId")]
        public virtual ConfiguracionPredefinida Configuracion { get; set; } = null!;
    }
}
