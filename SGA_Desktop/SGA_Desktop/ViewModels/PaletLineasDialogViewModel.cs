using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Services;
using SGA_Desktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SGA_Desktop.Helpers;

namespace SGA_Desktop.ViewModels
{
	public partial class PaletLineasDialogViewModel : ObservableObject
	{
		private readonly PaletService _paletService;
		private readonly StockService _stockService;

		public ObservableCollection<ArticuloConStockDto> ArticulosConStock { get; } = new();
		public PaletLineasDialogViewModel(
			Guid paletId,
			string paletCodigo,
			string paletTipo,
			string paletEstado,
			PaletService paletService,
			  StockService stockService)
		{
			PaletId = paletId;
			PaletCodigo = paletCodigo;
			PaletTipo = paletTipo;
			PaletEstado = paletEstado;

			_paletService = paletService;
			_stockService = stockService;

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

		[ObservableProperty]
		private string articuloDescripcion;

		public ObservableCollection<StockDisponibleDto> LineasAñadidas { get; } = new();
		public ObservableCollection<StockDisponibleDto> LineasPendientes { get; } = new();

		partial void OnArticuloBuscadoChanged(string value) => BuscarStockCommand.NotifyCanExecuteChanged();
		partial void OnArticuloDescripcionChanged(string value) => BuscarStockCommand.NotifyCanExecuteChanged();

		// === Lista de stock disponible ===
		public ObservableCollection<StockDisponibleDto> StockDisponible { get; }

		// === Comando: Buscar Stock ===
		//[RelayCommand(CanExecute = nameof(CanBuscarStock))]
		//private async Task BuscarStockAsync()
		//{
		//	//var resultados = await _paletService.BuscarStockAsync(articuloBuscado, articuloDescripcion);

		//	//StockDisponible.Clear();

		//	//foreach (var item in resultados)
		//	//	StockDisponible.Add(item);
		//	var resultados = await _paletService.BuscarStockAsync(ArticuloBuscado, ArticuloDescripcion);

		//	var grupos = resultados
		//		.GroupBy(s => new { s.CodigoArticulo, s.DescripcionArticulo })
		//		.Select(g => new ArticuloConStockDto
		//		{
		//			CodigoArticulo = g.Key.CodigoArticulo,
		//			DescripcionArticulo = g.Key.DescripcionArticulo,
		//			Ubicaciones = new ObservableCollection<StockDisponibleDto>(g)
		//		})
		//		.ToList();

		//	ArticulosConStock.Clear();
		//	foreach (var art in grupos)
		//		ArticulosConStock.Add(art);
		//}

		[RelayCommand(CanExecute = nameof(CanBuscarStock))]
		private async Task BuscarStockAsync()
		{
			try
			{
				var resultados = await _paletService.BuscarStockAsync(ArticuloBuscado, ArticuloDescripcion);

			

				List<string> permisos = SessionManager.UsuarioActual.codigosAlmacen?.ToList() ?? new();

				if (!permisos.Any())
				{
					string centro = SessionManager.UsuarioActual.codigoCentro ?? "0";


					permisos = await _stockService.ObtenerAlmacenesAsync(centro) ?? new();
				}

				resultados = resultados
					.Where(x => x?.CodigoAlmacen != null && permisos.Contains(x.CodigoAlmacen))
					.ToList();

				// === AGRUPAR Y MOSTRAR ===
				var grupos = resultados
					.GroupBy(s => new { s.CodigoArticulo, s.DescripcionArticulo })
					.Select(g => new ArticuloConStockDto
					{
						CodigoArticulo = g.Key.CodigoArticulo,
						DescripcionArticulo = g.Key.DescripcionArticulo,
						Ubicaciones = new ObservableCollection<StockDisponibleDto>(g)
					})
					.ToList();

				ArticulosConStock.Clear();
				foreach (var art in grupos)
					ArticulosConStock.Add(art);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error al buscar stock: {ex.Message}", "Error",
					MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}


		private bool CanBuscarStock()
			=> !string.IsNullOrWhiteSpace(articuloBuscado) || !string.IsNullOrWhiteSpace(articuloDescripcion);

		// === Comando: Confirmar todas las líneas ===
		[RelayCommand]
		private async Task ConfirmarAsync()
		{
			if (!LineasPendientes.Any())
			{
				MessageBox.Show("No hay líneas para confirmar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			foreach (var dto in LineasPendientes)
			{
				var ok = await _paletService.AnhadirLineaPaletAsync(PaletId, dto);

				if (!ok)
				{
					MessageBox.Show(
						$"Error al mover el artículo {dto.CodigoArticulo} desde {dto.Ubicacion}.",
						"Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}

			MessageBox.Show("Movimientos realizados correctamente.", "Éxito",
				MessageBoxButton.OK, MessageBoxImage.Information);

			// opcional: cerrar ventana
			Application.Current.Windows.OfType<Window>()
				.FirstOrDefault(w => w.DataContext == this)?.Close();
		}


		[RelayCommand]
		private async Task AnhadirLineaAsync(StockDisponibleDto dto)
		{
			if (dto.CantidadAMoverDecimal is not decimal cantidad || cantidad <= 0)
			{
				MessageBox.Show("Indica una cantidad mayor que 0", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (cantidad > dto.UnidadSaldo)
			{
				MessageBox.Show($"La cantidad a mover ({cantidad}) es mayor que la disponible ({dto.UnidadSaldo}).",
					"Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			dto.CantidadAMover = cantidad;

			// 🚫 comprobar duplicado
			bool yaExiste = LineasPendientes.Any(x =>
	x.CodigoArticulo == dto.CodigoArticulo &&
	x.Partida == dto.Partida &&
	x.Ubicacion == dto.Ubicacion &&
	x.CodigoAlmacen == dto.CodigoAlmacen);


			if (yaExiste)
			{
				MessageBox.Show("Ya has añadido esta línea antes.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			LineasPendientes.Add(dto);
		}




		[RelayCommand]
		private void EliminarLinea(StockDisponibleDto dto)
		{
			if (LineasPendientes.Contains(dto))
				LineasPendientes.Remove(dto);
		}
	}
}
