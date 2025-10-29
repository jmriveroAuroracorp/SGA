using System;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// Información sobre bloqueos de calidad para un artículo
    /// </summary>
    public class BloqueoCalidadInfo
    {
        [JsonPropertyName("isBloqueado")]
        public bool IsBloqueado { get; set; }
        
        [JsonPropertyName("motivoBloqueo")]
        public string? MotivoBloqueo { get; set; }
        
        [JsonPropertyName("fechaBloqueo")]
        public DateTime? FechaBloqueo { get; set; }
        
        [JsonPropertyName("usuarioBloqueo")]
        public string? UsuarioBloqueo { get; set; }
        
        [JsonPropertyName("idBloqueo")]
        public Guid? IdBloqueo { get; set; }
    }
}
