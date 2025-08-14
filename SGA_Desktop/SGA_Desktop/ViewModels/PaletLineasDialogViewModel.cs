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
using SGA_Desktop.Dialog;
using System.Windows.Data;

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
				var resultados = await _paletService.BuscarStockDisponibleAsync(ArticuloBuscado, ArticuloDescripcion);



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

		[RelayCommand]
		private async Task ConfirmarAsync()
		{
			if (!LineasPendientes.Any())
			{
				var warning = new WarningDialog(
					"Sin líneas",
					"No hay líneas para confirmar. Añade al menos una línea antes de confirmar.",
					"\uE814"
				);
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
							 ?? Application.Current.MainWindow;
				if (owner != null && owner != warning)
					warning.Owner = owner;
				warning.ShowDialog();
				return;
			}

			bool todoOk = true;

			foreach (var dto in LineasPendientes)
			{
				var (ok, mensaje) = await _paletService.AnhadirLineaPaletAsync(PaletId, dto);
				if (!ok)
				{
					todoOk = false;
					dto.TieneError = true;
				}
				else
				{
					dto.TieneError = false;
				}
			}

			if (todoOk)
			{
				var confirm = new ConfirmationDialog(
					"Artículo añadido",
					$"El artículo ha sido añadido correctamente al palet {PaletCodigo}.",
					"\uE8FB"
				);
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
							 ?? Application.Current.MainWindow;
				if (owner != null && owner != confirm)
					confirm.Owner = owner;
				confirm.ShowDialog();
				Application.Current.Windows.OfType<Window>()
					.FirstOrDefault(w => w.DataContext == this)?.Close();
			}
			else
			{
				var warning = new WarningDialog(
					"Error en traspaso",
					"Algunas líneas no se pudieron mover. Revisa los errores mostrados.",
					"\uE814"
				);
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
							 ?? Application.Current.MainWindow;
				if (owner != null && owner != warning)
					warning.Owner = owner;
				warning.ShowDialog();
			}
			CollectionViewSource.GetDefaultView(LineasPendientes).Refresh();
		}


		[RelayCommand]
		private async Task AnhadirLineaAsync(StockDisponibleDto dto)
		{
			if (dto.CantidadAMoverDecimal is not decimal cantidad || cantidad <= 0)
			{
				var warning = new WarningDialog(
					"Cantidad no válida",
					"Debes indicar una cantidad mayor que 0 para añadir al palet.",
					"\uE814"
				);
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
							 ?? Application.Current.MainWindow;
				if (owner != null && owner != warning)
					warning.Owner = owner;
				warning.ShowDialog();
				return;
			}

			if (cantidad > dto.UnidadSaldo)
			{
				var warning = new WarningDialog(
					"Cantidad excedida",
					$"La cantidad a mover ({cantidad}) es mayor que la disponible ({dto.UnidadSaldo}).",
					"\uE814"
				);
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
							 ?? Application.Current.MainWindow;
				if (owner != null && owner != warning)
					warning.Owner = owner;
				warning.ShowDialog();
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
				var warning = new WarningDialog(
					"Línea duplicada",
					"Ya has añadido esta línea antes al palet.",
					"\uE814"
				);
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
							 ?? Application.Current.MainWindow;
				if (owner != null && owner != warning)
					warning.Owner = owner;
				warning.ShowDialog();
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
