using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class RendimientoOperarioDto
    {
        [JsonPropertyName("operarioId")]
        public int OperarioId { get; set; }
        
        [JsonPropertyName("nombreOperario")]
        public string? NombreOperario { get; set; }
        
        [JsonPropertyName("totalOperaciones")]
        public int TotalOperaciones { get; set; }
        
        [JsonPropertyName("traspasosCompletados")]
        public int TraspasosCompletados { get; set; }
        
        [JsonPropertyName("traspasosPalet")]
        public int TraspasosPalet { get; set; }
        
        [JsonPropertyName("traspasosArticulo")]
        public int TraspasosArticulo { get; set; }
        
        [JsonPropertyName("lineasInventarioContadas")]
        public int LineasInventarioContadas { get; set; }
        
        [JsonPropertyName("lecturasConteo")]
        public int LecturasConteo { get; set; }
        
        [JsonPropertyName("conteosCompletados")]
        public int ConteosCompletados { get; set; }
        
        [JsonPropertyName("tiempoPromedioTraspasosMinutos")]
        public double? TiempoPromedioTraspasosMinutos { get; set; }
        
        [JsonPropertyName("tiempoPromedioInventarioMinutos")]
        public double? TiempoPromedioInventarioMinutos { get; set; }
        
        [JsonPropertyName("tiempoPromedioConteoMinutos")]
        public double? TiempoPromedioConteoMinutos { get; set; }
        
        [JsonPropertyName("traspasosConErrores")]
        public int TraspasosConErrores { get; set; }
        
        [JsonPropertyName("lineasPorHora")]
        public double? LineasPorHora { get; set; }
        
        [JsonPropertyName("lecturasPorHora")]
        public double? LecturasPorHora { get; set; }
        
        [JsonPropertyName("traspasosPorDia")]
        public double? TraspasosPorDia { get; set; }
        
        [JsonPropertyName("porcentajeDelTotal")]
        public double? PorcentajeDelTotal { get; set; }
        
        [JsonPropertyName("ranking")]
        public int Ranking { get; set; }
        
        [JsonPropertyName("tiempoMinimoTraspasosMinutos")]
        public double? TiempoMinimoTraspasosMinutos { get; set; }
        
        [JsonPropertyName("tiempoMaximoTraspasosMinutos")]
        public double? TiempoMaximoTraspasosMinutos { get; set; }
        
        [JsonPropertyName("tiempoMedianoTraspasosMinutos")]
        public double? TiempoMedianoTraspasosMinutos { get; set; }
        
        [JsonPropertyName("tasaFinalizacion")]
        public double? TasaFinalizacion { get; set; }
        
        [JsonPropertyName("diasActivos")]
        public int DiasActivos { get; set; }
        
        [JsonPropertyName("tiempoTotalTrabajadoMinutos")]
        public double? TiempoTotalTrabajadoMinutos { get; set; }
        
        [JsonPropertyName("almacenesDiferentes")]
        public int AlmacenesDiferentes { get; set; }
        
        [JsonPropertyName("articulosDiferentes")]
        public int ArticulosDiferentes { get; set; }
        
        [JsonPropertyName("ultimaActividad")]
        public DateTime? UltimaActividad { get; set; }
        
        [JsonPropertyName("variacionPorcentual")]
        public double? VariacionPorcentual { get; set; }
    }
}

