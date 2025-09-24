using SGA_Desktop.Services;
using SGA_Desktop.ViewModels;
using System.Windows.Controls;

namespace SGA_Desktop.Views
{
    /// <summary>
    /// Lógica de interacción para ConfiguracionOperariosView.xaml
    /// </summary>
    public partial class ConfiguracionOperariosView : Page
    {
        public ConfiguracionOperariosView()
        {
            InitializeComponent();
            
            // Configurar DataContext con servicios
            DataContext = new ConfiguracionOperariosViewModel(new OperariosConfiguracionService());
        }
    }
}
