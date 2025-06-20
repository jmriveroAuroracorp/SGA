namespace SGA_Api.Models.Almacen
{
	public class AlmacenDto
	{
		public string CodigoAlmacen { get; set; } = null!;
		public string NombreAlmacen { get; set; } = null!;
		public short CodigoEmpresa { get; set; }  // ← nuevo campo
		public bool EsDelCentro { get; set; } = false; // ← opcional
	}
}
