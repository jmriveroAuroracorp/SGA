using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
	public partial class UbicacionDialogViewModel : ObservableObject
	{
		private readonly UbicacionesService _svc;
		private readonly bool _isNew;

		public string DialogTitle => _isNew ? "Nueva Ubicación" : "Editar Ubicación";
		public string SaveButtonText => _isNew ? "Crear" : "Actualizar";

		// Propiedades mapeadas al DTO de creación
		public short CodigoEmpresa { get; }
		public string CodigoAlmacen { get; }
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
		private string codigoUbicacion;

		[ObservableProperty]
		private string? descripcionUbicacion;
		[ObservableProperty]
		private int? pasillo;
		[ObservableProperty]
		private int? estanteria;
		[ObservableProperty]
		private int? altura;
		[ObservableProperty]
		private int? posicion;
		[ObservableProperty]
		private int? temperaturaMin;
		[ObservableProperty]
		private int? temperaturaMax;
		[ObservableProperty]
		private string? tipoPaletPermitido;
		[ObservableProperty]
		private bool? habilitada;
		[ObservableProperty]
		private short? tipoUbicacionId;

		public ObservableCollection<TipoUbicacionDto> TiposDisponibles { get; }
	= new ObservableCollection<TipoUbicacionDto>();
		public IRelayCommand SaveCommand { get; }
		public IRelayCommand CancelCommand { get; }

		public UbicacionDialogViewModel(
			UbicacionesService svc,
			short codigoEmpresa,
			string codigoAlmacen,
			UbicacionDetalladaDto? existing = null)
		{
			_svc = svc;
			_isNew = existing == null;

			// 1) Inicializa los comandos antes de nada
			SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
			CancelCommand = new RelayCommand(Cancel);

			// 2) Ahora establece los valores de DTO
			CodigoEmpresa = codigoEmpresa;
			CodigoAlmacen = codigoAlmacen;


			if (existing != null)
			{
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
			}
			else
			{
				// Valores por defecto para creación
				Habilitada = true;
				CodigoUbicacion = string.Empty;
			}

			_ = LoadTiposAsync();
		}

		private bool CanSave()
		{
			return !string.IsNullOrWhiteSpace(CodigoUbicacion);
		}

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
				TipoUbicacionId = TipoUbicacionId
			};

			if (_isNew)
				await _svc.CrearUbicacionDetalladaAsync(dto);
			else
				//await _svc.ActualizarUbicacionDetalladaAsync(dto);

			Application.Current.Windows
				.OfType<Window>()
				.SingleOrDefault(w => w.IsActive)
				?.Close();
		}

		private async Task LoadTiposAsync()
		{
			try
			{
				var lista = await _svc.ObtenerTiposUbicacionAsync();
				TiposDisponibles.Clear();
				foreach (var t in lista)
					TiposDisponibles.Add(t);
				// Si estamos editando, fuerza selección del existente
				if (!_isNew && TipoUbicacionId.HasValue)
					OnPropertyChanged(nameof(TipoUbicacionId));
			}
			catch (Exception ex)
			{
				// log o muestra mensaje
			}
		}
		private void Cancel()
		{
			Application.Current.Windows
				.OfType<Window>()
				.SingleOrDefault(w => w.IsActive)
				?.Close();
		}
	}
}