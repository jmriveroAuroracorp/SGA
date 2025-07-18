namespace SGA_Desktop.Models
{
    public class CrearTraspasoArticuloDto
    {
        public string AlmacenOrigen { get; set; }
        public string UbicacionOrigen { get; set; }
        public string CodigoArticulo { get; set; }
        public decimal? Cantidad { get; set; }
        public int UsuarioId { get; set; }
        public string? AlmacenDestino { get; set; }
        public string? UbicacionDestino { get; set; }
        public bool? Finalizar { get; set; }
        public DateTime? FechaCaducidad { get; set; }
        public string Partida { get; set; }
        public short CodigoEmpresa { get; set; }
        public DateTime? FechaInicio { get; set; }
    }
} 