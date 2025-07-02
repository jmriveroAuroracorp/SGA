// Helpers/TraspasosNavigationStore.cs
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using SGA_Desktop.Views;

namespace SGA_Desktop.Helpers
{
	public static class TraspasosNavigationStore
	{
		public static Frame InnerFrame { get; set; }
		private static readonly Dictionary<string, Page> _cache = new();

		public static void Navigate(string pageKey)
		{
			if (!_cache.TryGetValue(pageKey, out var page))
			{
				page = pageKey switch
				{
					"Paletizacion" => new PaletizacionView(),
					//"Clasico" => new TraspasosClasicoView(),
					_ => throw new ArgumentException($"Página desconocida: {pageKey}")
				};
				_cache[pageKey] = page;
			}

			if (InnerFrame.Content != page)
				InnerFrame.Navigate(page);
		}

		public static void ClearCache()
		{
			_cache.Clear();
			if (InnerFrame.Content != null)
				InnerFrame.Content = null;
		}
	}
}
