namespace SGA_Desktop.Models.Calidad
{
    public class DesbloquearStockDto
    {
        public Guid IdBloqueo { get; set; }
        public string ComentarioDesbloqueo { get; set; } = string.Empty;
        public int UsuarioId { get; set; }
    }
}
