using System.Collections.ObjectModel;
using SGA_Desktop.Models;

namespace SGA_Desktop.ViewModels
{
	public class ConfirmarImpresionDialogViewModel
	{
		public ObservableCollection<ImpresoraDto> Impresoras { get; set; }
		public ImpresoraDto? ImpresoraSeleccionada { get; set; }
		public int NumeroCopias { get; set; } = 1;

		public ConfirmarImpresionDialogViewModel(
			ObservableCollection<ImpresoraDto> impresoras,
			ImpresoraDto? preseleccionada)
		{
			Impresoras = impresoras;
			ImpresoraSeleccionada = preseleccionada ?? impresoras.FirstOrDefault();
		}
	}
}
