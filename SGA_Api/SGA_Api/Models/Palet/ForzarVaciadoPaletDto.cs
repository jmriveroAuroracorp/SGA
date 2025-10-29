using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Palet
{
	/// <summary>
	/// DTO para forzar el vaciado de un palet
	/// </summary>
	public class ForzarVaciadoPaletDto
	{
		/// <summary>
		/// ID del usuario que realiza el vaciado forzado
		/// </summary>
		[Required(ErrorMessage = "El UsuarioId es obligatorio")]
		public int UsuarioId { get; set; }
	}
}
