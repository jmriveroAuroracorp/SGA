namespace SGA_Api.Models.Almacen
{
	public class AlmacenesAutorizadosDto
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoCentro { get; set; } = "";
		public List<string> CodigosAlmacen { get; set; } = new();
	}
}
