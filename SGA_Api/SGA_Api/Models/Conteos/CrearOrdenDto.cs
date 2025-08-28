using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Conteos
{
    public class CrearOrdenDto
    {
        public int CodigoEmpresa { get; set; } = 1;
        
        [Required(ErrorMessage = "El título es obligatorio")]
        [StringLength(120, ErrorMessage = "El título no puede exceder 120 caracteres")]
        public string Titulo { get; set; } = string.Empty;
        
        [StringLength(10, ErrorMessage = "La visibilidad no puede exceder 10 caracteres")]
        public string? Visibilidad { get; set; }
        
        [StringLength(10, ErrorMessage = "El modo de generación no puede exceder 10 caracteres")]
        public string? ModoGeneracion { get; set; }
        
        [StringLength(20, ErrorMessage = "El alcance no puede exceder 20 caracteres")]
        public string? Alcance { get; set; }
        
        public string? FiltrosJson { get; set; }
        public DateTime? FechaPlan { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        
        [StringLength(50, ErrorMessage = "El código del supervisor no puede exceder 50 caracteres")]
        public string? SupervisorCodigo { get; set; }
        
        [Required(ErrorMessage = "El código del creador es obligatorio")]
        [StringLength(50, ErrorMessage = "El código del creador no puede exceder 50 caracteres")]
        public string CreadoPorCodigo { get; set; } = string.Empty;
        
        [Range(1, 5, ErrorMessage = "La prioridad debe estar entre 1 y 5")]
        public byte Prioridad { get; set; } = 3;
        
        [StringLength(50, ErrorMessage = "El código del operario no puede exceder 50 caracteres")]
        public string? CodigoOperario { get; set; }
        
        [StringLength(10, ErrorMessage = "El código del almacén no puede exceder 10 caracteres")]
        public string? CodigoAlmacen { get; set; }
        
        [StringLength(30, ErrorMessage = "El código de ubicación no puede exceder 30 caracteres")]
        public string? CodigoUbicacion { get; set; }
        
        [StringLength(30, ErrorMessage = "El código del artículo no puede exceder 30 caracteres")]
        public string? CodigoArticulo { get; set; }
        
        [StringLength(40, ErrorMessage = "El lote/partida no puede exceder 40 caracteres")]
        public string? LotePartida { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "La cantidad teórica debe ser positiva")]
        public decimal? CantidadTeorica { get; set; }
        
        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        public string? Comentario { get; set; }
    }
} 