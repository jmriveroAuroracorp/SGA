using System.Collections.Generic;

namespace SGA_Api.Models.Pesaje
{
    public class PesajeResponseDto
    {
        public int EjercicioFabricacion { get; set; }
        public string SerieFabricacion { get; set; }
        public int NumeroFabricacion { get; set; }
        public decimal VNumeroAmasijos { get; set; }
        public List<PesajeOtDto> OrdenesTrabajo { get; set; } = new();
    }
}
