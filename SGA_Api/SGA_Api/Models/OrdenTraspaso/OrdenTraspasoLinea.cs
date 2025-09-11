using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.OrdenTraspaso
{
    [Table("OrdenTrabajoLineas")]
    public class OrdenTraspasoLinea
    {
        [Key]
        public Guid IdLineaOrden { get; set; } = Guid.NewGuid();
        
        [Required]
        public Guid IdOrdenTrabajo { get; set; }
        
        [Required]
        public int Orden { get; set; }
        
        [Required]
        [MaxLength(30)]
        public string CodigoArticulo { get; set; }
        
        [MaxLength(200)]
        public string? DescripcionArticulo { get; set; }
        
        [MaxLength(10)]
        public string? UM { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal CantidadPlan { get; set; }

        // Origen
        [Required]
        [MaxLength(10)]
        public string CodigoAlmacenOrigen { get; set; }
        
        [MaxLength(30)]
        public string? UbicacionOrigen { get; set; }
        
        [MaxLength(50)]
        public string? PartidaOrigen { get; set; }
        
        [MaxLength(50)]
        public string? PaletOrigen { get; set; }

        // Destino
        [Required]
        [MaxLength(10)]
        public string CodigoAlmacenDestino { get; set; }
        
        [MaxLength(30)]
        public string? UbicacionDestino { get; set; }
        
        [MaxLength(50)]
        public string? PartidaDestino { get; set; }
        
        [MaxLength(50)]
        public string? PaletDestino { get; set; }

        // Estado/ejecución
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = "PENDIENTE"; // 'PENDIENTE','EN_PROGRESO','COMPLETADA'
        
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        public decimal CantidadMovida { get; set; } = 0;
        
        [Required]
        public bool Completada { get; set; } = false;
        
        public int? IdOperarioAsignado { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFinalizacion { get; set; }

        // Enlace con traspaso ejecutado
        public Guid? IdTraspaso { get; set; } // FK -> TraspasoCabecera(IdTraspaso)
        public Guid? IdLineaTraspaso { get; set; } // FK -> TraspasoLineas(IdLineaTraspaso)

        // Navegación
        [ForeignKey("IdOrdenTrabajo")]
        public virtual OrdenTraspasoCabecera OrdenTraspaso { get; set; }
        
        public virtual ICollection<OrdenTraspasoMovimiento> Movimientos { get; set; } = new List<OrdenTraspasoMovimiento>();
    }
} 