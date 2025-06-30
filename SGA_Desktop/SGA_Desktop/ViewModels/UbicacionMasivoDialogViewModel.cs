using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace SGA_Desktop.ViewModels
{
	public partial class UbicacionMasivoDialogViewModel
		: ObservableObject, INotifyDataErrorInfo
	{
		private readonly string _codigoAlmacen;
		private readonly UbicacionesService _ubicacionService;
		private readonly PaletService _paletService;
		private readonly Dictionary<string, List<string>> _errors = new();
		private readonly string _descripcionAlmacen;

		public string TituloDisplay => _descripcionAlmacen;

		public UbicacionMasivoDialogViewModel(
			AlmacenDto almacen,
			UbicacionesService ubicService,
			PaletService paletService)
		{
			// 1) Inicializamos datos de cabecera
			_descripcionAlmacen = almacen.DescripcionCombo;
			_codigoAlmacen = almacen.CodigoAlmacen;
			_ubicacionService = ubicService ?? throw new ArgumentNullException(nameof(ubicService));
			_paletService = paletService ?? throw new ArgumentNullException(nameof(paletService));

			// 2) Colecciones vacías (se llenan en InitializeAsync)
			TiposPaletDisponibles = new ObservableCollection<TipoPaletDto>();
			TiposUbicacionDisponibles = new ObservableCollection<TipoUbicacionDto>();
			AlergenosDisponibles = new ObservableCollection<AlergenoSeleccionable>();
			UbicacionesGeneradas = new ObservableCollection<CrearUbicacionDetalladaDto>();

			// 3) Comandos asíncronos
			CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
			GenerarCommand = new AsyncRelayCommand(GenerarAsync, () => !HasErrors);
			GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);

			// 4) Validamos rangos por si el usuario ya puso algo
			ValidateAllRanges();

			// 5) Carga inicial de palets, tipos y alérgenos
			_ = InitializeAsync();
		}

		// ——— Propiedades de rango ———
		[ObservableProperty] private int pasilloDesde;
		partial void OnPasilloDesdeChanged(int _, int __) { ValidateRange(nameof(PasilloDesde), nameof(PasilloHasta)); NotifyCommands(); }

		[ObservableProperty] private int pasilloHasta;
		partial void OnPasilloHastaChanged(int _, int __) { ValidateRange(nameof(PasilloDesde), nameof(PasilloHasta)); NotifyCommands(); }

		[ObservableProperty] private int estanteriaDesde;
		partial void OnEstanteriaDesdeChanged(int _, int __) { ValidateRange(nameof(EstanteriaDesde), nameof(EstanteriaHasta)); NotifyCommands(); }

		[ObservableProperty] private int estanteriaHasta;
		partial void OnEstanteriaHastaChanged(int _, int __) { ValidateRange(nameof(EstanteriaDesde), nameof(EstanteriaHasta)); NotifyCommands(); }

		[ObservableProperty] private int alturaDesde;
		partial void OnAlturaDesdeChanged(int _, int __) { ValidateRange(nameof(AlturaDesde), nameof(AlturaHasta)); NotifyCommands(); }

		[ObservableProperty] private int alturaHasta;
		partial void OnAlturaHastaChanged(int _, int __) { ValidateRange(nameof(AlturaDesde), nameof(AlturaHasta)); NotifyCommands(); }

		[ObservableProperty] private int posicionDesde;
		partial void OnPosicionDesdeChanged(int _, int __) { ValidateRange(nameof(PosicionDesde), nameof(PosicionHasta)); NotifyCommands(); }

		[ObservableProperty] private int posicionHasta;
		partial void OnPosicionHastaChanged(int _, int __) { ValidateRange(nameof(PosicionDesde), nameof(PosicionHasta)); NotifyCommands(); }

		// ——— Campos comunes de configuración ———
		[ObservableProperty] private int? temperaturaMin;
		[ObservableProperty] private int? temperaturaMax;
		[ObservableProperty] private string? tipoPaletPermitido;
		[ObservableProperty] private short? tipoUbicacionId;
		[ObservableProperty] private decimal? peso;
		[ObservableProperty] private decimal? alto;

		// ——— Colecciones para los dropdowns y alérgenos ———
		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }
		public ObservableCollection<TipoUbicacionDto> TiposUbicacionDisponibles { get; }
		public ObservableCollection<AlergenoSeleccionable> AlergenosDisponibles { get; }

		// ——— Preview de las ubicaciones generadas ———
		public ObservableCollection<CrearUbicacionDetalladaDto> UbicacionesGeneradas { get; }

		// ——— Comandos ———
		public IRelayCommand CancelCommand { get; }
		public IAsyncRelayCommand GenerarCommand { get; }
		public IAsyncRelayCommand GuardarCommand { get; }

		// ——— Cierre de ventana ———
		public Action CloseAction { get; set; }

		#region Validación (INotifyDataErrorInfo)
		public bool HasErrors => _errors.Any();
		public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
		public IEnumerable GetErrors(string propertyName)
			=> string.IsNullOrEmpty(propertyName)
			   ? _errors.SelectMany(kv => kv.Value)
			   : (_errors.TryGetValue(propertyName, out var list) ? list : Enumerable.Empty<string>());

		private void ValidateRange(string propMin, string propMax)
		{
			ClearErrors(propMin);
			ClearErrors(propMax);
			var min = (int)GetType().GetProperty(propMin)!.GetValue(this)!;
			var max = (int)GetType().GetProperty(propMax)!.GetValue(this)!;
			if (min > max)
			{
				AddError(propMin, $"{propMin} ({min}) no puede ser mayor que {propMax} ({max}).");
				AddError(propMax, $"{propMax} ({max}) no puede ser menor que {propMin} ({min}).");
			}
		}
		private void ValidateAllRanges()
		{
			ValidateRange(nameof(PasilloDesde), nameof(PasilloHasta));
			ValidateRange(nameof(EstanteriaDesde), nameof(EstanteriaHasta));
			ValidateRange(nameof(AlturaDesde), nameof(AlturaHasta));
			ValidateRange(nameof(PosicionDesde), nameof(PosicionHasta));
		}
		private void AddError(string prop, string msg)
		{
			if (!_errors.ContainsKey(prop)) _errors[prop] = new List<string>();
			if (!_errors[prop].Contains(msg))
			{
				_errors[prop].Add(msg);
				ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(prop));
			}
		}
		private void ClearErrors(string prop)
		{
			if (_errors.Remove(prop))
				ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(prop));
		}
		#endregion

		#region Lógica de Generación + Persistencia
		private async Task GenerarAsync()
		{
			UbicacionesGeneradas.Clear();
			var empresa = SessionManager.EmpresaSeleccionada!.Value;

			// 1) Genero todas las combinaciones en memoria
			var todas = new List<CrearUbicacionDetalladaDto>();
			for (int p = PasilloDesde; p <= PasilloHasta; p++)
				for (int e = EstanteriaDesde; e <= EstanteriaHasta; e++)
					for (int a = AlturaDesde; a <= AlturaHasta; a++)
						for (int o = PosicionDesde; o <= PosicionHasta; o++)
						{
							var codigo = $"UB{p:D3}{e:D3}{a:D3}{o:D3}";
							var descripcion = $"Pasillo {p}, Estantería {e}, Altura {a}, Posición {o}";
							var tipoDesc = TiposUbicacionDisponibles
												 .FirstOrDefault(t => t.TipoUbicacionId == TipoUbicacionId)
												 ?.Descripcion ?? "";

							todas.Add(new CrearUbicacionDetalladaDto
							{
								CodigoEmpresa = empresa,
								CodigoAlmacen = _codigoAlmacen,
								CodigoUbicacion = codigo,
								DescripcionUbicacion = descripcion,
								Pasillo = p,
								Estanteria = e,
								Altura = a,
								Posicion = o,
								Orden = 0,
								Peso = Peso,
								Alto = Alto,
								TemperaturaMin = TemperaturaMin,
								TemperaturaMax = TemperaturaMax,
								TipoPaletPermitido = TipoPaletPermitido,
								TipoUbicacionId = TipoUbicacionId,
								TipoUbicacionDescripcion = tipoDesc,
								AlergenosPermitidos = AlergenosDisponibles
															  .Where(x => x.IsSelected)
															  .Select(x => x.Codigo)
															  .ToList(),
								Excluir = false,
								ExistsInDb = false
							});
						}

			// 2) Consulto BD qué códigos ya existen
			var existentes = await _ubicacionService
				.ObtenerUbicacionesBasicoAsync(empresa, _codigoAlmacen);
			var setExistentes = new HashSet<string>(
				existentes.Select(x => x.Ubicacion),
				StringComparer.OrdinalIgnoreCase);

			// 3) Marco y añado al ObservableCollection
			foreach (var dto in todas)
			{
				dto.ExistsInDb = setExistentes.Contains(dto.CodigoUbicacion);
				UbicacionesGeneradas.Add(dto);
			}

			NotifyCommands();
		}

		private bool CanSave()
			=> UbicacionesGeneradas.Any(u => !u.Excluir && !u.ExistsInDb);

		private async Task GuardarAsync()
		{
			// Solo enviamos las nuevas (no excluidas y que no existan en BD)
			var lista = UbicacionesGeneradas
						  .Where(u => !u.Excluir && !u.ExistsInDb)
						  .ToList();
			if (!lista.Any()) { CloseAction?.Invoke(); return; }

			try
			{
				await _ubicacionService.CrearUbicacionesMasivoAsync(lista);
			}
			catch (Exception ex)
			{
				// TODO: mostrar mensaje de error
			}
			CloseAction?.Invoke();
		}

		private void NotifyCommands()
		{
			GenerarCommand.NotifyCanExecuteChanged();
			GuardarCommand.NotifyCanExecuteChanged();
		}
		#endregion

		/// <summary>
		/// Carga inicial de palets, tipos de ubicación y alérgenos.
		/// </summary>
		public async Task InitializeAsync()
		{
			//var palets = await _paletService.ObtenerTiposPaletAsync();
			//foreach (var p in palets) TiposPaletDisponibles.Add(p);

			//var tipos = await _ubicacionService.ObtenerTiposUbicacionAsync();
			//foreach (var t in tipos) TiposUbicacionDisponibles.Add(t);

			var maestros = await _ubicacionService.ObtenerAlergenosMaestrosAsync();
			foreach (var a in maestros) AlergenosDisponibles.Add(new AlergenoSeleccionable(a));
		}
	}
}
