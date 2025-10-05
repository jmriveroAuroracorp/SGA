using System;
using System.Collections.Generic;
using System.Windows.Controls;
using SGA_Desktop.Views;

namespace SGA_Desktop.Helpers
{
	public static class TraspasosNavigationStore
	{
		public static Frame InnerFrame { get; set; } // Mantenemos para compatibilidad hacia atrás
		public static Frame PaletizacionFrame { get; set; }
		public static Frame GestionTraspasosFrame { get; set; }
		
		private static readonly Dictionary<string, Page> _cache = new();

		public static void Navigate(string pageKey)
		{
			if (!_cache.TryGetValue(pageKey, out var page))
			{
				page = pageKey switch
				{
					"GestionTraspasos" => new GestionTraspasosView(),
					_ => throw new ArgumentException($"Página desconocida: {pageKey}")
				};
				_cache[pageKey] = page;
			}

			// Navegar al Frame correspondiente
			switch (pageKey)
			{
				case "Paletizacion":
					// Ya no navegamos a PaletizacionView, se maneja directamente en TraspasosView
					break;
					
				case "GestionTraspasos":
					if (GestionTraspasosFrame != null && GestionTraspasosFrame.Content != page)
						GestionTraspasosFrame.Navigate(page);
					else if (InnerFrame != null && InnerFrame.Content != page)
						InnerFrame.Navigate(page); // Fallback para compatibilidad
					break;
			}
		}

		public static void ClearCache()
		{
			_cache.Clear();
			if (InnerFrame?.Content != null)
				InnerFrame.Content = null;
			if (PaletizacionFrame?.Content != null)
				PaletizacionFrame.Content = null;
			if (GestionTraspasosFrame?.Content != null)
				GestionTraspasosFrame.Content = null;
		}
	}
}
