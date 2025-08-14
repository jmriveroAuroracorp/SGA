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
		private int? pasillo;
		partial void OnPasilloChanged(int? oldValue, int? newValue) => UpdateCodigoYDescripcion();

		[ObservableProperty]
		private int? estanteria;
		partial void OnEstanteriaChanged(int? oldValue, int? newValue) => UpdateCodigoYDescripcion();

		[ObservableProperty]
		private int? altura;
		partial void OnAlturaChanged(int? oldValue, int? newValue) => UpdateCodigoYDescripcion();

		[ObservableProperty]
		private int? posicion;
		partial void OnPosicionChanged(int? oldValue, int? newValue) => UpdateCodigoYDescripcion();

		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
		private string codigoUbicacion = string.Empty;
		partial void OnCodigoUbicacionChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanEditCodigo));
		}
		public bool CanEditCodigo => _isNew && !string.IsNullOrEmpty(CodigoUbicacion);

		[ObservableProperty]
		private string? descripcionUbicacion;
		partial void OnDescripcionUbicacionChanged(string oldValue, string newValue)
		{
			OnPropertyChanged(nameof(CanEditDescripcion));
		}
		public bool CanEditDescripcion => _isNew && !string.IsNullOrEmpty(DescripcionUbicacion);

		[ObservableProperty] private int? temperaturaMin;
		[ObservableProperty] private int? temperaturaMax;
		[ObservableProperty] private string? tipoPaletPermitido;
		[ObservableProperty] private bool? habilitada;
		[ObservableProperty] private short? tipoUbicacionId;
		[ObservableProperty] private int? orden;
		[ObservableProperty] private decimal? peso;
		[ObservableProperty] private decimal? alto;
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
				// EN EDICIÓN: tomar valores existentes y NO permitir regenerar
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
				Alto = existing.Alto;
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
				// CREACIÓN: valores por defecto
				Habilitada = true;
				_ = LoadAlergenosAsync();
			}

			_ = LoadTiposAsync();
			_ = LoadTiposPaletAsync();
		}

		private bool CanSave() => !string.IsNullOrWhiteSpace(CodigoUbicacion);

		private async Task SaveAsync()
		{
			var dto = new CrearUbicacionDetalladaDto
			{
				CodigoEmpresa = CodigoEmpresa,
				CodigoAlmacen = CodigoAlmacen,
				CodigoUbicacion = CodigoUbicacion,
				DescripcionUbicacion = DescripcionUbicacion,
				Pasillo = Pasillo,
				Estanteria = Estanteria,
				Altura = Altura,
				Posicion = Posicion,
				TemperaturaMin = TemperaturaMin,
				TemperaturaMax = TemperaturaMax,
				TipoPaletPermitido = TipoPaletPermitido,
				Habilitada = Habilitada,
				TipoUbicacionId = TipoUbicacionId,
				Orden = Orden,
				Peso = Peso,
				Alto = Alto,
				DimensionX = DimensionX,
				DimensionY = DimensionY,
				DimensionZ = DimensionZ,
				Angulo = Angulo,
				AlergenosPermitidos = AlergenosDisponibles.Where(a => a.IsSelected)
													 .Select(a => a.Codigo)
													 .ToList()
			};

			bool ok;
			string? error = null;

			if (_isNew)
			{
				ok = await _svc.CrearUbicacionDetalladaAsync(dto);
				if (!ok) error = "El servidor falló al crear la ubicación.";
			}
			else
			{
				var result = await _svc.ActualizarUbicacionDetalladaAsync(dto);
				ok = result.Success;
				error = result.ErrorMessage;
			}

			if (!ok)
			{
				var dialog = new SGA_Desktop.Dialog.ConfirmationDialog(
					$"Error al {(_isNew ? "crear" : "actualizar")} ubicación",
					error ?? "Error desconocido",
					"\uE814" // icono de advertencia
				);
				var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
						 ?? System.Windows.Application.Current.MainWindow;
				if (owner != null && owner != dialog)
					dialog.Owner = owner;
				dialog.ShowDialog();
				return;
			}

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
			if (!_isNew && TipoUbicacionId.HasValue)
				OnPropertyChanged(nameof(TipoUbicacionId));
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
			if (!_isNew && !string.IsNullOrEmpty(TipoPaletPermitido))
				OnPropertyChanged(nameof(TipoPaletPermitido));
		}

		private void Cancel()
		{
			var wnd = Application.Current.Windows
							  .OfType<Window>()
							  .SingleOrDefault(w => w.IsActive);
			wnd?.Close();
		}

		private void UpdateCodigoYDescripcion()
		{
			if (!_isNew)
				return;

			if (Pasillo == null || Estanteria == null || Altura == null || Posicion == null)
			{
				CodigoUbicacion = string.Empty;
				DescripcionUbicacion = string.Empty;
			}
			else
			{
				CodigoUbicacion = $"UB{Pasillo:000}{Estanteria:000}{Altura:000}{Posicion:000}";
				DescripcionUbicacion = $"Pasillo {Pasillo}, Estantería {Estanteria}, Altura {Altura}, Posición {Posicion}";
			}

			OnPropertyChanged(nameof(CanEditCodigo));
			OnPropertyChanged(nameof(CanEditDescripcion));
			SaveCommand.NotifyCanExecuteChanged();
		}
	}
}
