using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services; // Asegúrate que esta carpeta existe y contiene LoginService
using System.Windows;

namespace SGA_Desktop
{
	/// <summary>
	/// Lógica de interacción para Login.xaml
	/// </summary>
	public partial class Login : Window
	{
		public Login()
		{
			InitializeComponent();
			DataContext = new ViewModels.LoginViewModel();
		}

		private void BtnSalir_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}
	}
}
