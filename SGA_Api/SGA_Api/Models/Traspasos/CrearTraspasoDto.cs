namespace SGA_Api.Models.Traspasos
{
	public class CrearTraspasoDto
	{
		public string AlmacenOrigen { get; set; }
		public string UbicacionOrigen { get; set; }
		public Guid PaletId { get; set; }
		public int UsuarioInicioId { get; set; }
		public string CodigoPalet { get; set; }
	}
}
