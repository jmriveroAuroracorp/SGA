using SGA_Desktop.Models;
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

namespace SGA_Desktop.Views
{
	/// <summary>
	/// Lógica de interacción para ConfiguracionUbicacionView.xaml
	/// </summary>
	public partial class ConfiguracionUbicacionView : Page
	{
		public ConfiguracionUbicacionView(UbicacionDetalladaDto dto)
		{
			InitializeComponent();
			DataContext = new ConfiguracionUbicacionViewModel(dto, new UbicacionesService());
		}
	}

}
