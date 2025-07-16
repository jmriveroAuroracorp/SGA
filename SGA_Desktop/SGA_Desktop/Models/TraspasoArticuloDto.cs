namespace SGA_Desktop.Models
{
    public class TraspasoArticuloDto
    {
        public Guid Id { get; set; }
        public string AlmacenOrigen { get; set; }
        public string UbicacionOrigen { get; set; }
        public string AlmacenDestino { get; set; }
        public string UbicacionDestino { get; set; }
        public int UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public string CodigoArticulo { get; set; }
        public decimal Cantidad { get; set; }
        public string Estado { get; set; }
        public string Origen => $"{AlmacenOrigen} / {UbicacionOrigen}";
        public string Destino => $"{AlmacenDestino} / {UbicacionDestino}";
    }
} 