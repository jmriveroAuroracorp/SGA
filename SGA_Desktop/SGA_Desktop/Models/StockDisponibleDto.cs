using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

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

	public string CodigoArticulo { get; set; }
	public string DescripcionArticulo { get; set; }

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

}
