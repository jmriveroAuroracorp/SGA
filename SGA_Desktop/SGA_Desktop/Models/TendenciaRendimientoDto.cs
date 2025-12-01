using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class TendenciaRendimientoDto
    {
        [JsonPropertyName("tipoMetrica")]
        public string TipoMetrica { get; set; } = string.Empty;
        
        [JsonPropertyName("puntos")]
        public List<PuntoTendenciaDto> Puntos { get; set; } = new List<PuntoTendenciaDto>();
    }
    
    public class PuntoTendenciaDto
    {
        [JsonPropertyName("fecha")]
        public DateTime Fecha { get; set; }
        
        [JsonPropertyName("periodo")]
        public string Periodo { get; set; } = string.Empty;
        
        [JsonPropertyName("valor")]
        public double Valor { get; set; }
        
        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;
    }
}

