using System;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class FiltroRendimientosDto
    {
        [JsonPropertyName("fechaDesde")]
        public DateTime? FechaDesde { get; set; }
        
        [JsonPropertyName("fechaHasta")]
        public DateTime? FechaHasta { get; set; }
        
        [JsonPropertyName("operarioId")]
        public int? OperarioId { get; set; }
        
        [JsonPropertyName("tipoProceso")]
        public string? TipoProceso { get; set; }
        
        [JsonPropertyName("codigoEmpresa")]
        public short? CodigoEmpresa { get; set; }
        
        [JsonPropertyName("codigoAlmacen")]
        public string? CodigoAlmacen { get; set; }
    }
}

