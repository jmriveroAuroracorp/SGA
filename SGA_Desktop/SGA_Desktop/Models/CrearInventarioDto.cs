using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para crear un nuevo inventario
    /// </summary>
    public class CrearInventarioDto
    {
        [JsonPropertyName("codigoInventario")]
        public string CodigoInventario { get; set; } = string.Empty;

        [JsonPropertyName("codigoEmpresa")]
        public int CodigoEmpresa { get; set; }

        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [JsonPropertyName("zona")]
        public string? Zona { get; set; }

        [JsonPropertyName("rangoUbicaciones")]
        public string? RangoUbicaciones { get; set; }

        [JsonPropertyName("tipoInventario")]
        public string TipoInventario { get; set; } = "TOTAL";

        [JsonPropertyName("comentarios")]
        public string? Comentarios { get; set; }

        [JsonPropertyName("usuarioCreacionId")]
        public int UsuarioCreacionId { get; set; }

        // Nuevas propiedades para rangos de ubicaciones
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
    /// DTO para registrar un conteo de inventario
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
    }
} 