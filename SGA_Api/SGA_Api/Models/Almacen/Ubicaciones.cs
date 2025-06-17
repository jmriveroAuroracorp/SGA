using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Almacen
{
	[Keyless]                                 // Le indicas a EF Core que no hay clave primaria
	[Table("Ubicaciones")]                    // Nombre real de la tabla (o de la vista) en la BD
	public class Ubicaciones
	{
		[Column("CodigoAlmacen")]              // Opcional, si el nombre ya coincide no hace falta
		public string CodigoAlmacen { get; set; } = null!;

		[Column("Ubicacion")]
		public string Ubicacion { get; set; } = null!;
	}
}