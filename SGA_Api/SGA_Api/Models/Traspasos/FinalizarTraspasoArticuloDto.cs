using System.Text.Json.Serialization;

namespace SGA_Api.Models.Traspasos
{
    public class FinalizarTraspasoArticuloDto
    {
        [JsonPropertyName("almacenDestino")]
        public string AlmacenDestino { get; set; }
        
        [JsonPropertyName("ubicacionDestino")]
        public string? UbicacionDestino { get; set; }
        
        [JsonPropertyName("usuarioId")]
        public int UsuarioId { get; set; }

        [JsonPropertyName("ConfirmarAgregarAPalet")]
		public bool? ConfirmarAgregarAPalet { get; set; }
        
        [JsonPropertyName("DejarSuelto")]
		public bool? DejarSuelto { get; set; }
        
        [JsonPropertyName("PaletIdConfirmado")]
		public Guid? PaletIdConfirmado { get; set; }
	}
} 