using System.Windows;
using System.Windows.Input;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para CrearInventarioDialog.xaml
    /// </summary>
    public partial class CrearInventarioDialog : Window
    {
        public CrearInventarioDialog()
        {
            InitializeComponent();
        }

        private void AlmacenItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border && 
                border.DataContext is AlmacenDto almacen &&
                DataContext is CrearInventarioDialogViewModel viewModel)
            {
                if (viewModel.ModoMultialmacen)
                {
                    // Modo multialmacén: toggle individual
                    almacen.IsSelected = !almacen.IsSelected;
                }
                else
                {
                    // Modo único: desmarcar todos los demás
                    foreach (var a in viewModel.AlmacenesDisponibles)
                    {
                        a.IsSelected = false;
                    }
                    
                    // Marcar el seleccionado
                    almacen.IsSelected = true;
                    viewModel.AlmacenSeleccionado = almacen;
                }
            }
        }
    }
} 