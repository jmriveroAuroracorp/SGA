using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Stock
{
    [Table("MovimientoStockUbicacion", Schema = "dbo")]
    public class MovimientoStockUbicacion
    {
        [Key]
        [Column(Order = 0)]
        public short CodigoEmpresa { get; set; }
        
        [Key]
        [Column(Order = 1)]
        public Guid MovPosicion { get; set; }
        
        public short? Ejercicio { get; set; }
        public string? CodigoAlmacen { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? CodigoColor_ { get; set; }
        public string? CodigoTalla01_ { get; set; }
        public string? UnidadMedida1_ { get; set; }
        public string? Partida { get; set; }
        public string? Ubicacion { get; set; }
        
        public Guid? MovTraspaso { get; set; }
        public Guid? MovOrigen { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal? Unidades { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal? Unidades2_ { get; set; }
        
        public byte? TipoMovimiento { get; set; } // 1=Entrada, 2=Salida
        public DateTime? Fecha { get; set; }
        public DateTime? FechaRegistro { get; set; }
        public string? Comentario { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal? UnidadesRebajadasFactor0 { get; set; }
    }
}

