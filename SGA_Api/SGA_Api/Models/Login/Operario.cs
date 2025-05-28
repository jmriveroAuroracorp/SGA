using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Login
{
    public class Operario
    {
        [Column("Operario")] // Mapea con la columna real de la BD
        public int Id { get; set; }                // ID del operario
        [Column("NombreOperario")]
        public string Nombre { get; set; }               // Nombre del operario
        public string Contraseña { get; set; }           // Contraseña en texto plano
        public DateTime? FechaBaja { get; set; }         // Si no es null, el operario está de baja
    }
}
