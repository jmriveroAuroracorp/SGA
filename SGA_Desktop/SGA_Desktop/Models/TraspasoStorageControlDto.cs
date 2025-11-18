using System;

namespace SGA_Desktop.Models
{
    public class TraspasoStorageControlDto
    {
        public Guid MovTraspaso { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? Partida { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        
        public string? AlmacenOrigen { get; set; }
        public string? UbicacionOrigen { get; set; }
        public string? AlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        
        public decimal Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime? FechaRegistro { get; set; }
        public string? Comentario { get; set; }
        
        public short CodigoEmpresa { get; set; }
        public short? Ejercicio { get; set; }

        public string? CodigoPalet { get; set; }
        
        // Propiedad para identificar la fuente del traspaso
        public string Fuente { get; set; } = "SAGE";
        
        /// <summary>
        /// Indica si el movimiento no tiene pareja: "SIN_ENTRADA" (salida sin entrada), "SIN_SALIDA" (entrada sin salida), o null (traspaso completo)
        /// </summary>
        public string? EstadoMovimiento { get; set; }
    }
}

