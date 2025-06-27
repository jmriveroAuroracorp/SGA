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


		/// <summary>Lo que se mostrará en el título</summary>
		public string TituloDisplay => $"{_descripcionAlmacen}";

		public UbicacionMasivoDialogViewModel(
		AlmacenDto almacen,
		UbicacionesService ubicService,
		PaletService paletService)
		{
			_codigoAlmacen = almacen.CodigoAlmacen;
			_ubicacionService = ubicService ?? throw new ArgumentNullException(nameof(ubicService));
			_paletService = paletService ?? throw new ArgumentNullException(nameof(paletService));

			// Inicializo colecciones BINDABLES
			TiposPaletDisponibles = new ObservableCollection<TipoPaletDto>();

			TiposUbicacionDisponibles = new ObservableCollection<TipoUbicacionDto>();

			// Resto de setup (rangos, comandos…)
			UbicacionesGeneradas = new ObservableCollection<CrearUbicacionDetalladaDto>();
			CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
			GenerarCommand = new RelayCommand(Generar, CanGenerate);
			GuardarCommand = new AsyncRelayCommand(GuardarAsync, CanSave);

			ValidateAllRanges();
		}



		// ——— Propiedades de rango ———
		[ObservableProperty]
		private int pasilloDesde;

		partial void OnPasilloDesdeChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(PasilloDesde), nameof(PasilloHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int pasilloHasta;

		partial void OnPasilloHastaChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(PasilloDesde), nameof(PasilloHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int estanteriaDesde;

		partial void OnEstanteriaDesdeChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(EstanteriaDesde), nameof(EstanteriaHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int estanteriaHasta;

		partial void OnEstanteriaHastaChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(EstanteriaDesde), nameof(EstanteriaHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int alturaDesde;

		partial void OnAlturaDesdeChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(AlturaDesde), nameof(AlturaHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int alturaHasta;

		partial void OnAlturaHastaChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(AlturaDesde), nameof(AlturaHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int posicionDesde;

		partial void OnPosicionDesdeChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(PosicionDesde), nameof(PosicionHasta));
			NotifyCommands();
		}

		[ObservableProperty]
		private int posicionHasta;

		partial void OnPosicionHastaChanged(int oldValue, int newValue)
		{
			ValidateRange(nameof(PosicionDesde), nameof(PosicionHasta));
			NotifyCommands();
		}

		// Props para binding
		[ObservableProperty] private int? temperaturaMin;
		[ObservableProperty] private int? temperaturaMax;
		[ObservableProperty] private string? tipoPaletPermitido;
		[ObservableProperty] private short? tipoUbicacionId;
		[ObservableProperty] private decimal? peso;
		[ObservableProperty] private decimal? alto;
		// Colecciones de selección
		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }
		public ObservableCollection<TipoUbicacionDto> TiposUbicacionDisponibles { get; }
		public ObservableCollection<AlergenoSeleccionable> AlergenosDisponibles { get; }
  = new ObservableCollection<AlergenoSeleccionable>();


		// ——— Colección para el grid de preview ———
		public ObservableCollection<CrearUbicacionDetalladaDto> UbicacionesGeneradas { get; }

		// ——— Comandos ———
		public IRelayCommand CancelCommand { get; }
		public IRelayCommand GenerarCommand { get; }
		public IAsyncRelayCommand GuardarCommand { get; }

		// ——— Para cerrar la ventana ———
		public Action CloseAction { get; set; }

		#region Validación (INotifyDataErrorInfo)
		public bool HasErrors => _errors.Any();
		public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
		public IEnumerable GetErrors(string propertyName)
			=> string.IsNullOrEmpty(propertyName)
			   ? _errors.SelectMany(kv => kv.Value)
			   : (_errors.ContainsKey(propertyName)
					? _errors[propertyName]
					: Enumerable.Empty<string>());

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
			if (!_errors.ContainsKey(prop))
				_errors[prop] = new List<string>();
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

		#region Lógica de comandos
		private void Generar()
		{
			UbicacionesGeneradas.Clear();
			var empresa = SessionManager.EmpresaSeleccionada!.Value;
	
			for (int p = PasilloDesde; p <= PasilloHasta; p++)
				for (int e = EstanteriaDesde; e <= EstanteriaHasta; e++)
					for (int a = AlturaDesde; a <= AlturaHasta; a++)
						for (int o = PosicionDesde; o <= PosicionHasta; o++)
						{
							var descripcion = $"Pasillo {p}, Estantería {e}, Altura {a}, Posición {o}";
							var tipoDesc = TiposUbicacionDisponibles
				 .FirstOrDefault(t => t.TipoUbicacionId == TipoUbicacionId)
				 ?.Descripcion
			   ?? "";
							UbicacionesGeneradas.Add(new CrearUbicacionDetalladaDto
							{

								CodigoEmpresa = empresa,
								CodigoAlmacen = _codigoAlmacen,
								CodigoUbicacion = $"UB{p:D3}{e:D3}{a:D3}{o:D3}",
								DescripcionUbicacion = descripcion,
								Pasillo = p,
								Orden = 0,
								Estanteria = e,
								Altura = a,
								Posicion = o,
								Excluir = false,
								IsDuplicate = false,
								TemperaturaMin = TemperaturaMin,
								TemperaturaMax = TemperaturaMax,
								TipoUbicacionId = TipoUbicacionId,
								TipoUbicacionDescripcion = tipoDesc,
								TipoPaletPermitido = TipoPaletPermitido,
								Peso = this.Peso,
								Alto = this.Alto,
								AlergenosPermitidos = AlergenosDisponibles
								.Where(x => x.IsSelected)
								.Select(x => x.Codigo)
								.ToList(),
							});
						}
			NotifyCommands();
		}

		private bool CanGenerate() => !HasErrors;
		private bool CanSave() => UbicacionesGeneradas.Any(u => !u.Excluir);

		private async Task GuardarAsync()
		{
			var lista = UbicacionesGeneradas
						  .Where(u => !u.Excluir)
						  .ToList();
			if (!lista.Any()) { CloseAction?.Invoke(); return; }

			try
			{
				var ok = await _ubicacionService.CrearUbicacionesMasivoAsync(lista);
				// notifica al usuario…
			}
			catch (Exception ex)
			{
				// manejar error…
			}
			CloseAction?.Invoke();
		}

		private void NotifyCommands()
		{
			GenerarCommand.NotifyCanExecuteChanged();
			GuardarCommand.NotifyCanExecuteChanged();
		}
		#endregion

		public async Task InitializeAsync()
		{
			// Cargo palets sin bloquear la UI
			var palets = await _paletService.ObtenerTiposPaletAsync();
			foreach (var p in palets)
				TiposPaletDisponibles.Add(p);

			// Cargo tipos de ubicación sin bloquear la UI
			var tipos = await _ubicacionService.ObtenerTiposUbicacionAsync();
			foreach (var t in tipos)
				TiposUbicacionDisponibles.Add(t);

			var maestros = await _ubicacionService.ObtenerAlergenosMaestrosAsync();
			AlergenosDisponibles.Clear();
			foreach (var a in maestros)
				AlergenosDisponibles.Add(new AlergenoSeleccionable(a));
		}

	}
}
