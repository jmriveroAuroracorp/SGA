namespace SGA_Api.Models.Login
{
    public class OperarioEmpresa
    {
        public short CodigoEmpresa { get; set; }
        public int Operario { get; set; }
        public short EmpresaOrigen { get; set; }
        public string Empresa { get; set; } = string.Empty;
    }
}
