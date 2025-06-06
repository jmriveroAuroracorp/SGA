using SGA_Desktop.Helpers;
using System.Windows;
using System.Windows.Controls;

namespace SGA_Desktop
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			NavigationStore.MainFrame = MainFrame;
			DataContext = new ViewModels.MainViewModel(); // <- FALTA ESTA LÍNEA
		}


		
	}
}
