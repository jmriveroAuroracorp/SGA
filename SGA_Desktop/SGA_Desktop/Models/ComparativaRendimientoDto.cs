using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class ComparativaRendimientoDto
    {
        [JsonPropertyName("tipoComparativa")]
        public string TipoComparativa { get; set; } = string.Empty;
        
        [JsonPropertyName("items")]
        public List<ItemComparativaDto> Items { get; set; } = new List<ItemComparativaDto>();
    }
    
    public class ItemComparativaDto
    {
        [JsonPropertyName("etiqueta")]
        public string Etiqueta { get; set; } = string.Empty;
        
        [JsonPropertyName("valor")]
        public double Valor { get; set; }
        
        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;
        
        [JsonPropertyName("variacion")]
        public double? Variacion { get; set; }
    }
}

