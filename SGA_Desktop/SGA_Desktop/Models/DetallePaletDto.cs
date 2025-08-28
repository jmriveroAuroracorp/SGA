using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
    /// <summary>
    /// DTO para el detalle de un palet en inventario
    /// </summary>
    public class DetallePaletDto : INotifyPropertyChanged
    {
        [JsonPropertyName("paletId")]
        public Guid PaletId { get; set; }

        [JsonPropertyName("codigoPalet")]
        public string CodigoPalet { get; set; } = string.Empty;

        [JsonPropertyName("estadoPalet")]
        public string EstadoPalet { get; set; } = string.Empty;

        [JsonPropertyName("cantidadEnPalet")]
        public decimal CantidadEnPalet { get; set; }

        [JsonPropertyName("fechaCaducidad")]
        public DateTime? FechaCaducidad { get; set; }

        [JsonPropertyName("porcentajeDelTotal")]
        public decimal PorcentajeDelTotal { get; set; }

        /// <summary>
        /// Color del estado del palet
        /// </summary>
        public string ColorEstado
        {
            get
            {
                return EstadoPalet?.ToUpper() switch
                {
                    "ABIERTO" => "#28A745", // Verde
                    "CERrado" => "#DC3545", // Rojo
                    "VACIADO" => "#FD7E14", // Naranja
                    _ => "#6C757D" // Gris
                };
            }
        }

        /// <summary>
        /// Texto formateado del porcentaje
        /// </summary>
        public string PorcentajeFormateado => $"{PorcentajeDelTotal:F1}%";

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
} 