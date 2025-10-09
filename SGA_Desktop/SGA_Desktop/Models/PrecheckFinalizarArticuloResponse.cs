using System;
using System.Collections.Generic;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// Respuesta del endpoint precheck-finalizar que devuelve palets disponibles en una ubicación
    /// </summary>
    public class PrecheckFinalizarArticuloResponse
    {
        public bool Existe { get; set; }
        public int CantidadPalets { get; set; }
        public List<PaletPrecheck> Palets { get; set; } = new();
        public string Aviso { get; set; }
        
        // Compatibilidad con respuesta anterior (primer palet)
        public Guid? PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public bool Cerrado { get; set; }
    }

    public class PaletPrecheck
    {
        public Guid PaletId { get; set; }
        public string CodigoPalet { get; set; }
        public string Estado { get; set; }
        public bool Cerrado { get; set; }
        public DateTime FechaApertura { get; set; }
        public DateTime? FechaCierre { get; set; }
        public string Descripcion { get; set; }
    }
}

