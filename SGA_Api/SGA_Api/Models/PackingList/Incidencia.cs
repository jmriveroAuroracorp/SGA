namespace SGA_Api.Models.PackingList
{
    /// <summary>
    /// Entidad que mapea AURORA.dbo.Incidencias.
    /// Consulta por CodigoEmpresa, EjercicioTrabajo, NumeroTrabajo para obtener Partida(s).
    /// Puede haber una o más filas por orden de trabajo.
    /// </summary>
    public class Incidencia
    {
        public short CodigoEmpresa { get; set; }
        public short EjercicioTrabajo { get; set; }
        public int NumeroTrabajo { get; set; }
        public string Partida { get; set; } = null!;
    }
}
