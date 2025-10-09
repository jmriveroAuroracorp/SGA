using System;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para mostrar palets disponibles en un ComboBox de selección
    /// </summary>
    public class PaletDisponibleDto
    {
        public Guid PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public string Estado { get; set; }
        public bool Cerrado { get; set; }
        
        /// <summary>
        /// Descripción formateada para mostrar en el ComboBox
        /// Ejemplo: "PAL25-0000029 - ABIERTO" o "PAL25-0000030 - CERRADO (se reabrirá)"
        /// </summary>
        public string Descripcion { get; set; }
    }
}

