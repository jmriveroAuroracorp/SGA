namespace SGA_Api.Models.Login
{
    public class LoginResponseDto
    {
        public int Operario { get; set; }
        public string? NombreOperario { get; set; }
        public List<short>? CodigosAplicacion { get; set; }
    }
}
