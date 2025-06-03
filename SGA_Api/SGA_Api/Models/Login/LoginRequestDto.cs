namespace SGA_Api.Models.Login
{
    public class LoginRequestDto
    {
        public required int Operario { get; set; }
        public required string Contraseña { get; set; }
        public string IdDispositivo { get; set; } = null!;

    }
}
