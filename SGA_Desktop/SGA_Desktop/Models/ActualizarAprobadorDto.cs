using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    public class ActualizarAprobadorDto
    {
        [Required(ErrorMessage = "El código del operario aprobador es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del operario no puede exceder 50 caracteres")]
        [JsonPropertyName("aprobadoPorCodigo")]
        public string AprobadoPorCodigo { get; set; } = string.Empty;
    }
} 