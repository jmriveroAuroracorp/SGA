using System;
using System.Collections.Generic;
using System.Windows.Controls;
using SGA_Desktop.Views;

namespace SGA_Desktop.Helpers
{
	public static class NavigationStore
	{
		// PERMANENCIA EN SESIÓN
		public static Frame MainFrame { get; set; }

		private static readonly Dictionary<string, Page> _pageCache = new();

		public static void Navigate(string pageKey)
		{
			System.Diagnostics.Debug.WriteLine($"NavigationStore.Navigate: {pageKey}");
			
			if (!_pageCache.TryGetValue(pageKey, out var page))
			{
				System.Diagnostics.Debug.WriteLine($"Creando nueva página: {pageKey}");
				
				// Sólo se crea la página la primera vez
				page = pageKey switch
				{
					"ConsultaStock" => new ConsultaStockView(),
					"Traspasos" => new TraspasosView(),
					"Ubicaciones" => new GestionUbicacionesView(),
					//"Inventario" => new InventarioView(),
					"Etiquetas" => new ImpresionEtiquetasView(),
					"SeleccionEmpresa" => new EmpresaView(),
					"Paletizacion" => new PaletizacionView(),
					"TraspasosStock" => new TraspasosStockView(),
					"Inventario" => new InventarioView(),
					"OrdenTraspaso" => new OrdenTraspasoView(),
					"ControlesRotativos" => new ControlesRotativosView(),

					_ => throw new ArgumentException($"Página desconocida: {pageKey}")
				};

				_pageCache[pageKey] = page;
				System.Diagnostics.Debug.WriteLine($"Página creada y cacheada: {pageKey}");
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"Usando página cacheada: {pageKey}");
			}

			if (MainFrame.Content != page)
			{
				System.Diagnostics.Debug.WriteLine($"Navegando a: {pageKey}");
				MainFrame.Navigate(page);
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"Ya estamos en la página: {pageKey}");
			}
		}
		/// <summary>
		/// Vacía la caché y limpia la vista actual.
		/// </summary>
		public static void ClearCache()
		{
			_pageCache.Clear();
			if (MainFrame.Content != null)
				MainFrame.Content = null;
		}
	}
}
