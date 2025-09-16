using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class OperariosAccesoDto
    {
        [JsonPropertyName("operario")]
        public int Operario { get; set; }
        
        [JsonPropertyName("nombreOperario")]
        public string? NombreOperario { get; set; }
        
        [JsonPropertyName("contraseña")]
        public string? Contraseña { get; set; }
        
        [JsonPropertyName("mrh_CodigoAplicacion")]
        public int MRH_CodigoAplicacion { get; set; }

        // Propiedad para mostrar en ComboBox
        public string NombreCompleto => $"{Operario} - {NombreOperario}";
        
        // Propiedad para mostrar en ComboBox (consistente con otros DTOs)
        public string DescripcionCombo => $"{Operario} - {NombreOperario}";
    }
} 