using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
	public partial class EditarUbicacionesMasivoDialogViewModel : ObservableObject
	{
		private readonly UbicacionesService _ubicService;
		private readonly PaletService _paletService;
		private readonly List<UbicacionDetalladaDto> _ubicacionesSeleccionadas;
		private readonly short _codigoEmpresa;

		public string Titulo => $"Editar {_ubicacionesSeleccionadas.Count} ubicaciones";

		// Campos editables en masa
		[ObservableProperty]
		private bool aplicarObsoleta = false;
		[ObservableProperty]
		private bool esObsoleta = false;

		[ObservableProperty]
		private bool aplicarHabilitada = false;
		[ObservableProperty]
		private bool habilitada = true;

		[ObservableProperty]
		private bool aplicarTemperaturaMin = false;
		[ObservableProperty]
		private int? temperaturaMin;

		[ObservableProperty]
		private bool aplicarTemperaturaMax = false;
		[ObservableProperty]
		private int? temperaturaMax;

		[ObservableProperty]
		private bool aplicarTipoPalet = false;
		[ObservableProperty]
		private string? tipoPaletPermitido;

		[ObservableProperty]
		private bool aplicarTipoUbicacion = false;
		[ObservableProperty]
		private short? tipoUbicacionId;

		[ObservableProperty]
		private bool aplicarAlergenos = false;

		// Colecciones
		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; } = new();
		public ObservableCollection<TipoUbicacionDto> TiposUbicacionDisponibles { get; } = new();
		public ObservableCollection<AlergenoSeleccionable> AlergenosDisponibles { get; } = new();

		// Preview de cambios
		public ObservableCollection<PreviewCambioUbicacion> PreviewCambios { get; } = new();

		[ObservableProperty]
		private bool isBusy = false;

		public IRelayCommand CancelCommand { get; }
		public IAsyncRelayCommand GuardarCommand { get; }

		public Action? CloseAction { get; set; }

		public EditarUbicacionesMasivoDialogViewModel(
			List<UbicacionDetalladaDto> ubicacionesSeleccionadas,
			UbicacionesService ubicService,
			PaletService paletService,
			short codigoEmpresa)
		{
			_ubicacionesSeleccionadas = ubicacionesSeleccionadas ?? throw new ArgumentNullException(nameof(ubicacionesSeleccionadas));
			_ubicService = ubicService ?? throw new ArgumentNullException(nameof(ubicService));
			_paletService = paletService ?? throw new ArgumentNullException(nameof(paletService));
			_codigoEmpresa = codigoEmpresa;

			CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
			GuardarCommand = new AsyncRelayCommand(GuardarAsync, () => !IsBusy && TieneCambios());

			_ = InitializeAsync();
		}

		private async Task InitializeAsync()
		{
			IsBusy = true;
			try
			{
				// Cargar tipos de palet
				var tiposPalet = await _paletService.ObtenerTiposPaletAsync();
				TiposPaletDisponibles.Clear();
				foreach (var tp in tiposPalet)
					TiposPaletDisponibles.Add(tp);

				// Cargar tipos de ubicación
				var tiposUbicacion = await _ubicService.ObtenerTiposUbicacionAsync();
				TiposUbicacionDisponibles.Clear();
				foreach (var tu in tiposUbicacion)
					TiposUbicacionDisponibles.Add(tu);

				// Cargar alérgenos
				var alergenos = await _ubicService.ObtenerAlergenosMaestrosAsync();
				AlergenosDisponibles.Clear();
				foreach (var a in alergenos)
					AlergenosDisponibles.Add(new AlergenoSeleccionable(a));

				// Inicializar preview
				ActualizarPreview();
			}
			finally
			{
				IsBusy = false;
			}
		}

		partial void OnAplicarObsoletaChanged(bool value) => ActualizarPreview();
		partial void OnEsObsoletaChanged(bool value) => ActualizarPreview();
		partial void OnAplicarHabilitadaChanged(bool value) => ActualizarPreview();
		partial void OnHabilitadaChanged(bool value) => ActualizarPreview();
		partial void OnAplicarTemperaturaMinChanged(bool value) => ActualizarPreview();
		partial void OnTemperaturaMinChanged(int? value) => ActualizarPreview();
		partial void OnAplicarTemperaturaMaxChanged(bool value) => ActualizarPreview();
		partial void OnTemperaturaMaxChanged(int? value) => ActualizarPreview();
		partial void OnAplicarTipoPaletChanged(bool value) => ActualizarPreview();
		partial void OnTipoPaletPermitidoChanged(string? value) => ActualizarPreview();
		partial void OnAplicarTipoUbicacionChanged(bool value) => ActualizarPreview();
		partial void OnTipoUbicacionIdChanged(short? value) => ActualizarPreview();
		partial void OnAplicarAlergenosChanged(bool value) => ActualizarPreview();

		private void ActualizarPreview()
		{
			PreviewCambios.Clear();
			foreach (var u in _ubicacionesSeleccionadas)
			{
				var preview = new PreviewCambioUbicacion
				{
					CodigoUbicacion = u.Ubicacion,
					DescripcionUbicacion = u.DescripcionUbicacion,
					Cambios = new List<string>()
				};

				if (AplicarObsoleta)
					preview.Cambios.Add($"Obsoleta: {(EsObsoleta ? "Sí" : "No")}");
				if (AplicarHabilitada)
					preview.Cambios.Add($"Habilitada: {(Habilitada ? "Sí" : "No")}");
				if (AplicarTemperaturaMin && TemperaturaMin.HasValue)
					preview.Cambios.Add($"Temp. Mín: {TemperaturaMin}°C");
				if (AplicarTemperaturaMax && TemperaturaMax.HasValue)
					preview.Cambios.Add($"Temp. Máx: {TemperaturaMax}°C");
				if (AplicarTipoPalet && !string.IsNullOrEmpty(TipoPaletPermitido))
					preview.Cambios.Add($"Tipo Palet: {TipoPaletPermitido}");
				if (AplicarTipoUbicacion && TipoUbicacionId.HasValue)
				{
					var tipo = TiposUbicacionDisponibles.FirstOrDefault(t => t.TipoUbicacionId == TipoUbicacionId);
					preview.Cambios.Add($"Tipo Ubicación: {tipo?.Descripcion ?? TipoUbicacionId.ToString()}");
				}
				if (AplicarAlergenos)
				{
					var alergenosSeleccionados = AlergenosDisponibles.Where(a => a.IsSelected).Select(a => a.Descripcion).ToList();
					preview.Cambios.Add($"Alérgenos: {(alergenosSeleccionados.Any() ? string.Join(", ", alergenosSeleccionados) : "Ninguno")}");
				}

				PreviewCambios.Add(preview);
			}
			GuardarCommand.NotifyCanExecuteChanged();
		}

		private bool TieneCambios()
		{
			return AplicarObsoleta || AplicarHabilitada || AplicarTemperaturaMin || AplicarTemperaturaMax ||
				   AplicarTipoPalet || AplicarTipoUbicacion || AplicarAlergenos;
		}

		private async Task GuardarAsync()
		{
			if (!TieneCambios())
			{
				MessageBox.Show("No hay cambios para aplicar.", "Sin cambios", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Confirmación
			var confirm = new ConfirmationDialog(
				"Confirmar edición masiva",
				$"Se aplicarán cambios a {_ubicacionesSeleccionadas.Count} ubicaciones.\n¿Deseas continuar?",
				"\uE946"
			);
			var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
				?? Application.Current.MainWindow;
			if (owner != null && owner != confirm)
				confirm.Owner = owner;
			if (confirm.ShowDialog() != true)
				return;

			IsBusy = true;
			int exitosas = 0;
			int fallidas = 0;
			List<string> errores = new();

			try
			{
				foreach (var ubicacion in _ubicacionesSeleccionadas)
				{
					try
					{
						// Crear DTO con valores actuales
						var dto = new CrearUbicacionDetalladaDto
						{
							CodigoEmpresa = ubicacion.CodigoEmpresa,
							CodigoAlmacen = ubicacion.CodigoAlmacen,
							CodigoUbicacion = ubicacion.Ubicacion,
							DescripcionUbicacion = ubicacion.DescripcionUbicacion,
							Pasillo = ubicacion.Pasillo,
							Estanteria = ubicacion.Estanteria,
							Altura = ubicacion.Altura,
							Posicion = ubicacion.Posicion,
							Orden = ubicacion.Orden,
							Peso = ubicacion.Peso,
							Alto = ubicacion.Alto,
							DimensionX = ubicacion.DimensionX,
							DimensionY = ubicacion.DimensionY,
							DimensionZ = ubicacion.DimensionZ,
							Angulo = ubicacion.Angulo,
							// Aplicar cambios solo si están marcados
							Obsoleta = AplicarObsoleta ? (EsObsoleta ? 1 : 0) : ubicacion.Obsoleta,
							Habilitada = AplicarHabilitada ? Habilitada : ubicacion.Habilitada,
							TemperaturaMin = AplicarTemperaturaMin ? TemperaturaMin : ubicacion.TemperaturaMin,
							TemperaturaMax = AplicarTemperaturaMax ? TemperaturaMax : ubicacion.TemperaturaMax,
							TipoPaletPermitido = AplicarTipoPalet ? TipoPaletPermitido : ubicacion.TipoPaletPermitido,
							TipoUbicacionId = AplicarTipoUbicacion ? TipoUbicacionId : ubicacion.TipoUbicacionId,
							AlergenosPermitidos = AplicarAlergenos
								? AlergenosDisponibles.Where(a => a.IsSelected).Select(a => a.Codigo).ToList()
								: ubicacion.AlergenosPermitidosList.Select(a => a.Codigo).ToList()
						};

						var result = await _ubicService.ActualizarUbicacionDetalladaAsync(dto);
						if (result.Success)
							exitosas++;
						else
						{
							fallidas++;
							errores.Add($"{ubicacion.Ubicacion}: {result.ErrorMessage}");
						}
					}
					catch (Exception ex)
					{
						fallidas++;
						errores.Add($"{ubicacion.Ubicacion}: {ex.Message}");
					}
				}

				// Mostrar resultado
				string mensaje = $"Proceso completado:\n✓ {exitosas} actualizadas correctamente";
				if (fallidas > 0)
					mensaje += $"\n✗ {fallidas} con errores";
				if (errores.Any())
					mensaje += "\n\nErrores:\n" + string.Join("\n", errores.Take(5));
				if (errores.Count > 5)
					mensaje += $"\n... y {errores.Count - 5} más";

				var dialog = new WarningDialog(
					fallidas > 0 ? "Edición completada con errores" : "Edición completada",
					mensaje,
					fallidas > 0 ? "\uE814" : "\uE73E"
				);
				if (owner != null && owner != dialog)
					dialog.Owner = owner;
				dialog.ShowDialog();

				if (exitosas > 0)
					CloseAction?.Invoke();
			}
			finally
			{
				IsBusy = false;
			}
		}
	}

	// Clase para preview de cambios
	public class PreviewCambioUbicacion
	{
		public string CodigoUbicacion { get; set; } = "";
		public string DescripcionUbicacion { get; set; } = "";
		public List<string> Cambios { get; set; } = new();
		public string CambiosTexto => string.Join("; ", Cambios);
	}
}

