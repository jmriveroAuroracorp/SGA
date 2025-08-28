using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Conteos
{
    public class LecturaDto
    {
        [Required(ErrorMessage = "El código del almacén es obligatorio")]
        [StringLength(10, ErrorMessage = "El código del almacén no puede exceder 10 caracteres")]
        public string CodigoAlmacen { get; set; } = string.Empty;
        
        [StringLength(30, ErrorMessage = "El código de ubicación no puede exceder 30 caracteres")]
        public string? CodigoUbicacion { get; set; }
        
        [StringLength(30, ErrorMessage = "El código del artículo no puede exceder 30 caracteres")]
        public string? CodigoArticulo { get; set; }
        
        [StringLength(40, ErrorMessage = "El lote/partida no puede exceder 40 caracteres")]
        public string? LotePartida { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "La cantidad contada debe ser positiva")]
        public decimal? CantidadContada { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "La cantidad en stock debe ser positiva")]
        public decimal? CantidadStock { get; set; }
        
        [Required(ErrorMessage = "El código del usuario es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del usuario no puede exceder 50 caracteres")]
        public string UsuarioCodigo { get; set; } = string.Empty;
        
        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        public string? Comentario { get; set; }
    }
} 