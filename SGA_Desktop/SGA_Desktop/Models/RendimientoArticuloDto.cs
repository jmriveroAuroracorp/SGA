using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class RendimientoArticuloDto
    {
        [JsonPropertyName("codigoFamilia")]
        public string CodigoFamilia { get; set; } = string.Empty;
        
        [JsonPropertyName("cantidadArticulosUnicos")]
        public int CantidadArticulosUnicos { get; set; }
        
        // Frecuencia de movimiento
        [JsonPropertyName("cantidadTraspasos")]
        public int CantidadTraspasos { get; set; }
        
        [JsonPropertyName("unidadesTotalesMovidas")]
        public decimal UnidadesTotalesMovidas { get; set; }
        
        [JsonPropertyName("promedioUnidadesPorTraspaso")]
        public decimal PromedioUnidadesPorTraspaso { get; set; }
        
        // Eficiencia de manejo
        [JsonPropertyName("tiempoPromedioMinutos")]
        public double? TiempoPromedioMinutos { get; set; }
        
        [JsonPropertyName("tiempoTotalMinutos")]
        public double TiempoTotalMinutos { get; set; }
        
        [JsonPropertyName("eficienciaUnidadesPorMinuto")]
        public double? EficienciaUnidadesPorMinuto { get; set; }
        
        // Distribución y alcance
        [JsonPropertyName("almacenesUnicos")]
        public int AlmacenesUnicos { get; set; }
        
        [JsonPropertyName("ubicacionesUnicas")]
        public int UbicacionesUnicas { get; set; }
        
        [JsonPropertyName("operariosUnicos")]
        public int OperariosUnicos { get; set; }
        
        // Rankings y porcentajes
        [JsonPropertyName("posicion")]
        public int Posicion { get; set; }
        
        [JsonPropertyName("porcentajeDelTotalTraspasos")]
        public double PorcentajeDelTotalTraspasos { get; set; }
    }
}

