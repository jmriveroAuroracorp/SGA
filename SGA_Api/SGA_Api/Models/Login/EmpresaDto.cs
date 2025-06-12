namespace SGA_Api.Models.Login
{
	public record EmpresaDto
	{
		public int Codigo { get; init; }
		public string Nombre { get; init; } = string.Empty;
	}

}
