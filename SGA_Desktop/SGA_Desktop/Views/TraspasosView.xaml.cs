using SGA_Desktop.Helpers;
using SGA_Desktop.ViewModels;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
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
	public partial class TraspasosView : Page
	{
	public TraspasosView()
	{
		InitializeComponent();
		// Ya no necesitamos frames - todo se maneja directamente en la vista
	}

        private void PaletCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid && grid.Tag is PaletDto palet && DataContext is TraspasosViewModel vm)
            {
                vm.PaletSeleccionado = palet;
            }
        }

        private void AbrirPaletLineas_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PaletDto palet && DataContext is TraspasosViewModel vm)
            {
                vm.PaletSeleccionado = palet;
                vm.AbrirPaletLineasCommand.Execute(null);
            }
        }

        private async void CerrarPalet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PaletDto palet && DataContext is TraspasosViewModel vm)
            {
                vm.PaletSeleccionado = palet;
                await vm.CerrarPaletCommand.ExecuteAsync(null);
            }
        }

        private async void ReabrirPalet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PaletDto palet && DataContext is TraspasosViewModel vm)
            {
                vm.PaletSeleccionado = palet;
                await vm.ReabrirPaletCommand.ExecuteAsync(null);
            }
        }

        private async void TraspasarPalet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PaletDto palet && DataContext is TraspasosViewModel vm)
            {
                vm.PaletSeleccionado = palet;
                await vm.TraspasarPaletCommand.ExecuteAsync(null);
            }
        }

        private async void ImprimirPalet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PaletDto palet && DataContext is TraspasosViewModel vm)
            {
                vm.PaletSeleccionado = palet;
                await vm.ImprimirPaletCommand.ExecuteAsync(null);
            }
        }
	}
}