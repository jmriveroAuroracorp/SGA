namespace SGA_Api.Models.Palet
{
    public class CerrarPaletMobilityDto
    {
        public int UsuarioId { get; set; }
        public string CodigoAlmacen { get; set; } = ""; // almacén origen
        public short CodigoEmpresa { get; set; }
        public string? Comentario { get; set; }
        public decimal? Altura { get; set; }
        public decimal? Peso { get; set; }
    }
} 