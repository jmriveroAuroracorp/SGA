using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Conteos
{
    public class ActualizarAprobadorDto
    {
        [Required(ErrorMessage = "El código del operario aprobador es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del operario no puede exceder 50 caracteres")]
        public string AprobadoPorCodigo { get; set; } = string.Empty;
    }
}
