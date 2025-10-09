using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Data;

using System.Collections.Generic;
using System.Linq;
using SGA_Desktop.Dialog;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class TraspasoStockDialogViewModel : ObservableObject
    {
        // Propiedades para binding
        public string ArticuloDescripcion { get; set; }
        public string AlmacenOrigenNombre { get; set; }
        public decimal CantidadDisponible { get; set; }
        public ObservableCollection<AlmacenDto> AlmacenesDestino { get; set; }
        
        // Vista filtrable para almacenes destino
        public ICollectionView AlmacenesDestinoView { get; private set; }
        
        [ObservableProperty]
        private AlmacenDto almacenDestinoSeleccionado;
        
        [ObservableProperty]
        private string filtroAlmacenesDestino = "";
        
        [ObservableProperty]
        private bool isDropDownOpenAlmacenes = false;
        [ObservableProperty]
        private string cantidadATraspasarTexto;

        [ObservableProperty]
        private string comentariosTexto = "";

        //  NUEVAS PROPIEDADES para mostrar información del palet
        [ObservableProperty]
        private string codigoPalet;

        [ObservableProperty]
        private string estadoPalet;

        [ObservableProperty]
        private string tipoStock;

        public bool EsStockPaletizado => TipoStock == "Paletizado";
        public bool EsStockSuelto => TipoStock == "Suelto";
        public string InformacionPalet => EsStockPaletizado ? $"{CodigoPalet} ({EstadoPalet})" : "";

        // Métodos para notificar cambios en las propiedades computadas
        partial void OnTipoStockChanged(string value)
        {
            OnPropertyChanged(nameof(EsStockPaletizado));
            OnPropertyChanged(nameof(EsStockSuelto));
            OnPropertyChanged(nameof(InformacionPalet));
        }

        partial void OnCodigoPaletChanged(string value)
        {
            OnPropertyChanged(nameof(InformacionPalet));
        }

        partial void OnEstadoPaletChanged(string value)
        {
            OnPropertyChanged(nameof(InformacionPalet));
        }

        partial void OnCantidadATraspasarTextoChanged(string value)
        {
            ConfirmarCommand.NotifyCanExecuteChanged();
        }

    partial void OnAlmacenDestinoSeleccionadoChanged(AlmacenDto value)
    {
        ConfirmarCommand.NotifyCanExecuteChanged();
        _ = CargarUbicacionesDestinoAsync();
    }

    // NUEVO: Cuando cambia la ubicación destino, consultar palets disponibles
    partial void OnUbicacionDestinoSeleccionadaChanged(UbicacionDto value)
    {
        _ = ConsultarPaletsDisponiblesAsync();
    }

        [ObservableProperty]
        private ObservableCollection<UbicacionDto> ubicacionesDestino = new();

    [ObservableProperty]
    private UbicacionDto ubicacionDestinoSeleccionada;

    // NUEVO: Propiedades para selección de palets
    [ObservableProperty]
    private ObservableCollection<PaletDisponibleDto> paletsDisponibles = new();

    [ObservableProperty]
    private PaletDisponibleDto paletDestinoSeleccionado;

    [ObservableProperty]
    private bool mostrarSelectorPalets = false;

    private readonly UbicacionesService _ubicacionesService = new UbicacionesService();

    private async Task CargarUbicacionesDestinoAsync()
    {
        UbicacionesDestino.Clear();
        if (AlmacenDestinoSeleccionado == null) return;
        var lista = await _ubicacionesService.ObtenerUbicacionesAsync(
            AlmacenDestinoSeleccionado.CodigoAlmacen,
            SessionManager.EmpresaSeleccionada.Value
        );
        if (lista != null)
        {
            foreach (var u in lista)
                UbicacionesDestino.Add(u);
        }
    }

    // NUEVO: Consultar palets disponibles en la ubicación destino
    private async Task ConsultarPaletsDisponiblesAsync()
    {
        // Limpiar lista anterior
        PaletsDisponibles.Clear();
        PaletDestinoSeleccionado = null;
        MostrarSelectorPalets = false;

        // Validar que tengamos almacén y ubicación destino
        if (AlmacenDestinoSeleccionado == null || UbicacionDestinoSeleccionada == null)
            return;

        try
        {
            // Llamar al endpoint precheck
            var resultado = await _traspasoService.PrecheckFinalizarArticuloAsync(
                SessionManager.EmpresaSeleccionada.Value,
                AlmacenDestinoSeleccionado.CodigoAlmacen,
                UbicacionDestinoSeleccionada.Ubicacion
            );

            if (resultado != null && resultado.CantidadPalets > 0)
            {
                // Añadir palets a la lista
                foreach (var palet in resultado.Palets)
                {
                    PaletsDisponibles.Add(new PaletDisponibleDto
                    {
                        PaletId = palet.PaletId,
                        CodigoPalet = palet.CodigoPalet,
                        Estado = palet.Estado,
                        Cerrado = palet.Cerrado,
                        Descripcion = palet.Descripcion
                    });
                }

                if (resultado.CantidadPalets == 1)
                {
                    // Solo hay 1 palet → seleccionarlo automáticamente
                    PaletDestinoSeleccionado = PaletsDisponibles.First();
                    MostrarSelectorPalets = false; // No mostrar el selector
                }
                else
                {
                    // Hay múltiples palets → mostrar selector para que el usuario elija
                    MostrarSelectorPalets = true;
                    // No seleccionar ninguno por defecto para forzar la selección manual
                }
            }
        }
        catch (Exception ex)
        {
            // Si falla el precheck, no pasa nada, funcionará como antes (sin selector)
            // Opcionalmente podrías loguear o mostrar un mensaje
        }
    }

        public string CodigoArticulo { get; set; }
        public string UbicacionOrigen => _stockSeleccionado.Ubicacion;
        public decimal Reservado => _stockSeleccionado.Reservado;
        public decimal Disponible => _stockSeleccionado.Disponible;

        // Eliminar las propiedades y la inicialización manual de los comandos
        // Comandos generados automáticamente por [RelayCommand]

        // Eventos
        public event Action<bool> RequestClose;

        private readonly TraspasosService _traspasoService;
        private readonly StockDisponibleDto _stockSeleccionado;
        private DateTime? _fechaBusqueda = null;

        public TraspasoStockDialogViewModel(StockDisponibleDto stockSeleccionado, TraspasosService traspasoService, DateTime? fechaBusqueda)
        {
            _stockSeleccionado = stockSeleccionado;
            CodigoArticulo = stockSeleccionado.CodigoArticulo;
            ArticuloDescripcion = stockSeleccionado.DescripcionArticulo;
            AlmacenOrigenNombre = stockSeleccionado.CodigoAlmacen;
            CantidadDisponible = stockSeleccionado.Disponible;
            AlmacenesDestino = new ObservableCollection<AlmacenDto>();
            _traspasoService = traspasoService;
            _fechaBusqueda = fechaBusqueda;
            
            // 🔷 NUEVO: La vista filtrable se inicializará después de cargar los datos
            
            // 🔷 NUEVO: Cargar información del palet
            TipoStock = stockSeleccionado.TipoStock;
            CodigoPalet = stockSeleccionado.CodigoPalet ?? "";
            EstadoPalet = stockSeleccionado.EstadoPalet ?? "";
            
            // 🔷 NUEVO: Establecer la cantidad disponible como valor por defecto
            CantidadATraspasarTexto = stockSeleccionado.Disponible.ToString("F4");
            
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var permisos = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
                if (!permisos.Any())
                {
                    permisos = await new StockService().ObtenerAlmacenesAsync(centro);
                }
                var almacenes = await new StockService().ObtenerAlmacenesAutorizadosAsync(empresa, centro, permisos);
                AlmacenesDestino.Clear();
                foreach (var a in almacenes)
                    AlmacenesDestino.Add(a);
                OnPropertyChanged(nameof(AlmacenesDestino));
                
                // 🔷 NUEVO: Inicializar la vista filtrable después de cargar los datos
                AlmacenesDestinoView = CollectionViewSource.GetDefaultView(AlmacenesDestino);
                AlmacenesDestinoView.Filter = FiltraAlmacenesDestino;
                OnPropertyChanged(nameof(AlmacenesDestinoView));
            }
            catch (Exception ex)
            {
                // Puedes mostrar un mensaje de error si lo deseas
            }
        }

        // Método que se llama al pulsar Buscar
        public void RegistrarBusqueda()
        {
            _fechaBusqueda = DateTime.UtcNow;
        }

        [ObservableProperty]
        private string? feedback;

        [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
		private async Task ConfirmarAsync()
		{
			// Validación de cantidad
			if (!decimal.TryParse(CantidadATraspasarTexto.Replace(',', '.'),
				System.Globalization.NumberStyles.Any,
				System.Globalization.CultureInfo.InvariantCulture,
				out var cantidad) || cantidad <= 0)
			{
				Feedback = "Cantidad no válida.";
				return;
			}

			// Validación de almacén destino
			if (AlmacenDestinoSeleccionado == null)
			{
				Feedback = "Selecciona un almacén destino.";
				return;
			}

			var empresa = SessionManager.EmpresaSeleccionada.Value;

			// --- ORIGEN ---
			bool reabrirOrigen = false; // ← se enviará al DTO si el usuario acepta
			var ubicacionOrigen = _stockSeleccionado.Ubicacion ?? "";
			var estadoOrigen = await _traspasoService.ConsultarEstadoPaletOrigenAsync(
				empresa,
				_stockSeleccionado.CodigoAlmacen,
				ubicacionOrigen
			);

			if (string.Equals(estadoOrigen, "Cerrado", StringComparison.OrdinalIgnoreCase))
			{
				var confirmOrigen = new ConfirmationDialog(
					"Palet de origen cerrado",
					"El palet de ORIGEN está CERRADO. ¿Deseas reabrirlo para poder extraer stock?"
				);

				// 🔷 MEJORADO: Centrar el diálogo
				var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
						 ?? Application.Current.MainWindow;
				if (owner != null && owner != confirmOrigen)
					confirmOrigen.Owner = owner;

				if (confirmOrigen.ShowDialog() == true)
				{
					reabrirOrigen = true; // ← que lo reabra el backend
				}
				else
				{
					Feedback = "Operación cancelada: palet de origen cerrado.";
					return;
				}
			}

			// --- DESTINO ---
			var ubicacionDestino = UbicacionDestinoSeleccionada?.Ubicacion ?? "";
			if (!string.IsNullOrWhiteSpace(ubicacionDestino))
			{
				var estadoDestino = await _traspasoService.ConsultarEstadoPaletDestinoAsync(
					empresa,
					AlmacenDestinoSeleccionado.CodigoAlmacen,
					ubicacionDestino
				);

				if (string.Equals(estadoDestino, "Cerrado", StringComparison.OrdinalIgnoreCase))
				{
					var confirmDestino = new ConfirmationDialog(
						"Palet destino cerrado",
						"Hay un palet CERRADO en la ubicación destino. ¿Deseas reabrirlo y continuar con el traspaso?"
					);

					if (confirmDestino.ShowDialog() != true)
					{
						Feedback = "Operación cancelada: palet destino cerrado.";
						return;
					}
					// No llamamos a nada: el backend ya reabre destino automáticamente.
				}
				else if (string.Equals(estadoDestino, "Abierto", StringComparison.OrdinalIgnoreCase))
				{
					var infoDestino = new ConfirmationDialog(
						"Palet destino abierto",
						"Hay un palet ABIERTO en la ubicación destino. El artículo se agregará a ese palet. ¿Deseas continuar?"
					);

					if (infoDestino.ShowDialog() != true)
					{
						Feedback = "Operación cancelada por el usuario.";
						return;
					}
				}
			}

		// --- Construir DTO y llamar a API ---
		var dto = new CrearTraspasoArticuloDto
		{
			AlmacenOrigen = _stockSeleccionado.CodigoAlmacen,
			UbicacionOrigen = _stockSeleccionado.Ubicacion ?? string.Empty,
			CodigoArticulo = _stockSeleccionado.CodigoArticulo,
			Cantidad = cantidad,
			UsuarioId = SessionManager.UsuarioActual?.operario ?? 0,
			AlmacenDestino = AlmacenDestinoSeleccionado.CodigoAlmacen,
			UbicacionDestino = string.IsNullOrWhiteSpace(ubicacionDestino) ? "" : ubicacionDestino,
			FechaCaducidad = _stockSeleccionado.FechaCaducidad,
			Partida = _stockSeleccionado.Partida,
			Finalizar = true,
			CodigoEmpresa = empresa,
			FechaInicio = _fechaBusqueda,
			DescripcionArticulo = _stockSeleccionado.DescripcionArticulo,
			UnidadMedida = null,
			Comentario = comentariosTexto, // Usar comentarios del usuario

			// 🔹 nuevo flag para que el backend reabra el palet de ORIGEN si estaba cerrado
			ReabrirSiCerradoOrigen = reabrirOrigen,

			// 🔹 NUEVO: Enviar el palet destino seleccionado manualmente (si existe)
			PaletIdDestino = PaletDestinoSeleccionado?.PaletId
		};

			var resultado = await _traspasoService.CrearTraspasoArticuloAsync(dto);

			if (resultado.Success)
			{
				Feedback = resultado.PaletInfo ?? "Traspaso realizado correctamente.";
				RequestClose?.Invoke(true);
			}
			else
			{
				// 🔷 CORREGIDO: Usar ShowCenteredDialog en lugar de ShowDialog directo
				var errorDialog = new WarningDialog("Error al traspasar", resultado.ErrorMessage ?? "Error al realizar el traspaso.");
				ShowCenteredDialog(errorDialog);
				Feedback = resultado.ErrorMessage ?? "Error al realizar el traspaso.";
			}
		}



		private bool PuedeConfirmar()
        {
            if (string.IsNullOrWhiteSpace(CantidadATraspasarTexto) || AlmacenDestinoSeleccionado == null)
                return false;

            // Permitir tanto coma como punto
            var texto = CantidadATraspasarTexto.Replace(',', '.');
            if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cantidad))
                return false;

            return cantidad > 0 && cantidad <= CantidadDisponible;
        }

        [RelayCommand]
        private void Cancelar()
        {
            RequestClose?.Invoke(false);
        }

        // NUEVO: Método para centrar diálogos (como en ControlesRotativosViewModel)
        private void ShowCenteredDialog(WarningDialog dialog)
        {
            // Configurar el owner para centrar el diálogo
            var mainWindow = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            
            if (mainWindow != null && mainWindow != dialog)
            {
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                // Si no hay ventana principal, centrar en pantalla
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
                
            dialog.ShowDialog();
        }


        // Métodos para filtrado de almacenes destino
        private bool FiltraAlmacenesDestino(object obj)
        {
            if (obj is not AlmacenDto almacen) return false;
            if (string.IsNullOrEmpty(FiltroAlmacenesDestino)) return true;
            
            return System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(almacen.DescripcionCombo, FiltroAlmacenesDestino, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;
        }
        
        // Método para manejar cambios en el filtro
        partial void OnFiltroAlmacenesDestinoChanged(string value)
        {
            AlmacenesDestinoView?.Refresh();
        }
        
        // Comandos para controlar dropdown
        [RelayCommand]
        private void AbrirDropDownAlmacenes()
        {
            // Limpiar el filtro para permitir escribir desde cero
            FiltroAlmacenesDestino = "";
            IsDropDownOpenAlmacenes = true;
        }
        
        [RelayCommand]
        private void CerrarDropDownAlmacenes()
        {
            IsDropDownOpenAlmacenes = false;
        }

        // No es necesario implementar PropertyChanged, lo gestiona ObservableObject
    }
} 