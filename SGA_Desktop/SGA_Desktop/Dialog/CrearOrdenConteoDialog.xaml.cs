using SGA_Desktop.ViewModels;
using System.Windows;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Lógica de interacción para CrearOrdenConteoDialog.xaml
    /// </summary>
    public partial class CrearOrdenConteoDialog : Window
    {
        public CrearOrdenConteoDialogViewModel ViewModel { get; private set; }

        public CrearOrdenConteoDialog()
        {
            InitializeComponent();
            ViewModel = (CrearOrdenConteoDialogViewModel)DataContext;
            ViewModel.DialogResult = this;
        }

        /// <summary>
        /// Constructor que acepta un ViewModel externo
        /// </summary>
        /// <param name="viewModel">ViewModel a usar</param>
        public CrearOrdenConteoDialog(CrearOrdenConteoDialogViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
            ViewModel.DialogResult = this;
        }
    }
} 