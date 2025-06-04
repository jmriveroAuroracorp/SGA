using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Views;
using System.Windows.Controls;

namespace SGA_Desktop.ViewModels
{
	public partial class MainViewModel : ObservableObject
	{
		[RelayCommand]
		public void IrATraspasos()
		{
			NavigationStore.MainFrame.Navigate(new TraspasosView());
		}

		[RelayCommand]
		public void IrAInventario()
		{
			// Implementar en el futuro
		}
	}
}
