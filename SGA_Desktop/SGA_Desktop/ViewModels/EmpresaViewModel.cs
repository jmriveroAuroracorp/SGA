using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Views;
using SGA_Desktop.Dialog;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels;

public partial class EmpresaViewModel : ObservableObject
{
	public ObservableCollection<EmpresaDto> Empresas { get; }

	[ObservableProperty] private EmpresaDto? empresaSeleccionada;

	private readonly LoginService _loginService;            // ← solo UNA instancia

	// === 1 · Constructor: recibe el servicio =====
	public EmpresaViewModel(LoginService loginService)
	{
		_loginService = loginService;

		// lista que llegó en el login
		Empresas = new ObservableCollection<EmpresaDto>(
			SessionManager.UsuarioActual?.empresas ?? new());

		// ► Sin selección inicial:
		empresaSeleccionada = null;
	}

	// === 2 · Comando Aceptar ======================
	[RelayCommand]
	private async Task Aceptar()
	{
		if (EmpresaSeleccionada is null)
		{
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
			var aviso = new WarningDialog(
				"Aviso",
				"Selecciona una empresa primero.",
				"\uE946" // ícono de información
			);
			if (owner != null && owner != aviso)
				aviso.Owner = owner;
			aviso.ShowDialog();
			return;
		}

		// --- Aquí, tu diálogo personalizado ---
		var owner2 = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
					 ?? Application.Current.MainWindow;
		var dialog = new ConfirmationDialog(
			"Confirmar cambio de empresa",
			$"Vas a cambiar a:\n  {EmpresaSeleccionada.Codigo} – {EmpresaSeleccionada.Nombre}\n\n" +
			"Esto reiniciará pantallas y perderás filtros no guardados.\n¿Deseas continuar?"
		);
		if (owner2 != null && owner2 != dialog)
			dialog.Owner = owner2;
		if (dialog.ShowDialog() != true)
			return;
		// ----------------------------------------

		// 1) Guarda globalmente
		SessionManager.SetEmpresa(EmpresaSeleccionada.Codigo);

		// 2) Guarda en la API
		var (ok, detalle, status) = await _loginService
			.EstablecerEmpresaPreferidaAsync(
				SessionManager.UsuarioActual!.operario,
				EmpresaSeleccionada.Codigo);

		if (!ok)
		{
			MessageBox.Show(
				$"Status: {(int)status} {status}\n{detalle}",
				"Error guardando empresa por defecto",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			return;
		}

		// 3) Limpia la caché de vistas
		NavigationStore.ClearCache();

		// 4) Navega de nuevo a ConsultaStock si quieres
		//NavigationStore.Navigate("ConsultaStock");
	}

}
