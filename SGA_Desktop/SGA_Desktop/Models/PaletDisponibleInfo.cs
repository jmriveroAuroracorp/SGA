using System;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// Información de un palet disponible en una ubicación
    /// </summary>
    public class PaletDisponibleInfo
    {
        public Guid PaletId { get; set; }
        public string CodigoPalet { get; set; } = string.Empty;
        public string? CodigoGS1 { get; set; }
        public decimal Cantidad { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
