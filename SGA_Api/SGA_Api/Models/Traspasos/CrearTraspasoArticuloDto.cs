namespace SGA_Api.Models.Traspasos
{
    public class CrearTraspasoArticuloDto
    {
        public string AlmacenOrigen { get; set; }
        public string UbicacionOrigen { get; set; }
        public string CodigoArticulo { get; set; }
        public decimal? Cantidad { get; set; }
        public int UsuarioId { get; set; }
        public short CodigoEmpresa { get; set; }
        // Para escritorio (finalización en una fase)
        public string? AlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        // true = escritorio (finaliza), false = mobility (pendiente)
        public bool? Finalizar { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public string Partida { get; set; }
        public Guid? MovPosicionOrigen { get; set; } = null;
        public Guid? MovPosicionDestino { get; set; } = null;
        public DateTime? FechaInicio { get; set; }
		public string? DescripcionArticulo { get; set; }
		public string? UnidadMedida { get; set; }
		public string? Observaciones { get; set; }

		public bool? ReabrirSiCerradoOrigen { get; set; }  // default null/false
	}
} 