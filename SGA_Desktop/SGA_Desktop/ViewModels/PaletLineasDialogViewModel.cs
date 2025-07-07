using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Services;
using SGA_Desktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
	public partial class PaletLineasDialogViewModel : ObservableObject
	{
		private readonly PaletService _paletService;

		public PaletLineasDialogViewModel(
			Guid paletId,
			string paletCodigo,
			string paletTipo,
			string paletEstado,
			PaletService paletService)
		{
			PaletId = paletId;
			PaletCodigo = paletCodigo;
			PaletTipo = paletTipo;
			PaletEstado = paletEstado;

			_paletService = paletService;

			StockDisponible = new ObservableCollection<StockDisponibleDto>();
		}

		// === Datos del palet ===
		public Guid PaletId { get; }
		public string PaletCodigo { get; }
		public string PaletTipo { get; }
		public string PaletEstado { get; }

		// === Artículo buscado ===
		[ObservableProperty]
		private string articuloBuscado;
		partial void OnArticuloBuscadoChanged(string value)
		{
			BuscarStockCommand.NotifyCanExecuteChanged();
		}

		// === Lista de stock disponible ===
		public ObservableCollection<StockDisponibleDto> StockDisponible { get; }

		// === Comando: Buscar Stock ===
		[RelayCommand(CanExecute = nameof(CanBuscarStock))]
		private async Task BuscarStockAsync()
		{
			var resultados = await _paletService.BuscarStockAsync(articuloBuscado);

			StockDisponible.Clear();

			foreach (var item in resultados)
				StockDisponible.Add(item);
		}

		private bool CanBuscarStock() => !string.IsNullOrWhiteSpace(articuloBuscado);

		// === Comando: Confirmar todas las líneas ===
		[RelayCommand]
		private async Task ConfirmarAsync()
		{
			var itemsAMover = StockDisponible.Where(s => s.CantidadAMover > 0).ToList();

			if (!itemsAMover.Any())
			{
				MessageBox.Show("No hay ninguna cantidad indicada para mover.", "Aviso",
					MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			foreach (var dto in itemsAMover)
			{
				var ok = await _paletService.AnhadirLineaPaletAsync(PaletId, dto);
				if (!ok)
				{
					MessageBox.Show($"Error al mover el artículo {dto.CodigoArticulo} desde {dto.Ubicacion}.",
						"Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}

			MessageBox.Show("Movimientos realizados correctamente.", "Éxito",
				MessageBoxButton.OK, MessageBoxImage.Information);

			// opcional: cerrar ventana
			Application.Current.Windows.OfType<Window>()
				.FirstOrDefault(w => w.DataContext == this)?.Close();
		}
	}
}
