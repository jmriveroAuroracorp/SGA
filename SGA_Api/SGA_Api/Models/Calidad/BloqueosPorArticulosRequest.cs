namespace SGA_Api.Models.Calidad
{
    /// <summary>
    /// Request para consultar bloqueos de calidad por lista de artículos
    /// </summary>
    public class BloqueosPorArticulosRequest
    {
        public short CodigoEmpresa { get; set; }
        public List<string> CodigosArticulos { get; set; } = new();
    }
}
