using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Palet
{
	[Table("Palets")]
	public class Palet
	{
		[Key]
		public Guid Id { get; set; }

		[Required]
		public short CodigoEmpresa { get; set; }

		[Required, MaxLength(100)]
		public string Codigo { get; set; } = "";

		[Required, MaxLength(50)]
		public string Estado { get; set; } = "";

		[MaxLength(10)]
		public string? TipoPaletCodigo { get; set; }

		[Required]
		public DateTime FechaApertura { get; set; }

		public DateTime? FechaCierre { get; set; }

		[Required]
		public int UsuarioAperturaId { get; set; }

		public int? UsuarioCierreId { get; set; }

		public decimal? Altura { get; set; }
		public decimal? Peso { get; set; }

		[Required]
		public bool EtiquetaGenerada { get; set; }

		[Required]
		public bool IsVaciado { get; set; }

		public DateTime? FechaVaciado { get; set; }

		// relaciones a PaletLineas y log_palet quedan aparte...
	}
}
