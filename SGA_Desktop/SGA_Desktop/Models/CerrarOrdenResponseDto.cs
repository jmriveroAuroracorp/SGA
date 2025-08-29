using System;

namespace SGA_Desktop.Models
{
    public class CerrarOrdenResponseDto
    {
        public long OrdenId { get; set; }
        public int TotalLecturas { get; set; }
        public int ResultadosCreados { get; set; }
        public DateTime FechaCierre { get; set; }

        // Propiedades adicionales para UI
        public string ResumenTexto => $"Orden #{OrdenId} cerrada con {TotalLecturas} lecturas y {ResultadosCreados} resultados creados";
        public bool TieneResultados => ResultadosCreados > 0;
        public string EstadoCierre => TieneResultados ? "Con diferencias" : "Sin diferencias";
    }
} 