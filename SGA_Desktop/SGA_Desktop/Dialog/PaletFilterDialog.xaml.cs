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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para PaletFilterDialog.xaml
	/// </summary>
	public partial class PaletFilterDialog : Window
	{
		private readonly PaletFilterDialogViewModel _vm;   // ← Declara aquí
		public PaletFilterDialog()
		{
			InitializeComponent();
			_vm = new PaletFilterDialogViewModel(new PaletService());
			DataContext = _vm;

			Loaded += async (_, __) => await _vm.InitializeAsync();
		}
	}
}
