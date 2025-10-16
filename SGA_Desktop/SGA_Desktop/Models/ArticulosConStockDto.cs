using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SGA_Desktop.Models
{
	public partial class ArticuloConStockDto : ObservableObject
	{
		public string CodigoArticulo { get; set; }
		public string DescripcionArticulo { get; set; }
		public ObservableCollection<StockDisponibleDto> Ubicaciones { get; set; } = new();
		public string HeaderArticulo => $"{CodigoArticulo} - {DescripcionArticulo}";
		
		// 🔷 NUEVO: Propiedad para controlar el estado de expansión
		[ObservableProperty]
		private bool isExpanded = false;
	}
}
