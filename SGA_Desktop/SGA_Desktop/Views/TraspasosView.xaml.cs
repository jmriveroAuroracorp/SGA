using SGA_Desktop.Helpers;
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
	public partial class TraspasosView : Page
	{
		public TraspasosView()
		{
			InitializeComponent();
			// Apuntamos el Frame interno al store
			TraspasosNavigationStore.InnerFrame = InnerFrame;
			// Navegamos automáticamente a la pestaña de Paletización
			TraspasosNavigationStore.Navigate("Paletizacion");
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (sender is TabControl tc && tc.SelectedItem is TabItem ti && ti.Tag is string key)
			{
				TraspasosNavigationStore.Navigate(key);
			}
		}
	}
}