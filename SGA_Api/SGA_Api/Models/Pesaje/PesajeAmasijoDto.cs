using System.Collections.Generic;

namespace SGA_Api.Models.Pesaje
{
    public class PesajeAmasijoDto
    {
        public string Amasijo { get; set; } = "Sin amasijo"; // o el número de amasijo
        public decimal TotalPesado { get; set; }
        public List<PesajeComponenteDto> Componentes { get; set; } = new();
    }
}
