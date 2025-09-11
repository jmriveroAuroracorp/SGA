using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Conteos
{
    public class ReasignarLineaDto
    {
        [Required(ErrorMessage = "El código del operario es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del operario no puede exceder 50 caracteres")]
        public string CodigoOperario { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        public string? Comentario { get; set; }
        
        [StringLength(50, ErrorMessage = "El código del supervisor no puede exceder 50 caracteres")]
        public string? SupervisorCodigo { get; set; }
    }
} 