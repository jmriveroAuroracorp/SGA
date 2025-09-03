using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        // Nuevas propiedades para información expandida
        [JsonPropertyName("conteoACiegas")]
        public bool? ConteoACiegas { get; set; }

        [JsonPropertyName("articuloDesde")]
        public string? ArticuloDesde { get; set; }

        [JsonPropertyName("articuloHasta")]
        public string? ArticuloHasta { get; set; }

        [JsonPropertyName("codigoArticuloFiltro")]
        public string? CodigoArticuloFiltro { get; set; }

        [JsonPropertyName("totalLineas")]
        public int? TotalLineas { get; set; }

        [JsonPropertyName("lineasContadas")]
        public int? LineasContadas { get; set; }

        [JsonPropertyName("incluirUbicacionesEspeciales")]
        public bool? IncluirUbicacionesEspeciales { get; set; }

        // Propiedades calculadas para la UI
        [JsonIgnore]
        public string EstadoFormateado => Estado switch
        {
            "ABIERTO" => "Abierto",
            "EN_CONTEO" => "En Conteo",
            "CONSOLIDADO" => "Consolidado",
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
        public bool PuedeConsolidar => Estado == "ABIERTO" || Estado == "EN_CONTEO";

        [JsonIgnore]
        public bool PuedeCerrar => Estado == "CONSOLIDADO" || Estado == "PENDIENTE_CIERRE";

        [JsonIgnore]
        public string IdInventarioCorto => CodigoInventario; // Ahora usa el código personalizado

        // Propiedades para el estado expandido
        private bool _isExpanded = false;
        [JsonIgnore]
        public bool IsExpanded 
        { 
            get => _isExpanded; 
            set 
            { 
                _isExpanded = value; 
                OnPropertyChanged(); 
            } 
        }

        // Propiedades calculadas para información expandida
        [JsonIgnore]
        public string ConteoTipo => ConteoACiegas == true ? "Inicializado a 0" : "Normal";

        [JsonIgnore]
        public string RangoArticulos 
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CodigoArticuloFiltro))
                    return $"Artículo específico: {CodigoArticuloFiltro}";
                if (!string.IsNullOrWhiteSpace(ArticuloDesde) && !string.IsNullOrWhiteSpace(ArticuloHasta))
                    return $"Rango: {ArticuloDesde} → {ArticuloHasta}";
                return "Todos los artículos";
            }
        }

        // Simplificar ProgresoCounting para mostrar solo "x de x líneas"
        [JsonIgnore]
        public string ProgresoCounting 
        {
            get
            {
                if (TotalLineas.HasValue && LineasContadas.HasValue)
                {
                    return $"{LineasContadas.Value} de {TotalLineas.Value} líneas";
                }
                return "Sin datos disponibles";
            }
        }

        [JsonIgnore]
        public double PorcentajeProgreso
        {
            get
            {
                if (TotalLineas.HasValue && LineasContadas.HasValue && TotalLineas.Value > 0)
                {
                    return (LineasContadas.Value * 100.0 / TotalLineas.Value);
                }
                return 0;
            }
        }

        [JsonIgnore]
        public string ProgresoTexto
        {
            get
            {
                if (TotalLineas.HasValue && LineasContadas.HasValue)
                {
                    return $"{LineasContadas.Value}/{TotalLineas.Value}";
                }
                return "0/0";
            }
        }

        [JsonIgnore]
        public string ColorProgreso
        {
            get
            {
                var porcentaje = PorcentajeProgreso;
                if (porcentaje == 0) return "#E74C3C";      // Rojo - Sin empezar
                if (porcentaje < 25) return "#F39C12";      // Naranja - Iniciado
                if (porcentaje < 75) return "#F1C40F";      // Amarillo - En progreso
                if (porcentaje < 100) return "#3498DB";     // Azul - Casi terminado
                return "#27AE60";                           // Verde - Completado
            }
        }

        [JsonIgnore]
        public string EstadoProgreso
        {
            get
            {
                var porcentaje = PorcentajeProgreso;
                if (porcentaje == 0) return "Sin empezar";
                if (porcentaje < 25) return "Iniciado";
                if (porcentaje < 75) return "En progreso";
                if (porcentaje < 100) return "Casi terminado";
                return "Completado";
            }
        }

        [JsonIgnore]
        public string UbicacionesEspeciales => IncluirUbicacionesEspeciales == true ? "Sí, incluidas" : "No incluidas";

        // === PROPIEDADES MULTIALMACÉN ===
        
        [JsonPropertyName("codigosAlmacen")]
        public List<string> CodigosAlmacen { get; set; } = new List<string>();

        [JsonIgnore]
        public bool EsMultialmacen => CodigosAlmacen.Count > 1;

        [JsonIgnore]
        public string DescripcionAlmacenes 
        { 
            get 
            {
                if (!CodigosAlmacen.Any() && !string.IsNullOrEmpty(CodigoAlmacen))
                    return CodigoAlmacen;
                    
                return CodigosAlmacen.Count <= 3 
                    ? string.Join(", ", CodigosAlmacen)
                    : $"{string.Join(", ", CodigosAlmacen.Take(2))} y {CodigosAlmacen.Count - 2} más";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 