using System.Collections.Generic;

namespace SGA_Api.Models.Pesaje
{
    public class PesajeOtDto
    {
        public string? CodigoArticuloOT { get; set; }
        public string? DescripcionArticuloOT { get; set; }
        public List<PesajeAmasijoDto> Amasijos { get; set; } = new();
    }
}