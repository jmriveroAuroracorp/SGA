using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.OrdenTraspaso
{
    [Table("OrdenTraspasoCabecera")]
    public class OrdenTraspasoCabecera
    {
        [Key]
        public Guid IdOrdenTraspaso { get; set; } = Guid.NewGuid();
        
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
        public string TipoOrigen { get; set; } = "SGA"; // 'SGA','MANUAL','INVENTARIO','ERP','AUTOMATICA'
        
        [Required]
        public int UsuarioCreacion { get; set; }
        
        [MaxLength(500)]
        public string? Comentarios { get; set; }
        
        [Required]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        
        [Required]
        [MaxLength(30)]
        public string CodigoOrden { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? CodigoAlmacenDestino { get; set; }

        // Navegación
        public virtual ICollection<OrdenTraspasoLinea> Lineas { get; set; } = new List<OrdenTraspasoLinea>();
    }
} 