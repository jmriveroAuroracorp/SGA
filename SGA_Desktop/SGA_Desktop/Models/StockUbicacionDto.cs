using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para el grid de stock de ubicaciones en inventario
    /// </summary>
    public class StockUbicacionDto
    {
        [JsonPropertyName("codigoEmpresa")]
        public string? CodigoEmpresa { get; set; }

        [JsonPropertyName("codigoArticulo")]
        public string? CodigoArticulo { get; set; }

        [JsonPropertyName("descripcionArticulo")]
        public string? DescripcionArticulo { get; set; }

        [JsonPropertyName("codigoAlternativo")]
        public string? CodigoAlternativo { get; set; }

        [JsonPropertyName("codigoAlternativo2")]
        public string? CodigoAlternativo2 { get; set; }

        [JsonPropertyName("referenciaEdi_")]
        public string? ReferenciaEdi_ { get; set; }

        [JsonPropertyName("mrhCodigoAlternativo3")]
        public string? MRHCodigoAlternativo3 { get; set; }

        [JsonPropertyName("vCodigoDUN14")]
        public string? VCodigoDUN14 { get; set; }

        [JsonPropertyName("codigoCentro")]
        public string? CodigoCentro { get; set; }

        [JsonPropertyName("codigoAlmacen")]
        public string? CodigoAlmacen { get; set; }

        [JsonPropertyName("almacen")]
        public string? Almacen { get; set; }

        [JsonPropertyName("ubicacion")]
        public string? Ubicacion { get; set; }

        [JsonPropertyName("partida")]
        public string? Partida { get; set; }

        [JsonPropertyName("fechaCaducidad")]
        public DateTime? FechaCaducidad { get; set; }

        [JsonPropertyName("unidadSaldo")]
        public decimal? UnidadSaldo { get; set; }

        [JsonPropertyName("paletId")]
        public Guid? PaletId { get; set; }

        [JsonPropertyName("codigoPalet")]
        public string? CodigoPalet { get; set; }

        [JsonPropertyName("estaPaletizado")]
        public bool EstaPaletizado => !string.IsNullOrEmpty(CodigoPalet);

        [JsonPropertyName("estadoPalet")]
        public string? EstadoPalet { get; set; }

        [JsonPropertyName("palets")]
        public List<PaletDetalleDto> Palets { get; set; } = new();

        [JsonPropertyName("totalArticuloGlobal")]
        public decimal? TotalArticuloGlobal { get; set; }

        [JsonPropertyName("totalArticuloAlmacen")]
        public decimal? TotalArticuloAlmacen { get; set; }
    }

    /// <summary>
    /// DTO para los rangos disponibles de ubicaciones
    /// </summary>
    public class RangosDisponiblesDto
    {
        [JsonPropertyName("pasillos")]
        public List<int> Pasillos { get; set; } = new();

        [JsonPropertyName("estanterias")]
        public List<int> Estanterias { get; set; } = new();

        [JsonPropertyName("alturas")]
        public List<int> Alturas { get; set; } = new();

        [JsonPropertyName("posiciones")]
        public List<int> Posiciones { get; set; } = new();

        [JsonPropertyName("totalUbicaciones")]
        public int TotalUbicaciones { get; set; }
    }
} 