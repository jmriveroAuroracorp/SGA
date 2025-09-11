using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.OrdenTraspaso
{
    [Table("OrdenTrabajoCabecera")]
    public class OrdenTraspasoCabecera
    {
        [Key]
        public Guid IdOrdenTrabajo { get; set; } = Guid.NewGuid();
        
        [Required]
        public short CodigoEmpresa { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = "PENDIENTE"; // 'PENDIENTE','EN_PROGRESO','COMPLETADA','CANCELADA'
        
        [Required]
        public short Prioridad { get; set; } = 10;
        
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string TipoOrigen { get; set; } = "MANUAL"; // 'MANUAL','INVENTARIO','ERP','AUTOMATICA'
        
        public Guid? IdOrigen { get; set; }
        
        [Required]
        public int UsuarioCreacion { get; set; }
        
        public int? UsuarioAsignado { get; set; }
        
        [MaxLength(500)]
        public string? Comentarios { get; set; }
        
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Navegación
        public virtual ICollection<OrdenTraspasoLinea> Lineas { get; set; } = new List<OrdenTraspasoLinea>();
        public virtual ICollection<OrdenTraspasoMovimiento> Movimientos { get; set; } = new List<OrdenTraspasoMovimiento>();
    }
} 