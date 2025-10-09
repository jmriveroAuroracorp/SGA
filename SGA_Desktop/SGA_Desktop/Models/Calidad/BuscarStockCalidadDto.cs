namespace SGA_Desktop.Models.Calidad
{
    public class BuscarStockCalidadDto
    {
        public short CodigoEmpresa { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string Partida { get; set; } = string.Empty;
        public string? CodigoAlmacen { get; set; }
        public string? CodigoUbicacion { get; set; }
    }
}
