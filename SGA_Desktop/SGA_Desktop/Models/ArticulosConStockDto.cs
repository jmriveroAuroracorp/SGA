using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class ArticuloConStockDto
	{
		public string CodigoArticulo { get; set; }
		public string DescripcionArticulo { get; set; }
		public ObservableCollection<StockDisponibleDto> Ubicaciones { get; set; } = new();
		public string HeaderArticulo => $"{CodigoArticulo} - {DescripcionArticulo}";
	}
}
