using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using ClosedXML.Excel;

namespace SGA_Desktop.ViewModels
{
    public partial class VerInventarioDialogViewModel : ObservableObject
    {
        #region Fields & Services
        private readonly InventarioService _inventarioService;
        #endregion

        #region Constructor
        public VerInventarioDialogViewModel(InventarioService inventarioService)
        {
            _inventarioService = inventarioService;
            
            LineasInventario = new ObservableCollection<LineaInventarioDto>();

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public VerInventarioDialogViewModel() : this(new InventarioService()) { }
        #endregion

        #region Observable Properties
        [ObservableProperty]
        private InventarioCabeceraDto? inventario;

        public ObservableCollection<Models.LineaInventarioDto> LineasInventario { get; }

        [ObservableProperty]
        private bool isCargando = false;

        [ObservableProperty]
        private string mensajeEstado = string.Empty;
        #endregion

        #region Computed Properties
        public string TotalLineas => $"Total: {LineasInventario.Count} líneas";
        #endregion

        #region Property Change Callbacks
        partial void OnInventarioChanged(InventarioCabeceraDto? oldValue, InventarioCabeceraDto? newValue)
        {
            if (newValue != null)
            {
                _ = CargarLineasInventarioAsync();
            }
        }
        #endregion

        #region Commands
        [RelayCommand]
        private async Task InitializeAsync()
        {
            try
            {
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al inicializar: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void Cerrar()
        {
            CerrarDialogo();
        }

        [RelayCommand]
        private async Task ExportarAExcelAsync()
        {
            try
            {
                if (!LineasInventario.Any())
                {
                    var warningDialog = new WarningDialog("Aviso", "No hay datos para exportar.");
                    var ownerWarning = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                   ?? Application.Current.MainWindow;
                    if (ownerWarning != null && ownerWarning != warningDialog)
                        warningDialog.Owner = ownerWarning;
                    warningDialog.ShowDialog();
                    return;
                }

                // Diálogo para elegir ubicación del archivo
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Archivos de Excel (*.xlsx)|*.xlsx",
                    FileName = $"Inventario_{Inventario?.CodigoInventario ?? "SinNombre"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    Title = "Guardar inventario como Excel"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                // Crear el archivo Excel
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Inventario");

                // Configurar encabezados
                var headers = new[]
                {
                    "Código Artículo",
                    "Descripción",
                    "Ubicación",
                    "Partida",
                    "Fecha Caducidad",
                    "Stock Actual",
                    "Stock Teórico",
                    "Stock Contado",
                    "Ajuste Final",
                    "Palets"
                };

                // Escribir encabezados
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Escribir datos
                int row = 2;
                foreach (var linea in LineasInventario)
                {
                    worksheet.Cell(row, 1).Value = linea.CodigoArticulo;
                    worksheet.Cell(row, 2).Value = linea.DescripcionArticulo;
                    worksheet.Cell(row, 3).Value = linea.CodigoUbicacion;
                    worksheet.Cell(row, 4).Value = linea.Partida;
                    worksheet.Cell(row, 5).Value = linea.FechaCaducidad?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cell(row, 6).Value = linea.StockActual;
                    worksheet.Cell(row, 7).Value = linea.StockTeorico;
                    worksheet.Cell(row, 8).Value = linea.StockContado;
                    worksheet.Cell(row, 9).Value = linea.AjusteFinal ?? 0;
                    worksheet.Cell(row, 10).Value = linea.PaletsResumen;

                    // Aplicar formato a las celdas numéricas
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.0000";
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.0000";
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.0000";
                    worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.0000";

                    // Aplicar bordes
                    for (int col = 1; col <= headers.Length; col++)
                    {
                        worksheet.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    row++;
                }

                // Autoajustar columnas
                worksheet.Columns().AdjustToContents();

                // Agregar nota explicativa sobre el Ajuste Final
                var notaRow = row + 2;
                worksheet.Cell(notaRow, 1).Value = "NOTA: El Ajuste Final se calcula como: Stock Contado - Stock Teórico";
                worksheet.Cell(notaRow, 1).Style.Font.Italic = true;
                worksheet.Cell(notaRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(notaRow, 1, notaRow, headers.Length).Merge();

                // Agregar explicación de cada columna
                var explicacionRow = notaRow + 1;
                worksheet.Cell(explicacionRow, 1).Value = "EXPLICACIÓN DE COLUMNAS:";
                worksheet.Cell(explicacionRow, 1).Style.Font.Bold = true;
                worksheet.Cell(explicacionRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
                worksheet.Range(explicacionRow, 1, explicacionRow, headers.Length).Merge();

                var stockActualRow = explicacionRow + 1;
                worksheet.Cell(stockActualRow, 1).Value = "• Stock Actual: Stock que había cuando se creó el inventario";
                worksheet.Cell(stockActualRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(stockActualRow, 1, stockActualRow, headers.Length).Merge();

                var stockTeoricoRow = stockActualRow + 1;
                worksheet.Cell(stockTeoricoRow, 1).Value = "• Stock Teórico: Stock real del sistema al consolidar (detecta cambios durante el inventario)";
                worksheet.Cell(stockTeoricoRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(stockTeoricoRow, 1, stockTeoricoRow, headers.Length).Merge();

                var stockContadoRow = stockTeoricoRow + 1;
                worksheet.Cell(stockContadoRow, 1).Value = "• Stock Contado: Cantidad contada físicamente";
                worksheet.Cell(stockContadoRow, 1).Style.Font.FontColor = XLColor.Gray;
                worksheet.Range(stockContadoRow, 1, stockContadoRow, headers.Length).Merge();

                // Agregar información del inventario en una hoja separada
                if (Inventario != null)
                {
                    var infoWorksheet = workbook.Worksheets.Add("Información Inventario");
                    
                    infoWorksheet.Cell("A1").Value = "INFORMACIÓN DEL INVENTARIO";
                    infoWorksheet.Cell("A1").Style.Font.Bold = true;
                    infoWorksheet.Cell("A1").Style.Font.FontSize = 14;
                    
                    infoWorksheet.Cell("A3").Value = "ID Inventario:";
                    infoWorksheet.Cell("B3").Value = Inventario.CodigoInventario;
                    
                    infoWorksheet.Cell("A4").Value = "Almacén:";
                    infoWorksheet.Cell("B4").Value = Inventario.CodigoAlmacen;
                    
                    infoWorksheet.Cell("A5").Value = "Tipo:";
                    infoWorksheet.Cell("B5").Value = Inventario.TipoInventario;
                    
                    infoWorksheet.Cell("A6").Value = "Estado:";
                    infoWorksheet.Cell("B6").Value = Inventario.Estado;
                    
                    infoWorksheet.Cell("A7").Value = "Fecha Creación:";
                    infoWorksheet.Cell("B7").Value = Inventario.FechaCreacion.ToString("dd/MM/yyyy HH:mm");
                    
                    infoWorksheet.Cell("A8").Value = "Usuario:";
                    infoWorksheet.Cell("B8").Value = Inventario.UsuarioCreacionNombre ?? Inventario.UsuarioCreacionId.ToString();
                    
                    infoWorksheet.Cell("A10").Value = "Fecha Exportación:";
                    infoWorksheet.Cell("B10").Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                    
                    infoWorksheet.Cell("A12").Value = "Total Líneas:";
                    infoWorksheet.Cell("B12").Value = LineasInventario.Count;

                    // Aplicar formato
                    for (int i = 1; i <= 12; i++)
                    {
                        infoWorksheet.Cell($"A{i}").Style.Font.Bold = true;
                        infoWorksheet.Cell($"A{i}").Style.Fill.BackgroundColor = XLColor.LightGray;
                    }
                    
                    infoWorksheet.Columns().AdjustToContents();
                }

                // Guardar archivo
                workbook.SaveAs(saveFileDialog.FileName);

                var successDialog = new WarningDialog("Exportación Completada", $"Inventario exportado correctamente a:\n{saveFileDialog.FileName}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != successDialog)
                    successDialog.Owner = owner;
                successDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                var errorDialog = new WarningDialog("Error", $"Error al exportar a Excel: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
        }
        #endregion

        #region Private Methods
        private async Task CargarLineasInventarioAsync()
        {
            if (Inventario == null) return;

            try
            {
                IsCargando = true;
                MensajeEstado = "Cargando líneas del inventario...";

                var lineas = await _inventarioService.ObtenerLineasInventarioAsync(Inventario.IdInventario);

                LineasInventario.Clear();
                foreach (var linea in lineas)
                {
                    LineasInventario.Add(linea);
                }

                // Notificar cambios en las propiedades computadas
                OnPropertyChanged(nameof(TotalLineas));

                MensajeEstado = $"Cargadas {LineasInventario.Count} líneas";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                var errorDialog = new WarningDialog("Error", $"Error al cargar líneas del inventario: {ex.Message}");
                var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                         ?? Application.Current.MainWindow;
                if (owner != null && owner != errorDialog)
                    errorDialog.Owner = owner;
                errorDialog.ShowDialog();
            }
            finally
            {
                IsCargando = false;
            }
        }



        private void CerrarDialogo()
        {
            if (Application.Current.Windows.OfType<VerInventarioDialog>().FirstOrDefault() is VerInventarioDialog dialog)
            {
                dialog.Close();
            }
        }
        #endregion
    }

    // DTO para las líneas del inventario

} 