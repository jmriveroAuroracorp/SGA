namespace SGA_Api.Models.Palet
{
	public class PaletCrearDto
	{
		public short CodigoEmpresa { get; set; }
		public int UsuarioAperturaId { get; set; }
		public string Codigo { get; set; } = "";
		public string TipoPaletCodigo { get; set; } = "";
		public decimal Altura { get; set; }
		public decimal Peso { get; set; }
	}
}
