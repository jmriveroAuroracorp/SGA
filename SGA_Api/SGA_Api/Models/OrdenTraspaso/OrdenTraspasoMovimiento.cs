using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.OrdenTraspaso
{
    [Table("OrdenTrabajoMovimientos")]
    public class OrdenTraspasoMovimiento
    {
        [Key]
        public Guid IdMovimiento { get; set; } = Guid.NewGuid();
        
        [Required]
        public Guid IdOrdenTrabajo { get; set; }
        
        [Required]
        public Guid IdLineaOrden { get; set; }
        
        // Enlaces a traspaso ejecutado (opcional)
        public Guid? IdTraspaso { get; set; } // -> TraspasoCabecera(IdTraspaso)
        public Guid? IdLineaTraspaso { get; set; } // -> TraspasoLineas(IdLineaTraspaso)
        
        [Required]
        public DateTime FechaMovimiento { get; set; } = DateTime.Now;
        
        [Required]
        public int IdOperario { get; set; }
        
        [MaxLength(200)]
        public string? Comentarios { get; set; }

        // Navegación
        [ForeignKey("IdOrdenTrabajo")]
        public virtual OrdenTraspasoCabecera OrdenTraspaso { get; set; }
        
        [ForeignKey("IdLineaOrden")]
        public virtual OrdenTraspasoLinea LineaOrden { get; set; }
    }
} 