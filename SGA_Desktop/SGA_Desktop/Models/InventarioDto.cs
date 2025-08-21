using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para representar un registro de inventario físico
    /// </summary>
    public class InventarioDto : INotifyPropertyChanged
    {
        [JsonPropertyName("idInventario")]
        public int IdInventario { get; set; }

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
        public DateTime FechaInventario { get; set; } = DateTime.Now;

        [JsonPropertyName("usuarioInventario")]
        public string UsuarioInventario { get; set; } = string.Empty;

        [JsonPropertyName("estadoInventario")]
        public string EstadoInventario { get; set; } = "PENDIENTE"; // PENDIENTE, COMPLETADO, APROBADO

        [JsonPropertyName("codigoPalet")]
        public string? CodigoPalet { get; set; }

        [JsonPropertyName("estadoPalet")]
        public string? EstadoPalet { get; set; }

        [JsonPropertyName("alergenos")]
        public string Alergenos { get; set; } = string.Empty;

        [JsonPropertyName("codigoAlternativo")]
        public string CodigoAlternativo { get; set; } = string.Empty;

        // Propiedades calculadas para la UI
        [JsonIgnore]
        public bool TieneDiferencia => Math.Abs(Diferencia) > 0.01m;

        [JsonIgnore]
        public bool EsDiferenciaPositiva => Diferencia > 0.01m;

        [JsonIgnore]
        public bool EsDiferenciaNegativa => Diferencia < -0.01m;

        [JsonIgnore]
        public string DiferenciaFormateada => Diferencia.ToString("N2");

        [JsonIgnore]
        public string StockSistemaFormateado => StockSistema.ToString("N2");

        [JsonIgnore]
        public string StockFisicoFormateado => StockFisico.ToString("N2");

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 