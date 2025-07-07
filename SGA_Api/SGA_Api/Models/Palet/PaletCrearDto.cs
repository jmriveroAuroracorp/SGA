namespace SGA_Api.Models.Palet
{
	public class PaletCrearDto
	{
		public short CodigoEmpresa { get; set; }
		public int UsuarioAperturaId { get; set; }
		public string TipoPaletCodigo { get; set; } = "";
		public string? OrdenTrabajoId { get; set; }
	}
}
