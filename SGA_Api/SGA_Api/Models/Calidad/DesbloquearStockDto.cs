using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Calidad
{
    public class DesbloquearStockDto
    {
        [Required]
        public Guid IdBloqueo { get; set; }

        [Required]
        [StringLength(500)]
        public string ComentarioDesbloqueo { get; set; } = string.Empty;

        [Required]
        public int UsuarioId { get; set; }
    }
}
