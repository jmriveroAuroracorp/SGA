namespace SGA_Desktop.Models.Calidad
{
    public class DesbloquearStockDto
    {
        public Guid? IdBloqueo { get; set; } // 🔷 MODIFICADO: Ahora es opcional para permitir desbloqueo global
        
        // 🔷 NUEVO: Campos para desbloqueo global (se usan si IdBloqueo es null)
        public short? CodigoEmpresa { get; set; }
        public string? CodigoArticulo { get; set; }
        public string? LotePartida { get; set; }
        
        public string ComentarioDesbloqueo { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
        public bool EsDesbloqueoGlobal { get; set; } = false; // 🔷 NUEVO: Indica si es desbloqueo en todas las ubicaciones
    }
}
