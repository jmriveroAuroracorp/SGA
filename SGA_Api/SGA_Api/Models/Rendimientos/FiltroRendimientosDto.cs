namespace SGA_Api.Models.Rendimientos
{
    public class FiltroRendimientosDto
    {
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public int? OperarioId { get; set; }
        public string? TipoProceso { get; set; } // "TRASPASOS", "INVENTARIOS", "CONTEO", "PALETS"
        public short? CodigoEmpresa { get; set; }
        public string? CodigoAlmacen { get; set; }
    }
}

