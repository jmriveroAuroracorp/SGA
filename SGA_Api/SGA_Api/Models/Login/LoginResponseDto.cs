namespace SGA_Api.Models.Login
{
    public class LoginResponseDto
    {
        public int Operario { get; set; }
        public string? NombreOperario { get; set; }
        public List<short>? CodigosAplicacion { get; set; }
        public List<string>? CodigosAlmacen { get; set; }
        public List<string>? Empresas { get; set; }
        public string Token { get; set; } = string.Empty;

        public string? CodigoCentro { get; set; }
    }
}
