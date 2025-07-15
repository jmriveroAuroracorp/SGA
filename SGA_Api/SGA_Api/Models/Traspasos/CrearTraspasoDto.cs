namespace SGA_Api.Models.Traspasos
{
	public class CrearTraspasoDto
	{
		public string AlmacenOrigen { get; set; }
		public string AlmacenDestino { get; set; }
		public Guid PaletId { get; set; }
		public int UsuarioInicioId { get; set; }
	}


}
