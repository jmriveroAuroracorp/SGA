using System.ComponentModel;

namespace SGA_Desktop.Models.Calidad
{
    public class BloqueoCalidadDto : INotifyPropertyChanged
    {
        public Guid Id { get; set; }
        public string CodigoArticulo { get; set; } = string.Empty;
        public string DescripcionArticulo { get; set; } = string.Empty;
        public string LotePartida { get; set; } = string.Empty;
        public string CodigoAlmacen { get; set; } = string.Empty;
        public string Almacen { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public bool Bloqueado { get; set; }
        public string TipoBloqueo { get; set; } = "TOTAL"; // "TOTAL" o "SOLO_PULMON"
        public string UsuarioBloqueo { get; set; } = string.Empty;
        public DateTime FechaBloqueo { get; set; }
        public string ComentarioBloqueo { get; set; } = string.Empty;
        public string? UsuarioDesbloqueo { get; set; }
        public DateTime? FechaDesbloqueo { get; set; }
        public string? ComentarioDesbloqueo { get; set; }

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
