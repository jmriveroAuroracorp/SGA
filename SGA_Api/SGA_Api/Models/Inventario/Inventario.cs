using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Inventario
{
    /// <summary>
    /// Entidad para almacenar registros de inventario físico
    /// </summary>
    [Table("Inventarios")]
    public class Inventario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CodigoEmpresa { get; set; }

        [Required]
        [StringLength(50)]
        public string CodigoArticulo { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DescripcionArticulo { get; set; }

        [Required]
        [StringLength(20)]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [StringLength(100)]
        public string Almacen { get; set; } = string.Empty;

        [StringLength(50)]
        public string Ubicacion { get; set; } = string.Empty;

        [StringLength(50)]
        public string Partida { get; set; } = string.Empty;

        public DateTime? FechaCaducidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StockSistema { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StockFisico { get; set; }

        [StringLength(500)]
        public string? Observaciones { get; set; }

        public DateTime FechaInventario { get; set; }

        [StringLength(100)]
        public string UsuarioInventario { get; set; } = string.Empty;

        [StringLength(20)]
        public string EstadoInventario { get; set; } = "PENDIENTE"; // PENDIENTE, COMPLETADO, APROBADO

        [StringLength(50)]
        public string? CodigoPalet { get; set; }

        [StringLength(50)]
        public string? EstadoPalet { get; set; }

        [StringLength(200)]
        public string Alergenos { get; set; } = string.Empty;

        [StringLength(50)]
        public string CodigoAlternativo { get; set; } = string.Empty;

        // Propiedades calculadas
        [NotMapped]
        public decimal Diferencia => StockFisico - StockSistema;

        [NotMapped]
        public bool TieneDiferencia => Math.Abs(Diferencia) > 0.01m;
    }
} 