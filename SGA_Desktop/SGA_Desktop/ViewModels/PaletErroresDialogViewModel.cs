using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels;

public partial class PaletErroresDialogViewModel : ObservableObject
{
	private readonly PaletService _paletService;

	public ObservableCollection<TraspasoErrorDto> Traspasos { get; } = new();

	[ObservableProperty]
	private TraspasoErrorDto? traspasoSeleccionado;

	[ObservableProperty]
	private bool estaCargando;

	public IAsyncRelayCommand RefrescarCommand { get; }
	public IAsyncRelayCommand RelanzarCommand { get; }

	public PaletErroresDialogViewModel(PaletService paletService)
	{
		_paletService = paletService;
		RefrescarCommand = new AsyncRelayCommand(RefrescarAsync);
		RelanzarCommand = new AsyncRelayCommand(RelanzarAsync, () => TraspasoSeleccionado != null && !EstaCargando);
	}

	public async Task InitializeAsync()
	{
		await RefrescarAsync();
	}

	partial void OnTraspasoSeleccionadoChanged(TraspasoErrorDto? value)
	{
		RelanzarCommand.NotifyCanExecuteChanged();
	}

	partial void OnEstaCargandoChanged(bool value)
	{
		RefrescarCommand.NotifyCanExecuteChanged();
		RelanzarCommand.NotifyCanExecuteChanged();
	}

	private async Task RefrescarAsync()
	{
		if (!SessionManager.EmpresaSeleccionada.HasValue)
		{
			ShowWarning("Empresa no seleccionada", "Selecciona una empresa antes de consultar los traspasos en error.");
			return;
		}

		try
		{
			EstaCargando = true;
			var empresa = SessionManager.EmpresaSeleccionada.Value;
			var lista = await _paletService.ObtenerTraspasosErrorErpAsync(empresa);

			Application.Current.Dispatcher.Invoke(() =>
			{
				Traspasos.Clear();
				foreach (var item in lista.OrderByDescending(x => x.FechaInicio))
				{
					Traspasos.Add(item);
				}
			});
		}
		catch (Exception ex)
		{
			ShowWarning("Error al cargar", ex.Message);
		}
		finally
		{
			EstaCargando = false;
		}
	}

	private async Task RelanzarAsync()
	{
		if (TraspasoSeleccionado == null)
		{
			ShowWarning("Sin selección", "Selecciona un traspaso antes de relanzar.");
			return;
		}

		var usuarioId = SessionManager.UsuarioActual?.operario ?? 0;
		if (usuarioId <= 0)
		{
			ShowWarning("Usuario no válido", "No se encontró el operario actual para relanzar el traspaso.");
			return;
		}

		var traspaso = TraspasoSeleccionado;
		var identificador = string.IsNullOrWhiteSpace(traspaso.CodigoPalet) ? traspaso.TraspasoId.ToString() : traspaso.CodigoPalet;
		var mensaje = $"Se relanzará el traspaso del palet {identificador} hacia {traspaso.AlmacenDestino}-{traspaso.UbicacionDestino}.";

		if (!ShowConfirmation("Relanzar traspaso", mensaje))
		{
			return;
		}

		try
		{
			EstaCargando = true;
			var request = new RelanzarTraspasoRequest
			{
				UsuarioId = usuarioId,
				Comentario = traspaso.Comentario
			};

			var (exito, mensajeError) = await _paletService.RelanzarTraspasoAsync(traspaso.TraspasoId, request);
			if (!exito)
			{
				ShowWarning("Error al relanzar", mensajeError ?? "No se pudo relanzar el traspaso.");
				return;
			}

			ShowInfo("Traspaso relanzado", "El traspaso se ha relanzado y volverá a procesarse.");
			await RefrescarAsync();
		}
		finally
		{
			EstaCargando = false;
		}
	}

	private void ShowWarning(string title, string message, string iconGlyph = "\uE814")
	{
		var dialog = new WarningDialog(title, message, iconGlyph);
		ConfigureOwner(dialog);
		dialog.ShowDialog();
	}

	private void ShowInfo(string title, string message)
	{
		var dialog = new WarningDialog(title, message, "\uE930");
		ConfigureOwner(dialog);
		dialog.ShowDialog();
	}

	private bool ShowConfirmation(string title, string message)
	{
		var dialog = new ConfirmationDialog(title, message);
		ConfigureOwner(dialog);
		return dialog.ShowDialog() == true;
	}

	private void ConfigureOwner(Window dialog)
	{
		var mainWindow = Application.Current.Windows
			.OfType<Window>()
			.FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

		if (mainWindow != null && mainWindow != dialog)
		{
			dialog.Owner = mainWindow;
			dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		}
		else
		{
			dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		}
	}
}

