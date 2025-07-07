using System;
using System.Linq;
using System.Windows;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models; // Aquí debe estar EstadoPaletDto y TipoPaletDto
using SGA_Desktop.Services;       // Para PaletService
using SGA_Desktop.Dialog;         // Para PaletFilterDialog

namespace SGA_Desktop.ViewModels
{
	public partial class PaletFilterDialogViewModel : ObservableObject
	{
		private readonly PaletService _paletService;

		// ▶️ Colecciones bindables
		public ObservableCollection<EstadoPaletDto> EstadosDisponibles { get; }
			= new ObservableCollection<EstadoPaletDto>();
		[ObservableProperty] private EstadoPaletDto? _estadoSeleccionado;

		public ObservableCollection<TipoPaletDto> TiposPaletDisponibles { get; }
			= new ObservableCollection<TipoPaletDto>();
		[ObservableProperty] private TipoPaletDto? _tipoPaletSeleccionado;
		public ObservableCollection<UsuarioConNombre> UsuariosDisponibles { get; }
			= new ObservableCollection<UsuarioConNombre>();
		[ObservableProperty] private UsuarioConNombre? _usuarioAperturaSeleccionado;
		[ObservableProperty] private UsuarioConNombre? _usuarioCierreSeleccionado;

		// ▶️ Otros filtros
		[ObservableProperty] private string? _codigo;
		[ObservableProperty] private DateTime? _fechaApertura;
		[ObservableProperty] private DateTime? _fechaCierre;
		[ObservableProperty] private DateTime? _fechaDesde;
		[ObservableProperty] private DateTime? _fechaHasta;
		[ObservableProperty] private int? _usuarioApertura;
		[ObservableProperty] private int? _usuarioCierre;

		// ▶️ Comando para “Aplicar”
		public IRelayCommand AplicarFiltrosCommand { get; }

		public PaletFilterDialogViewModel(PaletService paletService)
		{
			_paletService = paletService;

			AplicarFiltrosCommand = new RelayCommand(() =>
			{
				var dlg = Application.Current.Windows
					.OfType<PaletFilterDialog>()
					.FirstOrDefault();
				if (dlg != null)
					dlg.DialogResult = true;
			});
		}

		/// <summary>
		/// Llamar en Loaded del Window para no bloquear la UI.
		/// </summary>
		public async Task InitializeAsync()
		{
			// 1) Carga Estados desde API
			var estados = await _paletService.ObtenerEstadosAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				EstadosDisponibles.Clear();
				// Inserta primero un “sin filtro”
				EstadosDisponibles.Add(new EstadoPaletDto
				{
					CodigoEstado = null!,
					Descripcion = "-- Todos los estados --",
					Orden = 0
				});
				foreach (var e in estados)
					EstadosDisponibles.Add(e);

				// Al no forzar SelectedItem, queda en el “sin filtro”
				EstadoSeleccionado = EstadosDisponibles[0];
			});

			// 2) Carga TiposPalet desde API
			var tipos = await _paletService.ObtenerTiposPaletAsync();
			await Application.Current.Dispatcher.InvokeAsync(() =>
			{
				TiposPaletDisponibles.Clear();
				// Inserta “sin filtro”
				TiposPaletDisponibles.Add(new TipoPaletDto
				{
					CodigoPalet = null!,
					Descripcion = "-- Todos los tipos --"
				});
				foreach (var t in tipos)
					TiposPaletDisponibles.Add(t);

				TipoPaletSeleccionado = TiposPaletDisponibles[0];
			});
		}

		public void ActualizarUsuariosDisponibles(IEnumerable<PaletDto> palets)
		{
			UsuariosDisponibles.Clear();

			System.Diagnostics.Debug.WriteLine($"🔷 Palets recibidos: {palets.Count()}");

			foreach (var p in palets)
			{
				System.Diagnostics.Debug.WriteLine(
					$"Palet: {p.Codigo} | UsuarioAperturaId: {p.UsuarioAperturaId} | UsuarioAperturaNombre: {p.UsuarioAperturaNombre}");
			}

			// Añade la opción "Todos"
			UsuariosDisponibles.Add(new UsuarioConNombre
			{
				UsuarioId = 0,
				NombreOperario = "-- Todos los usuarios --"
			});

			var usuariosUnicos = palets
				.Where(p => p.UsuarioAperturaId.HasValue)
				.GroupBy(p => p.UsuarioAperturaId.Value)
				.Select(g => new UsuarioConNombre
				{
					UsuarioId = g.Key,
					NombreOperario = g.First().UsuarioAperturaNombre ?? ""
				})
				.OrderBy(u => u.NombreOperario)
				.ToList();

			System.Diagnostics.Debug.WriteLine($"🔷 Usuarios únicos encontrados: {usuariosUnicos.Count}");

			foreach (var u in usuariosUnicos)
			{
				System.Diagnostics.Debug.WriteLine($"➡️ UsuarioId: {u.UsuarioId} | Nombre: {u.NombreOperario}");
				UsuariosDisponibles.Add(u);
			}

			System.Diagnostics.Debug.WriteLine($"🔷 UsuariosDisponibles final: {UsuariosDisponibles.Count}");

			UsuarioAperturaSeleccionado = UsuariosDisponibles.FirstOrDefault();
			UsuarioCierreSeleccionado = UsuariosDisponibles.FirstOrDefault();
		}


	}
}
