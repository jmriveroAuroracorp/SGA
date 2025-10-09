namespace SGA_Api.Models.Calidad
{
    public class BuscarStockCalidadDto
    {
        public short CodigoEmpresa { get; set; }      // OBLIGATORIO
        public string CodigoArticulo { get; set; } = string.Empty;  // OBLIGATORIO
        public string Partida { get; set; } = string.Empty;        // OBLIGATORIO
        public string? CodigoAlmacen { get; set; }                 // Opcional
        public string? CodigoUbicacion { get; set; }               // Opcional
    }
}
