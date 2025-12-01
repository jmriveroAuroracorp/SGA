namespace SGA_Desktop.Models.Calidad
{
    public class BloquearStockDto
    {
        public short CodigoEmpresa { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string LotePartida { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public string ComentarioBloqueo { get; set; } = string.Empty;
        public string TipoBloqueo { get; set; } = "TOTAL"; // "TOTAL" = bloquea todos los traspasos, "SOLO_PULMON" = solo bloquea a PULMÓN
        public int UsuarioId { get; set; }
        public bool EsBloqueoGlobal { get; set; } = false; // 🔷 NUEVO: Indica si es bloqueo en todas las ubicaciones
    }
}
