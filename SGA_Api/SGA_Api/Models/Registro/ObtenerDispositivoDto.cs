namespace SGA_Api.Models.Registro
{
    public class ObtenerDispositivoDto
    {
        public required string Id { get; set; }
        public string NombreOperario { get; set; } = null!;
        public string Tipo { get; set; } = null!;
    }
}
