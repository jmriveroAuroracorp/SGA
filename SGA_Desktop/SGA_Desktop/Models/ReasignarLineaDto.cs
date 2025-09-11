using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class ReasignarLineaDto
    {
        [Required(ErrorMessage = "El código del operario es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del operario no puede exceder 50 caracteres")]
        [JsonPropertyName("codigoOperario")]
        public string CodigoOperario { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        [JsonPropertyName("comentario")]
        public string? Comentario { get; set; }
        
        [StringLength(50, ErrorMessage = "El código del supervisor no puede exceder 50 caracteres")]
        [JsonPropertyName("supervisorCodigo")]
        public string? SupervisorCodigo { get; set; }
    }
} 