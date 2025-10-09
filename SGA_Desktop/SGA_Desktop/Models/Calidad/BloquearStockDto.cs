namespace SGA_Desktop.Models.Calidad
{
    public class BloquearStockDto
    {
        public short CodigoEmpresa { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string LotePartida { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public string ComentarioBloqueo { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
    }
}
