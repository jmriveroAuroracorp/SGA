namespace SGA_Api.Models.Palet
{
	public class CerrarPaletDto
	{
		public int UsuarioId { get; set; }
		public string CodigoAlmacen { get; set; } = "";           // almacén actual (origen), de las líneas
		public string CodigoAlmacenDestino { get; set; } = "";   // almacén destino, seleccionado por el usuario
		public string UbicacionDestino { get; set; } = "";       // ubicación dentro del destino
		public string? TipoTraspaso { get; set; }
		public string? CodigoEstado { get; set; }
		public DateTime? FechaFinalizacion { get; set; }
		public int? UsuarioFinalizacionId { get; set; }
		public short CodigoEmpresa { get; set; }
	}
}
