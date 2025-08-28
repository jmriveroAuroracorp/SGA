using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para líneas temporales de inventario con información adicional
    /// </summary>
    public class LineaTemporalInventarioDto : INotifyPropertyChanged
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
        public decimal? CantidadContada 
        { 
            get => _cantidadContada; 
            set 
            {
                if (SetProperty(ref _cantidadContada, value))
                {
                    OnPropertyChanged(nameof(CantidadContadaTexto));
                    OnPropertyChanged(nameof(Diferencia));
                    OnPropertyChanged(nameof(TieneDiferencia));
                }
            } 
        }
        private decimal? _cantidadContada;

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

        // === NUEVAS PROPIEDADES PARA INFORMACIÓN DE PALETS ===
        
        /// <summary>
        /// Información de los palets que contienen este stock
        /// </summary>
        [JsonPropertyName("palets")]
        public List<PaletDetalleDto> Palets { get; set; } = new();

        /// <summary>
        /// Indica si el stock está en al menos un palet
        /// </summary>
        public bool TienePalets => Palets?.Any() == true;

        /// <summary>
        /// Indica si el stock está distribuido en múltiples palets
        /// </summary>
        public bool TieneMultiplesPalets => Palets?.Count > 1;

        /// <summary>
        /// Texto resumido de los palets para mostrar en la UI
        /// </summary>
        public string PaletsResumen
        {
            get
            {
                if (Palets == null || !Palets.Any())
                    return "Sin palets";

                if (Palets.Count == 1)
                {
                    var palet = Palets.First();
                    return $"{palet.CodigoPalet} ({palet.Cantidad:F2})";
                }

                return "📦 Múltiples palets";
            }
        }


        /// <summary>
        /// Diferencia entre cantidad contada y stock actual
        /// </summary>
        public decimal Diferencia => (CantidadContada ?? 0) - StockActual;

        /// <summary>
        /// Indica si hay diferencia entre lo contado y el stock actual
        /// </summary>
        public bool TieneDiferencia => Diferencia != 0;

        /// <summary>
        /// Indica si este artículo está seleccionado en la UI
        /// </summary>
        public bool IsSelected 
        { 
            get => _isSelected; 
            set => SetProperty(ref _isSelected, value); 
        }
        private bool _isSelected = false;

        /// <summary>
        /// Texto para el binding del TextBox (permite entrada temporal)
        /// </summary>
        public string CantidadContadaTexto
        {
            get => _cantidadContadaTexto ?? CantidadContada?.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "0";
            set
            {
                _cantidadContadaTexto = value;
                
                // Si está vacío, establecer como null
                if (string.IsNullOrWhiteSpace(value))
                {
                    CantidadContada = null;
                    return;
                }

                // Intentar parsear como decimal usando cultura invariante (punto decimal)
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal result))
                {
                    CantidadContada = result;
                }
                // Si no se puede parsear, mantener el texto tal como está
                // Esto permite entrada temporal como ".5" o "1."
                
                OnPropertyChanged();
            }
        }
        private string? _cantidadContadaTexto;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
} 