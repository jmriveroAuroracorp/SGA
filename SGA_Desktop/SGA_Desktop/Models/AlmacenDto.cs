using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class AlmacenDto : INotifyPropertyChanged
	{
		public string CodigoAlmacen { get; set; } = "";
		public string NombreAlmacen { get; set; } = "";
		public short CodigoEmpresa { get; set; }
		public bool EsDelCentro { get; set; }
		public string DescripcionCombo =>
	CodigoAlmacen == "Todas" ? "Todos" : $"{CodigoAlmacen} – {NombreAlmacen}";

		private bool _isSelected;
		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (_isSelected != value)
				{
					_isSelected = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
				}
			}
		}

		public string Descripcion => NombreAlmacen;

		public event PropertyChangedEventHandler? PropertyChanged;
	}

}
