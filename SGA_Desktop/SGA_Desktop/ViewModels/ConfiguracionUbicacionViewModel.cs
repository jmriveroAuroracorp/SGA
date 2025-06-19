using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Windows;

public partial class ConfiguracionUbicacionViewModel : ObservableObject
{
	private readonly UbicacionesService _svc;
	private readonly UbicacionDetalladaDto _original;

	public ConfiguracionUbicacionViewModel(
		UbicacionDetalladaDto dto,
		UbicacionesService svc)
	{
		_original = dto;
		_svc = svc;

		// Inicializa las props ligadas:
		Ubicacion = dto.Ubicacion;
		DescripcionUbicacion = dto.DescripcionUbicacion;
		TipoUbicacion = dto.TipoUbicacion;
		Habilitada = dto.Habilitada;
		TiposDisponibles = new List<string> { "Picking", "Almacenaje", "..." };

		//GuardarCommand = new RelayCommand(async () => await GuardarAsync());
	}

	[ObservableProperty] private string ubicacion;
	[ObservableProperty] private string descripcionUbicacion;
	[ObservableProperty] private string tipoUbicacion;
	[ObservableProperty] private bool habilitada;
	public List<string> TiposDisponibles { get; }

	public IRelayCommand GuardarCommand { get; }

	//private async Task GuardarAsync()
	//{
	//	try
	//	{
	//		// Montas un DTO de envío, copiando cambios:
	//		var updateDto = new ConfiguracionUbicacionDto
	//		{
	//			CodigoEmpresa = _original.CodigoEmpresa,
	//			CodigoAlmacen = _original.CodigoAlmacen,
	//			Ubicacion = _original.Ubicacion,
	//			DescripcionUbicacion = DescripcionUbicacion,
	//			TipoUbicacion = TipoUbicacion,
	//			Habilitada = Habilitada
	//			// …más campos…
	//		};

	//		var ok = await _svc.ActualizarConfiguracionAsync(updateDto);
	//		if (ok)
	//			MessageBox.Show("Guardado correctamente.", "OK",
	//				MessageBoxButton.OK, MessageBoxImage.Information);
	//		else
	//			MessageBox.Show("No se pudo guardar.", "Error",
	//				MessageBoxButton.OK, MessageBoxImage.Error);
	//	}
	//	catch (Exception ex)
	//	{
	//		MessageBox.Show(ex.Message, "Error al guardar",
	//			MessageBoxButton.OK, MessageBoxImage.Error);
	//	}
	//}
}
