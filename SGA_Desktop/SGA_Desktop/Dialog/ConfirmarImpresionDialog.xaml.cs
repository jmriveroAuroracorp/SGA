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
	/// Lógica de interacción para ConfirmarImpresionDialog.xaml
	/// </summary>
	public partial class ConfirmarImpresionDialog : Window
	{
		public ConfirmarImpresionDialog()
		{
			InitializeComponent();
			Loaded += (_, __) =>
			{
				if (DataContext is SGA_Desktop.ViewModels.ConfirmarImpresionDialogViewModel vm)
				{
					vm.RequestClose += r => { DialogResult = r; Close(); };
				}
			};
		}

		private void Aceptar_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}
	}
}
