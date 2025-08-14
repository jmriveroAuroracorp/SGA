using System.Windows;
using SGA_Desktop.Helpers;
using System.Windows.Input;

namespace SGA_Desktop
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			// ← Aquí asignas el Frame definido en el XAML
			NavigationStore.MainFrame = MainFrame;

			// Luego ajustas el DataContext de tu VM
			DataContext = new ViewModels.MainViewModel(new Services.LoginService());

			//// (Opcional) Navega inmediatamente a la vista inicial
			//NavigationStore.Navigate("ConsultaStock");
		}

		private void MinimizeButton_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}
	}
}
