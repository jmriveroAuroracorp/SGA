using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Login
{
    public class Operario
    {
        [Column("Operario")] // Mapea con la columna real de la BD
        public int Id { get; set; }                // ID del operario
        [Column("NombreOperario")]
        public string? Nombre { get; set; }               // Nombre del operario
        public required string Contraseña { get; set; }           // Contraseña en texto plano
        public DateTime? FechaBaja { get; set; }         // Si no es null, el operario está de baja

        public string? CodigoCentro { get; set; }
        
        [Column("MRH_LimiteInventarioEuros")]
        public decimal? MRH_LimiteInventarioEuros { get; set; }  // Límite de inventario en euros

        [Column("MRH_LimiteInventarioUnidades")]
        public decimal? MRH_LimiteInventarioUnidades { get; set; }  // Límite de inventario en unidades
    }
}
