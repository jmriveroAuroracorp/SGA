using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Dialog;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using SGA_Desktop.Views;

namespace SGA_Desktop.ViewModels
{
    public partial class InventarioDialogViewModel : ObservableObject
    {
        private readonly InventarioDto? _inventarioExistente;
        private readonly StockDto? _stockSistema;

        public InventarioDialogViewModel()
        {
            // Constructor para diseño
            FechaInventario = DateTime.Now;
            UsuarioInventario = SessionManager.NombreOperario;
        }

        public InventarioDialogViewModel(StockDto stockSistema)
        {
            _stockSistema = stockSistema;
            
            // Inicializar desde stock del sistema
            CodigoArticulo = stockSistema.CodigoArticulo;
            DescripcionArticulo = stockSistema.DescripcionArticulo ?? string.Empty;
            Almacen = stockSistema.Almacen;
            Ubicacion = stockSistema.Ubicacion;
            Partida = stockSistema.Partida;
            StockSistema = stockSistema.UnidadSaldo;
            FechaCaducidad = stockSistema.FechaCaducidad;
            CodigoPalet = stockSistema.CodigoPalet;
            EstadoPalet = stockSistema.EstadoPalet;
            
            // Valores por defecto
            StockFisico = stockSistema.UnidadSaldo; // Inicialmente igual al sistema
            FechaInventario = DateTime.Now;
            UsuarioInventario = SessionManager.NombreOperario;
            Observaciones = string.Empty;
        }

        public InventarioDialogViewModel(InventarioDto inventarioExistente)
        {
            _inventarioExistente = inventarioExistente;
            
            // Inicializar desde inventario existente
            CodigoArticulo = inventarioExistente.CodigoArticulo;
            DescripcionArticulo = inventarioExistente.DescripcionArticulo ?? string.Empty;
            Almacen = inventarioExistente.Almacen;
            Ubicacion = inventarioExistente.Ubicacion;
            Partida = inventarioExistente.Partida;
            StockSistema = inventarioExistente.StockSistema;
            StockFisico = inventarioExistente.StockFisico;
            FechaCaducidad = inventarioExistente.FechaCaducidad;
            CodigoPalet = inventarioExistente.CodigoPalet;
            EstadoPalet = inventarioExistente.EstadoPalet;
            FechaInventario = inventarioExistente.FechaInventario;
            UsuarioInventario = inventarioExistente.UsuarioInventario;
            Observaciones = inventarioExistente.Observaciones ?? string.Empty;
        }

        #region Properties
        [ObservableProperty]
        private string codigoArticulo = string.Empty;

        [ObservableProperty]
        private string descripcionArticulo = string.Empty;

        [ObservableProperty]
        private string almacen = string.Empty;

        [ObservableProperty]
        private string ubicacion = string.Empty;

        [ObservableProperty]
        private string partida = string.Empty;

        [ObservableProperty]
        private decimal stockSistema;

        [ObservableProperty]
        private decimal stockFisico;

        [ObservableProperty]
        private DateTime? fechaCaducidad;

        [ObservableProperty]
        private string? codigoPalet;

        [ObservableProperty]
        private string? estadoPalet;

        [ObservableProperty]
        private DateTime fechaInventario;

        [ObservableProperty]
        private string usuarioInventario = string.Empty;

        [ObservableProperty]
        private string observaciones = string.Empty;

        [ObservableProperty]
        private string stockFisicoTexto = string.Empty;
        #endregion

        #region Computed Properties
        public decimal Diferencia => StockFisico - StockSistema;

        public string DiferenciaFormateada => Diferencia.ToString("N2");

        public string StockSistemaFormateado => StockSistema.ToString("N2");

        public string FechaCaducidadFormateada => FechaCaducidad?.ToString("dd/MM/yyyy") ?? "Sin fecha";

        public string FechaInventarioFormateada => FechaInventario.ToString("dd/MM/yyyy HH:mm");

        public Brush ColorDiferencia
        {
            get
            {
                if (Math.Abs(Diferencia) < 0.01m)
                    return Brushes.Black;
                return Diferencia > 0 ? Brushes.Green : Brushes.Red;
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private void Guardar()
        {
            if (ValidarDatos())
            {
                // Cerrar diálogo con resultado positivo
                if (Application.Current.MainWindow is Window mainWindow)
                {
                    var dialog = mainWindow.OwnedWindows.OfType<InventarioDialog>().FirstOrDefault();
                    dialog?.Close();
                }
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            // Cerrar diálogo con resultado negativo
            if (Application.Current.MainWindow is Window mainWindow)
            {
                var dialog = mainWindow.OwnedWindows.OfType<InventarioDialog>().FirstOrDefault();
                dialog?.Close();
            }
        }
        #endregion

        #region Property Change Handlers
        partial void OnStockFisicoTextoChanged(string value)
        {
            if (decimal.TryParse(value?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var stock))
            {
                StockFisico = stock;
                OnPropertyChanged(nameof(Diferencia));
                OnPropertyChanged(nameof(DiferenciaFormateada));
                OnPropertyChanged(nameof(ColorDiferencia));
            }
        }

        partial void OnStockFisicoChanged(decimal value)
        {
            StockFisicoTexto = value.ToString("N2");
            OnPropertyChanged(nameof(Diferencia));
            OnPropertyChanged(nameof(DiferenciaFormateada));
            OnPropertyChanged(nameof(ColorDiferencia));
        }
        #endregion

        #region Private Methods
        private bool ValidarDatos()
        {
            if (StockFisico < 0)
            {
                MessageBox.Show("El stock físico no puede ser negativo.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(Observaciones) && Math.Abs(Diferencia) > 0.01m)
            {
                var resultado = MessageBox.Show(
                    "Hay una diferencia entre el stock del sistema y el físico. ¿Desea añadir observaciones?",
                    "Confirmación",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (resultado == MessageBoxResult.Yes)
                {
                    return false; // Mantener el diálogo abierto para que añada observaciones
                }
            }

            return true;
        }
        #endregion
    }
} 