namespace SGA_Api.Models.Pesaje
{
    public class PesajeSqlRawDto
    {
        public int EjercicioFabricacion { get; set; }
        public string SerieFabricacion { get; set; }
        public int NumeroFabricacion { get; set; }
        public string CodigoArticuloOT { get; set; }
        public string DescripcionArticuloOT { get; set; }
        public string? ArticuloComponente { get; set; }
        public Guid? IdAmasijo { get; set; }
        public string? NumeroAmasijo { get; set; }
        public decimal VNumeroAmasijos { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? Partida { get; set; }
        public string? FechaCaduca { get; set; }
        public decimal UnidadesComponente { get; set; }
    }
}
