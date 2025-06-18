using System.Windows;
using SGA_Desktop.Helpers;

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
	}
}
