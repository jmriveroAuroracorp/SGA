using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGA_Desktop.Models
{
	public class GeneracionPatronRegla : INotifyDataErrorInfo
	{
		public int PasilloDesde { get; set; }
		public int PasilloHasta { get; set; }
		public int EstanteriaDesde { get; set; }
		public int EstanteriaHasta { get; set; }
		public int AlturaDesde { get; set; }
		public int AlturaHasta { get; set; }
		public int PosicionDesde { get; set; }
		public int PosicionHasta { get; set; }

		// Implementación simplificada de INotifyDataErrorInfo
		public bool HasErrors => GetErrors(null).Cast<object>().Any();
		public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

		public IEnumerable GetErrors(string propertyName)
		{
			// Para cada par “Desde”/“Hasta” comprobamos si el mínimo > máximo
			var errores = new List<string>();
			void Check(string minProp, string maxProp)
			{
				var min = (int)GetType().GetProperty(minProp).GetValue(this);
				var max = (int)GetType().GetProperty(maxProp).GetValue(this);
				if (min > max)
					errores.Add($"\"{minProp}\" ({min}) no puede ser mayor que \"{maxProp}\" ({max}).");
			}
			Check(nameof(PasilloDesde), nameof(PasilloHasta));
			Check(nameof(EstanteriaDesde), nameof(EstanteriaHasta));
			Check(nameof(AlturaDesde), nameof(AlturaHasta));
			Check(nameof(PosicionDesde), nameof(PosicionHasta));
			return errores;
		}
	}
}
