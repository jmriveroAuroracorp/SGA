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
	/// Lógica de interacción para EmpresaView.xaml
	/// </summary>
	public partial class EmpresaView : Page
	{
		public EmpresaView()
		{
			InitializeComponent();
			DataContext = new ViewModels.EmpresaViewModel(new Services.LoginService()); // 👈 Esto soluciona el error
		}
	}
}
