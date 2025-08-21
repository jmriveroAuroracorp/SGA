using System.Text.Json.Serialization;

namespace SGA_Api.Models.Inventario
{
    /// <summary>
    /// DTO para crear un nuevo inventario (cabecera)
    /// </summary>
    public class CrearInventarioDto
    {
        [JsonPropertyName("codigoInventario")]
        public string CodigoInventario { get; set; } = string.Empty;

        [JsonPropertyName("codigoEmpresa")]
        public int CodigoEmpresa { get; set; }

        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [JsonPropertyName("rangoUbicaciones")]
        public string? RangoUbicaciones { get; set; }

        [JsonPropertyName("tipoInventario")]
        public string TipoInventario { get; set; } = "TOTAL"; // TOTAL o PARCIAL

        [JsonPropertyName("comentarios")]
        public string? Comentarios { get; set; }

        [JsonPropertyName("usuarioCreacionId")]
        public int UsuarioCreacionId { get; set; }
    }

    /// <summary>
    /// DTO para registrar un conteo individual de inventario
    /// </summary>
    public class ContarInventarioDto
    {
        [JsonPropertyName("idInventario")]
        public Guid IdInventario { get; set; }

        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;

        [JsonPropertyName("codigoUbicacion")]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [JsonPropertyName("cantidadContada")]
        public decimal CantidadContada { get; set; }

        [JsonPropertyName("usuarioConteoId")]
        public int UsuarioConteoId { get; set; }

        [JsonPropertyName("observaciones")]
        public string? Observaciones { get; set; }
    }

    /// <summary>
    /// DTO para filtrar inventarios
    /// </summary>
    public class FiltroInventarioDto
    {
        [JsonPropertyName("codigoEmpresa")]
        public int CodigoEmpresa { get; set; }

        [JsonPropertyName("codigoAlmacen")]
        public string? CodigoAlmacen { get; set; }

        [JsonPropertyName("codigosAlmacen")]
        public List<string>? CodigosAlmacen { get; set; }

        [JsonPropertyName("estadoInventario")]
        public string? EstadoInventario { get; set; }

        [JsonPropertyName("fechaDesde")]
        public DateTime? FechaDesde { get; set; }

        [JsonPropertyName("fechaHasta")]
        public DateTime? FechaHasta { get; set; }

        [JsonPropertyName("tipoInventario")]
        public string? TipoInventario { get; set; }
    }

    /// <summary>
    /// DTO para respuesta de inventario
    /// </summary>
    public class InventarioResponseDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigoEmpresa")]
        public int CodigoEmpresa { get; set; }

        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;

        [JsonPropertyName("descripcionArticulo")]
        public string? DescripcionArticulo { get; set; }

        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [JsonPropertyName("almacen")]
        public string Almacen { get; set; } = string.Empty;

        [JsonPropertyName("ubicacion")]
        public string Ubicacion { get; set; } = string.Empty;

        [JsonPropertyName("partida")]
        public string Partida { get; set; } = string.Empty;

        [JsonPropertyName("fechaCaducidad")]
        public DateTime? FechaCaducidad { get; set; }

        [JsonPropertyName("stockSistema")]
        public decimal StockSistema { get; set; }

        [JsonPropertyName("stockFisico")]
        public decimal StockFisico { get; set; }

        [JsonPropertyName("diferencia")]
        public decimal Diferencia => StockFisico - StockSistema;

        [JsonPropertyName("observaciones")]
        public string? Observaciones { get; set; }

        [JsonPropertyName("fechaInventario")]
        public DateTime FechaInventario { get; set; }

        [JsonPropertyName("usuarioInventario")]
        public string UsuarioInventario { get; set; } = string.Empty;

        [JsonPropertyName("estadoInventario")]
        public string EstadoInventario { get; set; } = string.Empty;

        [JsonPropertyName("codigoPalet")]
        public string? CodigoPalet { get; set; }

        [JsonPropertyName("estadoPalet")]
        public string? EstadoPalet { get; set; }

        [JsonPropertyName("alergenos")]
        public string Alergenos { get; set; } = string.Empty;

        [JsonPropertyName("codigoAlternativo")]
        public string CodigoAlternativo { get; set; } = string.Empty;

        [JsonPropertyName("tieneDiferencia")]
        public bool TieneDiferencia => Math.Abs(Diferencia) > 0.01m;
    }
} 