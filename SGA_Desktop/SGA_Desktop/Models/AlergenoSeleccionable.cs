using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public partial class AlergenoSeleccionable : ObservableObject
	{
		public short Codigo { get; }
		public string Descripcion { get; }

		[ObservableProperty]
		private bool isSelected;

		public AlergenoSeleccionable(AlergenoDto dto)
		{
			Codigo = dto.Codigo;
			Descripcion = dto.Descripcion;
		}
	}
}
