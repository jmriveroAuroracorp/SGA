namespace SGA_Api.Models.Almacen
{
	public class AlmacenesAutorizadosDto
	{
		public short CodigoEmpresa { get; set; }
		public string CodigoCentro { get; set; } = "";
		public List<string> CodigosAlmacen { get; set; } = new();
		public int? OperarioId { get; set; } // Opcional: si viene, consultar OperariosAlmacenes directamente
	}
}
