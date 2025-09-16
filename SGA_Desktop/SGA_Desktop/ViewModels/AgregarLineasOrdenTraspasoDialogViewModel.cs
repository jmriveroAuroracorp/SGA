using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Services;
using SGA_Desktop.Models;
using SGA_Desktop.Helpers;
using SGA_Desktop.Dialog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Collections.Generic;

namespace SGA_Desktop.ViewModels
{
    public partial class AgregarLineasOrdenTraspasoDialogViewModel : ObservableObject
    {
        private readonly StockService _stockService;

        public ObservableCollection<ArticuloConStockDto> ArticulosConStock { get; } = new();
        public ObservableCollection<LineaOrdenTraspasoItem> LineasPendientes { get; } = new();
        public ObservableCollection<AlmacenDto> AlmacenesCombo { get; } = new();

        [ObservableProperty]
        private string articuloBuscado = "";

        [ObservableProperty]
        private string articuloDescripcion = "";

        // Información del destino común (viene del encabezado de la orden)
        [ObservableProperty]
        private string codigoAlmacenDestino = "";

        [ObservableProperty]
        private string nombreAlmacenDestino = "";

        partial void OnArticuloBuscadoChanged(string value) => BuscarStockCommand.NotifyCanExecuteChanged();
        partial void OnArticuloDescripcionChanged(string value) => BuscarStockCommand.NotifyCanExecuteChanged();

        public AgregarLineasOrdenTraspasoDialogViewModel(string codigoAlmacenDestino, string nombreAlmacenDestino)
        {
            _stockService = new StockService();
            CodigoAlmacenDestino = codigoAlmacenDestino;
            NombreAlmacenDestino = nombreAlmacenDestino;
        }

        public async Task InitializeAsync()
        {
            // Cargar almacenes autorizados para el filtrado
            await CargarAlmacenesAutorizados();
        }

        private async Task CargarAlmacenesAutorizados()
        {
            try
            {
                var empresa = SessionManager.EmpresaSeleccionada!.Value;
                var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";
                var desdeLogin = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();

                var resultado = await _stockService.ObtenerAlmacenesAutorizadosAsync(empresa, centro, desdeLogin);

                AlmacenesCombo.Clear();
                foreach (var a in resultado)
                    AlmacenesCombo.Add(a);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando almacenes autorizados: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(CanBuscarStock))]
        private async Task BuscarStockAsync()
        {
            try
            {
                var codigo = string.IsNullOrWhiteSpace(ArticuloBuscado) ? null : ArticuloBuscado;
                var descripcion = string.IsNullOrWhiteSpace(ArticuloDescripcion) ? null : ArticuloDescripcion;

                if (codigo == null && descripcion == null)
                {
                    var warningDialog = new WarningDialog("Aviso", "Introduce al menos código de artículo o descripción para buscar.");
                    warningDialog.ShowDialog();
                    return;
                }

                // Usar el mismo método que ConsultaStockViewModel para obtener resultados filtrados por empresa
                var codigoEmpresa = SessionManager.EmpresaSeleccionada ?? 1;
                var resultados = await _stockService.ObtenerPorArticuloAsync(
                    codigoEmpresa, 
                    codigo, 
                    null, // partida
                    null, // codigoAlmacen
                    null, // codigoUbicacion
                    descripcion);

                // 🔷 NUEVA LÓGICA: Filtrar por permisos de almacén (igual que ConsultaStockViewModel)
                var almacenesAutorizados = ObtenerAlmacenesAutorizados();
                if (almacenesAutorizados.Any())
                {
                    resultados = resultados.Where(x => almacenesAutorizados.Contains(x.CodigoAlmacen)).ToList();
                }

                // Convertir StockDto a StockDisponibleDto y agrupar por artículo y empresa
                var stockDisponible = resultados.Select(s => new StockDisponibleDto
                {
                    CodigoArticulo = s.CodigoArticulo,
                    DescripcionArticulo = s.DescripcionArticulo,
                    CodigoEmpresa = (short)s.CodigoEmpresa,
                    CodigoAlmacen = s.CodigoAlmacen,
                    Ubicacion = s.Ubicacion,
                    Partida = s.Partida,
                    FechaCaducidad = s.FechaCaducidad,
                    UnidadSaldo = s.UnidadSaldo,
                    Reservado = 0, // StockDto no tiene Reservado, lo ponemos en 0
                    Disponible = s.UnidadSaldo, // En StockDto, Disponible = UnidadSaldo
                    CantidadAMoverTexto = "0"
                }).ToList();

                // Agrupar por artículo y empresa (sumando todo el stock de todos los almacenes, ubicaciones y lotes de la misma empresa)
                var gruposPorArticulo = stockDisponible
                    .GroupBy(s => new { s.CodigoArticulo, s.DescripcionArticulo, s.CodigoEmpresa })
                    .Select(g => new ArticuloConStockDto
                    {
                        CodigoArticulo = g.Key.CodigoArticulo,
                        DescripcionArticulo = g.Key.DescripcionArticulo,
                        Ubicaciones = new ObservableCollection<StockDisponibleDto>
                        {
                            new StockDisponibleDto
                            {
                                CodigoArticulo = g.Key.CodigoArticulo,
                                DescripcionArticulo = g.Key.DescripcionArticulo,
                                CodigoEmpresa = g.Key.CodigoEmpresa,
                                CodigoAlmacen = "TODOS LOS ALMACENES",
                                Ubicacion = "TODAS LAS UBICACIONES",
                                Partida = "VARIOS LOTES",
                                FechaCaducidad = null,
                                UnidadSaldo = g.Sum(x => x.UnidadSaldo),
                                Reservado = g.Sum(x => x.Reservado),
                                Disponible = g.Sum(x => x.Disponible),
                                CantidadAMoverTexto = "0"
                            }
                        }
                    })
                    .ToList();

                ArticulosConStock.Clear();
                foreach (var art in gruposPorArticulo)
                    ArticulosConStock.Add(art);
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al buscar stock: {ex.Message}");
                errorDialog.ShowDialog();
            }
        }

        private bool CanBuscarStock()
            => !string.IsNullOrWhiteSpace(ArticuloBuscado) || !string.IsNullOrWhiteSpace(ArticuloDescripcion);

        private List<string> ObtenerAlmacenesAutorizados()
        {
            var almacenesIndividuales = SessionManager.UsuarioActual?.codigosAlmacen ?? new List<string>();
            var centro = SessionManager.UsuarioActual?.codigoCentro ?? "0";

            // Si no hay almacenes individuales, usar solo los del centro
            if (!almacenesIndividuales.Any())
            {
                // En este caso, los almacenes del centro ya están en AlmacenesCombo
                return AlmacenesCombo
                    .Where(a => a.CodigoAlmacen != "Todas")
                    .Select(a => a.CodigoAlmacen)
                    .ToList();
            }

            // Si hay almacenes individuales, incluir también los del centro
            var almacenesDelCentro = AlmacenesCombo
                .Where(a => a.CodigoAlmacen != "Todas" && a.EsDelCentro)
                .Select(a => a.CodigoAlmacen)
                .ToList();

            // Combinar almacenes individuales + almacenes del centro
            return almacenesIndividuales
                .Concat(almacenesDelCentro)
                .Distinct()
                .ToList();
        }

        [RelayCommand]
        private async Task AgregarLineaAsync(StockDisponibleDto dto)
        {
            if (dto.CantidadAMoverDecimal is not decimal cantidad || cantidad <= 0)
            {
                var warningDialog = new WarningDialog("Aviso", "Indica una cantidad mayor que 0");
                warningDialog.ShowDialog();
                return;
            }

            if (cantidad > dto.Disponible)
            {
                var warningDialog = new WarningDialog("Aviso", $"La cantidad a mover ({cantidad}) es mayor que la disponible real ({dto.Disponible}).");
                warningDialog.ShowDialog();
                return;
            }

            // Para órdenes de traspaso, verificamos que no exista el mismo artículo de la misma empresa
            bool yaExiste = LineasPendientes.Any(x => 
                x.CodigoArticulo == dto.CodigoArticulo && 
                x.CodigoEmpresa == dto.CodigoEmpresa);

            if (yaExiste)
            {
                var warningDialog = new WarningDialog("Aviso", "Ya has añadido este artículo.");
                warningDialog.ShowDialog();
                return;
            }

            var nuevaLinea = new LineaOrdenTraspasoItem
            {
                // Campos requeridos según la tabla OrdenTraspasoLineas
                CodigoArticulo = dto.CodigoArticulo,
                DescripcionArticulo = dto.DescripcionArticulo,
                CantidadPlan = cantidad,
                UbicacionDestino = "", // Se puede configurar después o dejar vacío
                Estado = "PENDIENTE", // Estado por defecto
                IdOperarioAsignado = null, // Se asignará después
                
                // Campos adicionales
                CodigoEmpresa = dto.CodigoEmpresa, // Mantenemos el código de empresa
                CodigoAlmacenOrigen = "", // Vacío porque es stock agrupado de todos los almacenes
                UbicacionOrigen = "", // Vacío porque es stock agrupado de todas las ubicaciones
                Partida = "", // Vacío porque es stock agrupado de varios lotes
                FechaCaducidad = null, // Null porque no distinguimos por fecha
                CodigoAlmacenDestino = CodigoAlmacenDestino, // Viene del encabezado
            };

            LineasPendientes.Add(nuevaLinea);
        }

        [RelayCommand]
        private void EliminarLinea(LineaOrdenTraspasoItem dto)
        {
            if (LineasPendientes.Contains(dto))
                LineasPendientes.Remove(dto);
        }


        [RelayCommand]
        private void Confirmar()
        {
            if (!LineasPendientes.Any())
            {
                var warningDialog = new WarningDialog("Aviso", "No hay líneas para agregar.");
                warningDialog.ShowDialog();
                return;
            }

            // Cerrar el dialog con resultado exitoso
            var window = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);
            
            if (window != null)
            {
                window.DialogResult = true;
            }
        }

        [RelayCommand]
        private void Cancelar()
        {
            var window = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);
            
            if (window != null)
            {
                window.DialogResult = false;
            }
        }
    }

    // Clase para representar una línea de orden de traspaso
    public class LineaOrdenTraspasoItem
    {
        public string CodigoArticulo { get; set; } = "";
        public string DescripcionArticulo { get; set; } = "";
        public short CodigoEmpresa { get; set; }
        public decimal CantidadPlan { get; set; }
        public string CodigoAlmacenOrigen { get; set; } = "";
        public string UbicacionOrigen { get; set; } = "";
        public string Partida { get; set; } = "";
        public DateTime? FechaCaducidad { get; set; }
        public string CodigoAlmacenDestino { get; set; } = "";
        public string UbicacionDestino { get; set; } = "";
        public string Estado { get; set; } = "PENDIENTE"; // Estado por defecto
        public int? IdOperarioAsignado { get; set; } // Operario asignado
    }
}
