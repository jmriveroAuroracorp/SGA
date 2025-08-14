namespace SGA_Api.Models.Traspasos
{
    public class MoverPaletDto
    {
        public Guid PaletId { get; set; }
        public int UsuarioId { get; set; }
        public string? AlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public string? CodigoPalet { get; set; }
        public string? CodigoEstado { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public int UsuarioFinalizacionId { get; set; }
        public short CodigoEmpresa { get; set; }
        public DateTime? FechaInicio { get; set; }
        public string TipoTraspaso { get; set; }
        public string? Comentario { get; set; }
        public string? EstadoErp { get; set; }
    }
} 