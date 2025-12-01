using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class RendimientoProcesoDto
    {
        [JsonPropertyName("tipoProceso")]
        public string TipoProceso { get; set; } = string.Empty;
        
        [JsonPropertyName("totalProcesos")]
        public int TotalProcesos { get; set; }
        
        [JsonPropertyName("procesosCompletados")]
        public int ProcesosCompletados { get; set; }
        
        [JsonPropertyName("procesosPendientes")]
        public int ProcesosPendientes { get; set; }
        
        [JsonPropertyName("tasaFinalizacion")]
        public double TasaFinalizacion { get; set; }
        
        [JsonPropertyName("tiempoPromedioMinutos")]
        public double? TiempoPromedioMinutos { get; set; }
        
        [JsonPropertyName("tiempoMinimoMinutos")]
        public double? TiempoMinimoMinutos { get; set; }
        
        [JsonPropertyName("tiempoMaximoMinutos")]
        public double? TiempoMaximoMinutos { get; set; }
        
        [JsonPropertyName("lineasPorHora")]
        public double? LineasPorHora { get; set; }
        
        [JsonPropertyName("procesosPorDia")]
        public double? ProcesosPorDia { get; set; }
    }
}

