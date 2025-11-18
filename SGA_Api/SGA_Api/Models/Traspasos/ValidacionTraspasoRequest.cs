using System.ComponentModel.DataAnnotations;

namespace SGA_Api.Models.Traspasos
{
    /// <summary>
    /// Request para validar traspaso de artículo individual
    /// </summary>
    public class ValidacionTraspasoRequest
    {
        [Required]
        public string CodigoArticulo { get; set; } = string.Empty;
        
        [Required]
        public string AlmacenDestino { get; set; } = string.Empty;
        
        // 🔷 CORREGIDO: Permitir ubicación vacía (SIN UBICAR)
        public string UbicacionDestino { get; set; } = string.Empty;
        
        public short CodigoEmpresa { get; set; }
        
        // 🔷 NUEVO: Partida/Lote para validar bloqueo específico
        public string? Partida { get; set; }
    }
}
