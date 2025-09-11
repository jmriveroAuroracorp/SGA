namespace SGA_Api.Models.Traspasos
{
    public class FinalizarTraspasoArticuloDto
    {
        public string AlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public int UsuarioId { get; set; }

		public bool? ConfirmarAgregarAPalet { get; set; }
		public Guid? PaletIdConfirmado { get; set; }
	}
} 