using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
	public partial class ConfirmarImpresionDialogViewModel : ObservableObject
	{
		private readonly LoginService _loginService;

		public ObservableCollection<ImpresoraDto> Impresoras { get; }
		public string? ImpresoraSeleccionadaNombre { get; set; }
		public int NumeroCopias { get; set; } = 1;

		public event Action<bool?>? RequestClose;

		public ConfirmarImpresionDialogViewModel(
			IEnumerable<ImpresoraDto> impresoras,
			string? preseleccionNombre,
			LoginService loginService)   // ⬅️ pásalo desde el padre
		{
			_loginService = loginService;

			var lista = impresoras ?? Enumerable.Empty<ImpresoraDto>();
			Impresoras = impresoras is ObservableCollection<ImpresoraDto> oc
				? oc
				: new ObservableCollection<ImpresoraDto>(lista);

			ImpresoraSeleccionadaNombre = preseleccionNombre
				?? Impresoras.FirstOrDefault()?.Nombre;
		}

		public ImpresoraDto? ImpresoraSeleccionada =>
			Impresoras.FirstOrDefault(i =>
				string.Equals(i.Nombre, ImpresoraSeleccionadaNombre, System.StringComparison.OrdinalIgnoreCase));

		[RelayCommand]
		private async Task AceptarAsync()
		{
			var nombre = (ImpresoraSeleccionadaNombre ?? string.Empty).Trim();

			// ——— REPLICADO TAL CUAL ———
			SessionManager.PreferredPrinter = string.IsNullOrEmpty(nombre) ? null : nombre;
			var resp = await _loginService.EstablecerImpresoraPreferidaAsync(
				SessionManager.Operario, nombre);
			// opcional: mostrar aviso si !resp.ok
			// ————————————————————————

			RequestClose?.Invoke(true);
		}

		[RelayCommand]
		private void Cancelar() => RequestClose?.Invoke(false);
	}
}
