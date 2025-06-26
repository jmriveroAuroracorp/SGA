using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
	public partial class UbicacionDialogViewModel : ObservableObject
	{
		private readonly UbicacionesService _svc;
		private readonly PaletService _paletService;
		private readonly bool _isNew;

		public string DialogTitle => _isNew ? "Nueva Ubicación" : "Editar Ubicación";
		public string SaveButtonText => _isNew ? "Crear" : "Actualizar";

		public short CodigoEmpresa { get; }
		public string CodigoAlmacen { get; }

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
		private string codigoUbicacion;

		[ObservableProperty] private string? descripcionUbicacion;
		[ObservableProperty] private int? pasillo;
		[ObservableProperty] private int? estanteria;
		[ObservableProperty] private int? altura;
		[ObservableProperty] private int? posicion;
		[ObservableProperty] private int? temperaturaMin;
		[ObservableProperty] private int? temperaturaMax;
		[ObservableProperty] private string? tipoPaletPermitido;
		[ObservableProperty] private bool? habilitada;
		[ObservableProperty] private short? tipoUbicacionId;
		[ObservableProperty] private int? orden;
		[ObservableProperty] private decimal? peso;
		[ObservableProperty] private decimal? dimensionX;
		[ObservableProperty] private decimal? dimensionY;
		[ObservableProperty] private decimal? dimensionZ;
		[ObservableProperty] private decimal? angulo;

		public ObservableCollection<TipoUbicacionDto> TiposDisponibles { get; } = new();
		public ObservableCollection<AlergenoSeleccionable> AlergenosDisponibles { get; } = new();
		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; } = new();

		public IRelayCommand SaveCommand { get; }
		public IRelayCommand CancelCommand { get; }

		public UbicacionDialogViewModel(
	UbicacionesService svc,
	PaletService paletService,
	short codigoEmpresa,
	string codigoAlmacen,
	UbicacionDetalladaDto? existing = null)
		{
			_svc = svc;
			_paletService = paletService;
			_isNew = existing == null;

			SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
			CancelCommand = new RelayCommand(Cancel);

			CodigoEmpresa = codigoEmpresa;
			CodigoAlmacen = codigoAlmacen;

			if (existing != null)
			{
				// *** AQUÍ SE USAN LAS PROPIEDADES, NO LOS CAMPOS PRIVADOS ***
				CodigoUbicacion = existing.Ubicacion;
				DescripcionUbicacion = existing.DescripcionUbicacion;
				Pasillo = existing.Pasillo;
				Estanteria = existing.Estanteria;
				Altura = existing.Altura;
				Posicion = existing.Posicion;
				TemperaturaMin = existing.TemperaturaMin;
				TemperaturaMax = existing.TemperaturaMax;
				TipoPaletPermitido = existing.TipoPaletPermitido;
				Habilitada = existing.Habilitada;
				TipoUbicacionId = existing.TipoUbicacionId;
				Orden = existing.Orden;
				Peso = existing.Peso;
				DimensionX = existing.DimensionX;
				DimensionY = existing.DimensionY;
				DimensionZ = existing.DimensionZ;
				Angulo = existing.Angulo;

				// precargar alérgenos seleccionados:
				var codigos = existing.AlergenosPermitidosList.Select(a => a.Codigo).ToList();
				_ = LoadAlergenosAsync(codigos);
			}
			else
			{
				// valores por defecto al crear
				Habilitada = true;
				CodigoUbicacion = string.Empty;
				_ = LoadAlergenosAsync();
			}

			_ = LoadTiposAsync();
			_ = LoadTiposPaletAsync();
		}


		private bool CanSave() => !string.IsNullOrWhiteSpace(codigoUbicacion);

		private async Task SaveAsync()
		{
			var dto = new CrearUbicacionDetalladaDto
			{
				CodigoEmpresa = CodigoEmpresa,
				CodigoAlmacen = CodigoAlmacen,
				CodigoUbicacion = codigoUbicacion,
				DescripcionUbicacion = descripcionUbicacion,
				Pasillo = pasillo,
				Estanteria = estanteria,
				Altura = altura,
				Posicion = posicion,
				TemperaturaMin = temperaturaMin,
				TemperaturaMax = temperaturaMax,
				TipoPaletPermitido = tipoPaletPermitido,
				Habilitada = habilitada,
				TipoUbicacionId = tipoUbicacionId,
				Orden = orden,
				Peso = peso,
				DimensionX = dimensionX,
				DimensionY = dimensionY,
				DimensionZ = dimensionZ,
				Angulo = angulo,
				AlergenosPermitidos = AlergenosDisponibles.Where(a => a.IsSelected)
														   .Select(a => a.Codigo)
														   .ToList()
			};

			bool ok;
			string? error = null;

			if (_isNew)
			{
				// El POST sigue devolviendo un bool
				ok = await _svc.CrearUbicacionDetalladaAsync(dto);
				if (!ok)
					error = "El servidor falló al crear la ubicación.";
			}
			else
			{
				// El PUT ahora devuelve un tuple
				var result = await _svc.ActualizarUbicacionDetalladaAsync(dto);
				ok = result.Success;
				error = result.ErrorMessage;
			}

			if (!ok)
			{
				MessageBox.Show(
					$"Error al {(_isNew ? "crear" : "actualizar")} ubicación:\n{error}",
					"SGA",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return;
			}

			// Si todo va bien, cerramos el diálogo
			var wnd = Application.Current.Windows
							  .OfType<Window>()
							  .SingleOrDefault(w => w.IsActive);
			if (wnd != null)
			{
				wnd.DialogResult = true;
				wnd.Close();
			}
		}

		private async Task LoadTiposAsync()
		{
			var lista = await _svc.ObtenerTiposUbicacionAsync();
			TiposDisponibles.Clear();
			foreach (var t in lista) TiposDisponibles.Add(t);
			if (!_isNew && tipoUbicacionId.HasValue)
				OnPropertyChanged(nameof(tipoUbicacionId));
		}

		private async Task LoadAlergenosAsync(IList<short>? permitidos)
		{
			var listaMaestra = await _svc.ObtenerAlergenosMaestrosAsync();
			AlergenosDisponibles.Clear();
			foreach (var dto in listaMaestra)
			{
				var sel = new AlergenoSeleccionable(dto)
				{
					IsSelected = permitidos?.Contains(dto.Codigo) == true
				};
				AlergenosDisponibles.Add(sel);
			}
		}

		private Task LoadAlergenosAsync() => LoadAlergenosAsync(null);

		private async Task LoadTiposPaletAsync()
		{
			var lista = await _paletService.ObtenerTiposPaletAsync();
			TiposPaletDisponibles.Clear();
			foreach (var p in lista) TiposPaletDisponibles.Add(p);
			if (!_isNew && !string.IsNullOrEmpty(tipoPaletPermitido))
				OnPropertyChanged(nameof(tipoPaletPermitido));
		}

		private void Cancel()
		{
			var wnd = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.IsActive);
			wnd?.Close();
		}
	}
}
