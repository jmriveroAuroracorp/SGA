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
using System.Windows.Shapes;

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para TraspasoPaletDialog.xaml
	/// </summary>
	public partial class TraspasoPaletDialog : Window
	{
		public TraspasoPaletDialog()
		{
			InitializeComponent();
			this.DataContext = new SGA_Desktop.ViewModels.TraspasoPaletDialogViewModel();
		}

		public TraspasoPaletDialog(SGA_Desktop.Models.PaletDto palet)
		{
			InitializeComponent();
			this.DataContext = new SGA_Desktop.ViewModels.TraspasoPaletDialogViewModel(palet);
		}

		private void Cerrar_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
	}
}
