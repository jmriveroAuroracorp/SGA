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


}
