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
			if (!_pageCache.TryGetValue(pageKey, out var page))
			{
				// Sólo se crea la página la primera vez
				page = pageKey switch
				{
					"ConsultaStock" => new ConsultaStockView(),
					"Traspasos" => new TraspasosView(),
					"Ubicaciones" => new GestionUbicacionesView(),
					//"Inventario" => new InventarioView(),
					"Etiquetas" => new ImpresionEtiquetasView(),
					"SeleccionEmpresa" => new EmpresaView(),
					_ => throw new ArgumentException($"Página desconocida: {pageKey}")
				};

				_pageCache[pageKey] = page;
			}

		
			if (MainFrame.Content != page)
				MainFrame.Navigate(page);
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
