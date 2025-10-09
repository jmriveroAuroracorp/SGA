using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using SGA_Desktop.Models;
public class StockDisponibleDto : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public string CodigoAlmacen { get; set; }
	public string Ubicacion { get; set; }
	public string Partida { get; set; }
	public DateTime? FechaCaducidad { get; set; }
	public decimal UnidadSaldo { get; set; }
	public decimal Reservado { get; set; }
	public decimal Disponible { get; set; }

	public string CodigoArticulo { get; set; }
	public string DescripcionArticulo { get; set; }
	public short CodigoEmpresa { get; set; }

	public bool TieneError { get; set; }

	public string ErrorMessage { get; set; }

	// editable
	public decimal CantidadAMover { get; set; }

	private string _cantidadAMoverTexto;
	public string CantidadAMoverTexto
	{
		get => _cantidadAMoverTexto;
		set
		{
			_cantidadAMoverTexto = value;

			if (decimal.TryParse(_cantidadAMoverTexto?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
				CantidadAMover = dec;
			else
				CantidadAMover = 0;

			OnPropertyChanged();
		}
	}
	public decimal? CantidadAMoverDecimal
	{
		get
		{
			if (decimal.TryParse(CantidadAMoverTexto?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
				return dec;
			return null;
		}
	}

    public event EventHandler AlmacenDestinoChanged;
    private string _almacenDestino;
    public string AlmacenDestino
    {
        get => _almacenDestino;
        set
        {
            if (_almacenDestino != value)
            {
                _almacenDestino = value;
                OnPropertyChanged(nameof(AlmacenDestino));
                AlmacenDestinoChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private string _ubicacionDestino;
    public string UbicacionDestino
    {
        get => _ubicacionDestino;
        set
        {
            _ubicacionDestino = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<UbicacionDto> UbicacionesDestino { get; set; } = new();
	public string? EstadoPaletDestino { get; set; }
	public string? EstadoPaletOrigen { get; set; }

    //  NUEVAS PROPIEDADES para información de palets
    public string TipoStock { get; set; } = "Suelto";
    public Guid? PaletId { get; set; }
    public string? CodigoPalet { get; set; }
    public string? EstadoPalet { get; set; }
    
    //  PROPIEDADES COMPUTADAS
    public bool EsStockPaletizado => TipoStock == "Paletizado";
    public bool EsStockSuelto => TipoStock == "Suelto";
    public string InformacionPalet => EsStockPaletizado ? $"{CodigoPalet} ({EstadoPalet})" : "";
    
    //  NUEVA: Para compatibilidad con el patrón de ConsultaStockView
    public bool EstaPaletizado => EsStockPaletizado;
    
    //  NUEVAS PROPIEDADES PARA SELECCIÓN DE PALET EN DESTINO
    public ObservableCollection<PaletDto> PaletsDisponibles { get; set; } = new();
    
    private PaletDto? _paletDestinoSeleccionado;
    public PaletDto? PaletDestinoSeleccionado 
    { 
        get => _paletDestinoSeleccionado;
        set
        {
            _paletDestinoSeleccionado = value;
            OnPropertyChanged();
        }
    }
    
    private bool _mostrarSelectorPalets;
    public bool MostrarSelectorPalets 
    { 
        get => _mostrarSelectorPalets;
        set
        {
            _mostrarSelectorPalets = value;
            OnPropertyChanged();
        }
    }
    
    private string? _paletDestinoId;
    public string? PaletDestinoId 
    { 
        get => _paletDestinoId;
        set
        {
            _paletDestinoId = value;
            OnPropertyChanged();
        }
    }
}
