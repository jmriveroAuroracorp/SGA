using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para representar la cabecera de un inventario
    /// </summary>
    public class InventarioCabeceraDto : INotifyPropertyChanged
    {
        [JsonPropertyName("idInventario")]
        public Guid IdInventario { get; set; }

        [JsonPropertyName("codigoInventario")]
        public string CodigoInventario { get; set; } = string.Empty;

        [JsonPropertyName("codigoEmpresa")]
        public int CodigoEmpresa { get; set; }

        [JsonPropertyName("codigoAlmacen")]
        public string CodigoAlmacen { get; set; } = string.Empty;

        [JsonPropertyName("rangoUbicaciones")]
        public string? RangoUbicaciones { get; set; }

        [JsonPropertyName("tipoInventario")]
        public string TipoInventario { get; set; } = "TOTAL";

        [JsonPropertyName("comentarios")]
        public string? Comentarios { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; } = "ABIERTO";

        [JsonPropertyName("usuarioCreacionId")]
        public int UsuarioCreacionId { get; set; }

        [JsonPropertyName("usuarioCreacionNombre")]
        public string? UsuarioCreacionNombre { get; set; }

        [JsonPropertyName("usuarioProcesamientoId")]
        public int? UsuarioProcesamientoId { get; set; }

        [JsonPropertyName("usuarioProcesamientoNombre")]
        public string? UsuarioProcesamientoNombre { get; set; }

        [JsonPropertyName("fechaCreacion")]
        public DateTime FechaCreacion { get; set; }

        [JsonPropertyName("fechaCierre")]
        public DateTime? FechaCierre { get; set; }

        // Propiedades calculadas para la UI
        [JsonIgnore]
        public string EstadoFormateado => Estado switch
        {
            "ABIERTO" => "Abierto",
            "PENDIENTE_CIERRE" => "Pendiente de Cierre",
            "CERRADO" => "Cerrado",
            _ => Estado
        };

        [JsonIgnore]
        public string TipoInventarioFormateado => TipoInventario switch
        {
            "TOTAL" => "Total",
            "PARCIAL" => "Parcial",
            _ => TipoInventario
        };

        [JsonIgnore]
        public bool PuedeContar => Estado == "ABIERTO";

        [JsonIgnore]
        public bool PuedeConsolidar => Estado == "ABIERTO";

        [JsonIgnore]
        public bool PuedeCerrar => Estado == "PENDIENTE_CIERRE";

        [JsonIgnore]
        public string IdInventarioCorto => CodigoInventario; // Ahora usa el código personalizado

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 