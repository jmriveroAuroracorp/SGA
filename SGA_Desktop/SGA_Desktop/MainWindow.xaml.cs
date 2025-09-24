using System.Windows;
using SGA_Desktop.Helpers;
using System.Windows.Input;
using System.Threading.Tasks;

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

		private async void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			// Obtener el ViewModel y ejecutar el comando de cerrar sesión
			if (DataContext is ViewModels.MainViewModel mainViewModel)
			{
				await mainViewModel.CerrarSesionCommand.ExecuteAsync(null);
			}
			else
			{
				// Fallback si no hay ViewModel disponible
				Close();
			}
		}

		private void CustomTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}
	}
}
