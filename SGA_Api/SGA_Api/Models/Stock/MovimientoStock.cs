using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Stock
{
    [Table("MovimientoStock", Schema = "dbo")]
    public class MovimientoStock
    {
        [Key]
        [Column(Order = 0)]
        public short CodigoEmpresa { get; set; }
        
        [Key]
        [Column(Order = 1)]
        public short Ejercicio { get; set; }
        
        [Key]
        [Column(Order = 2)]
        public short Periodo { get; set; }
        
        [Key]
        [Column(Order = 3)]
        public DateTime Fecha { get; set; }
        
        [Key]
        [Column(Order = 4)]
        public DateTime FechaRegistro { get; set; }
        
        [Key]
        [Column(Order = 5)]
        public string Serie { get; set; } = "";
        
        [Key]
        [Column(Order = 6)]
        public int Documento { get; set; }
        
        [Key]
        [Column(Order = 7)]
        public Guid MovPosicion { get; set; }
        
        public string CodigoArticulo { get; set; } = "";
        public string CodigoAlmacen { get; set; } = "";
        public string AlmacenContrapartida { get; set; } = "";
        public string Partida { get; set; } = "";
        public string Partida2_ { get; set; } = "";
        public string CodigoColor_ { get; set; } = "";
        public short GrupoTalla_ { get; set; }
        public string CodigoTalla01_ { get; set; } = "";
        public byte TipoMovimiento { get; set; } // 1=Entrada, 2=Salida
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal Unidades { get; set; }
        
        public string UnidadMedida1_ { get; set; } = "";
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal Precio { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal Importe { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal Unidades2_ { get; set; }
        
        public string UnidadMedida2_ { get; set; } = "";
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal FactorConversion_ { get; set; }
        
        public string Comentario { get; set; } = "";
        public string CodigoCanal { get; set; } = "";
        public string CodigoCliente { get; set; } = "";
        public string CodigoProveedor { get; set; } = "";
        public DateTime? FechaCaduca { get; set; }
        public string Ubicacion { get; set; } = "";
        public short StatusAcumulado { get; set; }
        public string OrigenMovimiento { get; set; } = "";
        public Guid MovTraspaso { get; set; }
        public short UsuarioProceso { get; set; }
        public short EmpresaOrigen { get; set; }
        public Guid MovOrigen { get; set; }
        public short EjercicioDocumento { get; set; }
        public Guid MovConsumo { get; set; }
        public Guid MovIdentificador { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal ImporteCoste { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal UnidadEntrada { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal UnidadStock { get; set; }
        
        [Column(TypeName = "decimal(28,10)")]
        public decimal PrecioMedio { get; set; }
        
        public short CalculoPrecioMedio { get; set; }
        public Guid Proceso { get; set; }
        public short sysTraspasoLWG { get; set; }
        public string NumeroSerieLc { get; set; } = "";
        public string MRH_UsuarioMobility { get; set; } = "";
        public string MRH_DescripcionMobility { get; set; } = "";
        public Guid MRH_ContratoProv { get; set; }
    }
}

