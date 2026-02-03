using SGA_Desktop.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SGA_Desktop.Models
{
	/// <summary>
	/// DTO para recibir los resultados de la consulta de stock.
	/// </summary>
	public class StockDto : INotifyPropertyChanged
	{
		[JsonPropertyName("codigoEmpresa")]
		public int CodigoEmpresa { get; set; }

		[JsonPropertyName("codigoArticulo")]
		public string CodigoArticulo { get; set; } = string.Empty;

		// Nuevo: código de almacén para filtrar
		[JsonPropertyName("codigoAlmacen")]
		public string CodigoAlmacen { get; set; } = string.Empty;

		[JsonPropertyName("almacen")]
		public string Almacen { get; set; } = string.Empty;

		[JsonPropertyName("ubicacion")]
		public string Ubicacion { get; set; } = string.Empty;

		[JsonPropertyName("partida")]
		public string Partida { get; set; } = string.Empty;

		[JsonPropertyName("fechaCaducidad")]
		public DateTime? FechaCaducidad { get; set; }

		[JsonPropertyName("unidadSaldo")]
		public decimal UnidadSaldo { get; set; }

		[JsonPropertyName("descripcionArticulo")]
		public string? DescripcionArticulo { get; set; }

		[JsonPropertyName("codigoAlternativo")]
		public string CodigoAlternativo { get; set; } = string.Empty;

		// JSON debe venir como "alergenos" o "vNEWAlergenos" según tu API
		[JsonPropertyName("alergenos")]
		public string Alergenos { get; set; } = string.Empty;

		// 👇 Nuevo campo
		[JsonPropertyName("codigoPalet")]
		public string? CodigoPalet { get; set; }

		// 👇 Propiedad calculada si quieres comodidad en el cliente
		[JsonIgnore]
		public bool EstaPaletizado => !string.IsNullOrEmpty(CodigoPalet);

		[JsonPropertyName("estadoPalet")]
		public string? EstadoPalet { get; set; }

		public List<PaletDetalleDto> Palets { get; set; } = new();

		public event PropertyChangedEventHandler? PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

		private decimal? _totalArticuloGlobal;
		public decimal? TotalArticuloGlobal
		{
			get => _totalArticuloGlobal;
			set { _totalArticuloGlobal = value; OnPropertyChanged(); }
		}

		private decimal? _totalArticuloAlmacen;
		public decimal? TotalArticuloAlmacen
		{
			get => _totalArticuloAlmacen;
			set { _totalArticuloAlmacen = value; OnPropertyChanged(); }
		}

		// 🔷 NUEVO: Total paletizado en ubicación (suma de cantidades de todos los palets en esta ubicación)
		[JsonIgnore]
		public decimal TotalPaletizadoUbicacion => Palets?.Sum(p => p.Cantidad) ?? 0;

		// 🔷 NUEVO: Indicador de bloqueo por calidad
		[JsonPropertyName("isBloqueadoCalidad")]
		public bool IsBloqueadoCalidad { get; set; }

		[JsonPropertyName("motivoBloqueoCalidad")]
		public string? MotivoBloqueoCalidad { get; set; }

		[JsonPropertyName("fechaBloqueoCalidad")]
		public DateTime? FechaBloqueoCalidad { get; set; }

		[JsonPropertyName("tipoBloqueoCalidad")]
		public string? TipoBloqueoCalidad { get; set; }

		// 🔷 NUEVO: Fecha del último traspaso
		[JsonPropertyName("fechaUltimoTraspaso")]
		public DateTime? FechaUltimoTraspaso { get; set; }

		// 🔷 NUEVO: Información de desincronización de stock (SAGE vs StorageControl)
		[JsonPropertyName("tieneDesincronizacion")]
		public bool? TieneDesincronizacion { get; set; }
		
		[JsonPropertyName("stockSage")]
		public decimal? StockSage { get; set; }
		
		[JsonPropertyName("stockStorageControl")]
		public decimal? StockStorageControl { get; set; }

	}
}
