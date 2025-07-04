using SGA_Desktop.Services;
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
	/// Lógica de interacción para PaletCrearDialog.xaml
	/// </summary>
	public partial class PaletCrearDialog : Window
	{
		public PaletCrearDialog()
		{
			InitializeComponent();
			DataContext = new PaletCrearDialogViewModel(new PaletService());
		}
	}
}
