using SGA_Desktop.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SGA_Desktop.Views
{
	/// <summary>
	/// Lógica de interacción para GestionUbicaciones.xaml
	/// </summary>
	public partial class GestionUbicacionesView : Page
	{
		public GestionUbicacionesView()
		{
			InitializeComponent();
			DataContext = new GestionUbicacionesViewModel();
		}
		private void Expander_Expanded(object sender, RoutedEventArgs e)
		{
			var exp = (Expander)sender;

			// 1) Deselecciona TODOS los ListViewItems salvo el actual
			foreach (var item in lvUbicaciones.Items)
			{
				var container = lvUbicaciones
					.ItemContainerGenerator
					.ContainerFromItem(item) as ListViewItem;
				if (container == null) continue;

				// compara el DataContext, que es tu DTO
				if (!ReferenceEquals(container.DataContext, exp.DataContext))
					container.IsSelected = false;
			}

			// 2) Selecciona (resalta) el ListViewItem que acaba de expandirse
			var myItem = lvUbicaciones
				.ItemContainerGenerator
				.ContainerFromItem(exp.DataContext) as ListViewItem;
			if (myItem != null)
				myItem.IsSelected = true;
		}


		//// Helper genérico para buscar por tipo dentro del árbol visual
		//public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
		//{
		//	for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		//	{
		//		var child = VisualTreeHelper.GetChild(parent, i);
		//		if (child is T t) return t;
		//		var result = FindVisualChild<T>(child);
		//		if (result != null) return result;
		//	}
		//	return null;
		//}


	}
}
