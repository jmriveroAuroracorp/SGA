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
		public string? Comentario { get; set; }

		public bool? ReabrirSiCerradoOrigen { get; set; }  // default null/false
		
		/// <summary>
		/// Opcional: ID del palet destino donde se quiere añadir el artículo.
		/// Si se especifica, se usará ese palet. Si no, se busca/crea automáticamente.
		/// </summary>
		public Guid? PaletIdDestino { get; set; }
		
		/// <summary>
		/// Opcional: ID del palet origen de donde se quiere extraer el stock.
		/// Si el usuario seleccionó stock desde un palet específico, este campo lo indica.
		/// Si se especifica, se usará este palet para crear la línea negativa.
		/// </summary>
		public Guid? PaletIdOrigen { get; set; }

		/// <summary>
		/// Si true, confirma agregar el artículo al palet existente
		/// </summary>
		public bool? ConfirmarAgregarAPalet { get; set; }

		/// <summary>
		/// Si true, deja el material suelto en la ubicación (sin paletizar)
		/// </summary>
		public bool? DejarSuelto { get; set; }
	}
} 