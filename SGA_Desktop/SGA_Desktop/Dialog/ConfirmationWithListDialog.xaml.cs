using SGA_Desktop.Helpers;
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
using System.Windows.Shapes;

namespace SGA_Desktop.Dialog
{
	/// <summary>
	/// Lógica de interacción para ConfirmationWithListDialog.xaml
	/// </summary>
	public partial class ConfirmationWithListDialog : Window
	{
		public ConfirmationWithListDialogViewModel VM { get; }

		//public ConfirmationWithListDialog(
		//	IEnumerable<LineaPaletDto> lineas,
		//	IEnumerable<UbicacionDetalladaDto> ubicaciones,
		//	IEnumerable<AlmacenDto> almacenes)
		//{
		//	InitializeComponent();
		//	VM = new ConfirmationWithListDialogViewModel(
		//		lineas,
		//		ubicaciones,
		//		almacenes
		//	);
		//	DataContext = VM;
		//}

		public ConfirmationWithListDialog(
		IEnumerable<LineaPaletDto> lineas,
		IEnumerable<AlmacenDto> almacenes,
		UbicacionesService ubicacionesService)
		{
			InitializeComponent();

			VM = new ConfirmationWithListDialogViewModel(lineas, almacenes, ubicacionesService);
			DataContext = VM;
		}



		public UbicacionDto? UbicacionSeleccionada => VM.UbicacionSeleccionada;

		private async void YesButton_Click(object sender, RoutedEventArgs e)
		{
			if (UbicacionSeleccionada == null)
			{
				MessageBox.Show("Por favor selecciona una ubicación.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			// 🔷 NUEVO: Validar traspaso de palet antes de confirmar
			var validacion = await ValidarTraspasoPaletAsync();
			if (!validacion.EsValido)
			{
				var errorDialog = new WarningDialog("Traspaso Bloqueado", validacion.MotivoBloqueo, "\uE72E");
				errorDialog.ShowDialog();
				return;
			}

			DialogResult = true;
			Close();
		}

		private void NoButton_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		// 🔷 NUEVO: Validar traspaso de palet antes de confirmar
		private async Task<ValidacionTraspasoResult> ValidarTraspasoPaletAsync()
		{
			try
			{
				if (UbicacionSeleccionada == null)
					return ValidacionTraspasoResult.Valido();

				// 1. Obtener códigos de artículos únicos de las líneas del palet
				var codigosArticulos = VM.Lineas
					.Select(l => l.CodigoArticulo)
					.Where(c => !string.IsNullOrEmpty(c))
					.Distinct()
					.ToList();

				if (!codigosArticulos.Any())
					return ValidacionTraspasoResult.Valido();

				// 2. Consultar bloqueos de calidad para los artículos del palet
				var stockService = new StockService();
				var bloqueosCalidad = await stockService.ObtenerBloqueosCalidadAsync(
					SessionManager.EmpresaSeleccionada!.Value, 
					codigosArticulos);

				// 3. Verificar si algún artículo del palet está bloqueado por calidad
				var articulosBloqueados = codigosArticulos
					.Where(codigo => bloqueosCalidad.ContainsKey(codigo) && 
								   bloqueosCalidad[codigo].IsBloqueado)
					.ToList();

				if (!articulosBloqueados.Any())
					return ValidacionTraspasoResult.Valido(); // No hay artículos bloqueados

				// 4. Validar cada artículo bloqueado individualmente con su partida y ubicación origen
				var ubicacionDestino = UbicacionSeleccionada.Ubicacion;
				var traspasosService = new TraspasosService();
				foreach (var codigoArticulo in articulosBloqueados)
				{
					// Obtener la primera línea de este artículo para obtener partida y ubicación origen
					var lineaArticulo = VM.Lineas.FirstOrDefault(l => l.CodigoArticulo == codigoArticulo);
					
					var request = new ValidacionTraspasoRequest
					{
						CodigoArticulo = codigoArticulo,
						AlmacenDestino = VM.AlmacenDestinoSeleccionado?.CodigoAlmacen ?? "",
						UbicacionDestino = ubicacionDestino,
						CodigoEmpresa = SessionManager.EmpresaSeleccionada!.Value,
						Partida = lineaArticulo?.Lote,
						// 🔷 NUEVO: Incluir ubicación origen de la línea para verificar bloqueos específicos
						AlmacenOrigen = lineaArticulo?.CodigoAlmacen,
						UbicacionOrigen = lineaArticulo?.Ubicacion
					};

					var resultado = await traspasosService.ValidarTraspasoArticuloAsync(request);
					
					if (!resultado.EsValido)
					{
						return ValidacionTraspasoResult.Bloqueado(
							$"No se puede traspasar el palet. {resultado.MotivoBloqueo}");
					}
				}

				return ValidacionTraspasoResult.Valido();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error validando traspaso de palet: {ex.Message}");
				// En caso de error, permitir traspaso para no bloquear operaciones
				return ValidacionTraspasoResult.Valido();
			}
		}
	}

}
