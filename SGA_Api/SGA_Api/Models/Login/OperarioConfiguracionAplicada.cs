using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Login
{
    /// <summary>
    /// Modelo para la tabla OperariosConfiguracionesAplicadas
    /// </summary>
    public class OperarioConfiguracionAplicada
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int OperarioId { get; set; }
        
        [Required]
        public int ConfiguracionPredefinidaId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ConfiguracionPredefinidaNombre { get; set; } = string.Empty;
        
        [Required]
        public DateTime FechaAplicacion { get; set; }
        
        [Required]
        public int UsuarioAplicacion { get; set; }
        
        [Required]
        public bool Activa { get; set; } = true;
        
        // Navegación
        [ForeignKey("ConfiguracionPredefinidaId")]
        public virtual ConfiguracionPredefinida ConfiguracionPredefinida { get; set; } = null!;
    }
}
