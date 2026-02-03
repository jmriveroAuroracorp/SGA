using System.ComponentModel;

namespace SGA_Desktop.Models.Calidad
{
    public class StockCalidadDto : INotifyPropertyChanged
    {
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Almacen { get; set; } = string.Empty;
        public string Ubicacion { get; set; } = string.Empty;
        public string LotePartida { get; set; } = string.Empty;
        public DateTime? FechaCaducidad { get; set; }
        public decimal CantidadDisponible { get; set; }
        public bool EstaBloqueado { get; set; }
        public string? ComentarioBloqueo { get; set; }
        public DateTime? FechaBloqueo { get; set; }
        public string? UsuarioBloqueo { get; set; }
        public string Estado { get; set; } = "Disponible";
        
        // 🔷 NUEVO: Información de palet si está paletizado
        public Guid? PaletId { get; set; }
        public string? CodigoPalet { get; set; }
        public string? EstadoPalet { get; set; }
        public bool EstaPaletizado => !string.IsNullOrEmpty(CodigoPalet);

        // 🔷 NUEVO: Propiedad para indicar si está seleccionado
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
