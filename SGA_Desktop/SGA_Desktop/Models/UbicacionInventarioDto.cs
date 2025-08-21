using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para mostrar una ubicación en el inventario
    /// </summary>
    public class UbicacionInventarioDto
    {
        [JsonPropertyName("codigoUbicacion")]
        public string CodigoUbicacion { get; set; } = string.Empty;

        [JsonPropertyName("pasillo")]
        public int Pasillo { get; set; }

        [JsonPropertyName("estanteria")]
        public int Estanteria { get; set; }

        [JsonPropertyName("altura")]
        public int Altura { get; set; }

        [JsonPropertyName("posicion")]
        public int Posicion { get; set; }

        [JsonPropertyName("tieneStock")]
        public bool TieneStock { get; set; }

        [JsonPropertyName("cantidadStock")]
        public decimal CantidadStock { get; set; }

        [JsonPropertyName("articulos")]
        public int CantidadArticulos { get; set; }

        [JsonIgnore]
        public string Descripcion => $"Pasillo {Pasillo}, Estantería {Estanteria}, Altura {Altura}, Posición {Posicion}";

        [JsonIgnore]
        public string StockInfo => TieneStock ? $"{CantidadStock} uds. ({CantidadArticulos} artículos)" : "Sin stock";
    }
} 