using SGA_Api.Models.Palet;
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
        public short CodigoEmpresa { get; set; }

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

        // Propiedades para rangos de ubicaciones
        [JsonPropertyName("pasilloDesde")]
        public int? PasilloDesde { get; set; }

        [JsonPropertyName("pasilloHasta")]
        public int? PasilloHasta { get; set; }

        [JsonPropertyName("estanteriaDesde")]
        public int? EstanteriaDesde { get; set; }

        [JsonPropertyName("estanteriaHasta")]
        public int? EstanteriaHasta { get; set; }

        [JsonPropertyName("alturaDesde")]
        public int? AlturaDesde { get; set; }

        [JsonPropertyName("alturaHasta")]
        public int? AlturaHasta { get; set; }

        [JsonPropertyName("posicionDesde")]
        public int? PosicionDesde { get; set; }

        [JsonPropertyName("posicionHasta")]
        public int? PosicionHasta { get; set; }

        [JsonPropertyName("incluirUnidadesCero")]
        public bool IncluirUnidadesCero { get; set; } = false;

        [JsonPropertyName("incluirArticulosConStockCero")]
        public bool IncluirArticulosConStockCero { get; set; } = false;

        [JsonPropertyName("incluirUbicacionesEspeciales")]
        public bool IncluirUbicacionesEspeciales { get; set; } = false;

        [JsonPropertyName("fechaInventario")]
        public DateTime FechaInventario { get; set; } = DateTime.Today.Date;

        // NUEVO: Filtro de artículo específico
        [JsonPropertyName("codigoArticuloFiltro")]
        public string? CodigoArticuloFiltro { get; set; }
    }

    /// <summary>
    /// DTO para líneas temporales de inventario con información adicional
    /// </summary>
    public class LineaTemporalInventarioDto
    {
        [JsonPropertyName("idTemp")]
        public Guid IdTemp { get; set; }

        [JsonPropertyName("idInventario")]
        public Guid IdInventario { get; set; }

        [JsonPropertyName("codigoArticulo")]
        public string CodigoArticulo { get; set; } = string.Empty;

        [JsonPropertyName("descripcionArticulo")]
        public string? DescripcionArticulo { get; set; }

        [JsonPropertyName("codigoUbicacion")]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [JsonPropertyName("partida")]
        public string? Partida { get; set; }

        [JsonPropertyName("fechaCaducidad")]
        public DateTime? FechaCaducidad { get; set; }

        [JsonPropertyName("cantidadContada")]
        public decimal? CantidadContada { get; set; }

        [JsonPropertyName("stockActual")]
        public decimal StockActual { get; set; }

        [JsonPropertyName("usuarioConteoId")]
        public int UsuarioConteoId { get; set; }

        [JsonPropertyName("fechaConteo")]
        public DateTime FechaConteo { get; set; }

        [JsonPropertyName("observaciones")]
        public string? Observaciones { get; set; }

        [JsonPropertyName("consolidado")]
        public bool Consolidado { get; set; }

        [JsonPropertyName("fechaConsolidacion")]
        public DateTime? FechaConsolidacion { get; set; }

        [JsonPropertyName("usuarioConsolidacionId")]
        public int? UsuarioConsolidacionId { get; set; }

        // === PROPIEDADES PARA INFORMACIÓN DE PALETS ===
        
        /// <summary>
        /// Información de los palets que contienen este stock
        /// </summary>
        [JsonPropertyName("palets")]
        public List<PaletDetalleDto> Palets { get; set; } = new();
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