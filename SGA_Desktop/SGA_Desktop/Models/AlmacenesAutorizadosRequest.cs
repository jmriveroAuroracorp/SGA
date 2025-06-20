namespace SGA_Desktop.Models
{
	public class AlmacenesAutorizadosRequest
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoCentro { get; set; } = "";
		public List<string> CodigosAlmacen { get; set; } = new();
	}
}
