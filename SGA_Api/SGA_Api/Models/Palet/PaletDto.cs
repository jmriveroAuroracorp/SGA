using System;

namespace SGA_Api.Models.Palet
{
	public class PaletDto
	{
		public Guid Id { get; set; }
		public short CodigoEmpresa { get; set; }
		public string Codigo { get; set; } = "";
		public string Estado { get; set; } = "";
		public string? TipoPaletCodigo { get; set; }
		public DateTime FechaApertura { get; set; }
		public DateTime? FechaCierre { get; set; }
		public int? UsuarioAperturaId { get; set; }
		public string? UsuarioAperturaNombre { get; set; }
		public int? UsuarioCierreId { get; set; }
		public string? UsuarioCierreNombre { get; set; }
		public decimal? Altura { get; set; }
		public decimal? Peso { get; set; }
		public bool EtiquetaGenerada { get; set; }
		public bool IsVaciado { get; set; }
		public DateTime? FechaVaciado { get; set; }
		public string? OrdenTrabajoId { get; set; } = "";
	}
}
