namespace SGA_Desktop.Models.Calidad
{
    public class StockCalidadDto
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Almacen { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public string LotePartida { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadDisponible { get; set; }
        public bool EstaBloqueado { get; set; }
        public string? ComentarioBloqueo { get; set; }
        public DateTime? FechaBloqueo { get; set; }
        public string? UsuarioBloqueo { get; set; }
        public string Estado { get; set; } = "Disponible";
    }
}
