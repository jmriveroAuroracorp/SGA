using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace SGA_Desktop.Dialog
{
    public partial class ExcelPreviewDialog : Window
    {
        private string _archivoExcel;
        private string _nombreArchivo;

        public ExcelPreviewDialog(string archivoExcel, string nombreArchivo)
        {
            InitializeComponent();
            _archivoExcel = archivoExcel;
            _nombreArchivo = nombreArchivo;
            
            CargarExcel();
        }

        private void CargarExcel()
        {
            try
            {
                var fechaActual = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                TxtInfo.Text = $"VISTA PREVIA: {_nombreArchivo} - {fechaActual}";
                TxtEstado.Text = "Convirtiendo a HTML...";
                
                // Convertir Excel a HTML para visualización
                var htmlContent = ConvertirExcelAHtml(_archivoExcel);
                WebBrowserExcel.NavigateToString(htmlContent);
                
                TxtEstado.Text = "Archivo cargado correctamente";
            }
            catch (Exception ex)
            {
                TxtEstado.Text = $"Error al cargar: {ex.Message}";
                var error = new WarningDialog(
                    "Error al cargar Excel",
                    $"No se pudo cargar el archivo:\n{ex.Message}",
                    "\uE814"
                );
                error.Owner = this;
                error.ShowDialog();
            }
        }

        private string ConvertirExcelAHtml(string rutaExcel)
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(rutaExcel);
                var worksheet = workbook.Worksheets.First();
                
                var html = new StringBuilder();
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html><head>");
                html.AppendLine("<meta charset='utf-8'>");
                html.AppendLine("<meta http-equiv='X-UA-Compatible' content='IE=edge'>");
                html.AppendLine("<style>");
                html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 20px; background: #f8f9fa; }");
                html.AppendLine(".container { background: white; padding: 25px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }");
                html.AppendLine(".header { text-align: center; margin-bottom: 25px; border-bottom: 2px solid #0d6efd; padding-bottom: 15px; }");
                html.AppendLine(".header h1 { color: #0d6efd; margin: 0 0 10px 0; font-size: 24px; }");
                html.AppendLine(".header p { color: #6c757d; margin: 5px 0; font-size: 14px; }");
                html.AppendLine("table { border-collapse: collapse; width: 100%; margin-top: 10px; }");
                html.AppendLine("th, td { border: 1px solid #dee2e6; padding: 10px; text-align: left; font-size: 13px; }");
                html.AppendLine("th { background-color: #0d6efd; color: white; font-weight: 600; position: sticky; top: 0; }");
                html.AppendLine("tr:nth-child(even) { background-color: #f8f9fa; }");
                html.AppendLine("tr:hover { background-color: #e7f1ff; }");
                html.AppendLine("td { color: #212529; }");
                html.AppendLine(".total-registros { text-align: right; margin-top: 15px; color: #6c757d; font-size: 13px; font-weight: 600; }");
                html.AppendLine("@media print {");
                html.AppendLine("  body { background: white !important; margin: 0; padding: 10px; }");
                html.AppendLine("  .container { box-shadow: none !important; border: 1px solid #ccc; }");
                html.AppendLine("  .header h1 { color: #000 !important; }");
                html.AppendLine("  .header p { color: #666 !important; }");
                html.AppendLine("  th { background-color: #f0f0f0 !important; color: #000 !important; }");
                html.AppendLine("  tr:nth-child(even) { background-color: #f9f9f9 !important; }");
                html.AppendLine("  tr:hover { background-color: transparent !important; }");
                html.AppendLine("  .total-registros { color: #000 !important; }");
                html.AppendLine("}");
                html.AppendLine("</style>");
                html.AppendLine("</head><body>");
                
                html.AppendLine("<div class='container'>");
                html.AppendLine("<div class='header'>");
                html.AppendLine($"<h1>📊 {System.Net.WebUtility.HtmlEncode(_nombreArchivo)}</h1>");
                html.AppendLine($"<p>Generado el {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
                html.AppendLine("</div>");
                
                // Obtener el rango usado
                var usedRange = worksheet.RangeUsed();
                if (usedRange != null)
                {
                    var firstRow = usedRange.FirstRow().RowNumber();
                    var lastRow = usedRange.LastRow().RowNumber();
                    var firstCol = usedRange.FirstColumn().ColumnNumber();
                    var lastCol = usedRange.LastColumn().ColumnNumber();
                    
                    var totalFilas = lastRow - firstRow; // Excluir la cabecera
                    
                    html.AppendLine("<table>");
                    
                    // Cabeceras
                    html.AppendLine("<thead><tr>");
                    for (int col = firstCol; col <= lastCol; col++)
                    {
                        var cellValue = worksheet.Cell(firstRow, col).Value.ToString();
                        html.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(cellValue)}</th>");
                    }
                    html.AppendLine("</tr></thead>");
                    
                    // Datos
                    html.AppendLine("<tbody>");
                    for (int row = firstRow + 1; row <= lastRow; row++)
                    {
                        html.AppendLine("<tr>");
                        for (int col = firstCol; col <= lastCol; col++)
                        {
                            var cell = worksheet.Cell(row, col);
                            var cellValue = cell.Value.ToString();
                            html.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(cellValue)}</td>");
                        }
                        html.AppendLine("</tr>");
                    }
                    html.AppendLine("</tbody>");
                    html.AppendLine("</table>");
                    
                    html.AppendLine($"<div class='total-registros'>Total de registros: {totalFilas}</div>");
                }
                else
                {
                    html.AppendLine("<p style='text-align: center; color: #6c757d; margin: 40px 0;'>No hay datos para mostrar</p>");
                }
                
                html.AppendLine("</div>");
                html.AppendLine("</body></html>");
                
                return html.ToString();
            }
            catch (Exception ex)
            {
                return $@"
                    <html>
                        <head><meta charset='utf-8'></head>
                        <body style='font-family: Arial; text-align: center; padding: 50px; background: #f5f5f5;'>
                            <div style='background: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); max-width: 600px; margin: 0 auto;'>
                                <h2 style='color: #dc3545; margin-bottom: 20px;'>❌ Error al cargar Excel</h2>
                                <p style='font-size: 16px; color: #666; margin-bottom: 20px;'>
                                    No se pudo convertir el archivo Excel a HTML.
                                </p>
                                <p style='font-size: 14px; color: #888; background: #f8f9fa; padding: 15px; border-radius: 5px; font-family: monospace;'>
                                    {System.Net.WebUtility.HtmlEncode(ex.Message)}
                                </p>
                                <p style='font-size: 12px; color: #999; margin-top: 20px;'>
                                    Stack: {System.Net.WebUtility.HtmlEncode(ex.StackTrace)}
                                </p>
                            </div>
                        </body>
                    </html>";
            }
        }

        private void BtnDescargar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Libro de Excel (*.xlsx)|*.xlsx",
                    FileName = _nombreArchivo,
                    Title = "Guardar archivo Excel"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.Copy(_archivoExcel, saveDialog.FileName, overwrite: true);
                    
                    var confirmacion = new WarningDialog(
                        "Descarga completada",
                        $"Archivo guardado en:\n{saveDialog.FileName}",
                        "\uE946"
                    );
                    confirmacion.Owner = this;
                    confirmacion.ShowDialog();
                    
                    TxtEstado.Text = "Archivo descargado correctamente";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al descargar el archivo:\n{ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImprimir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtEstado.Text = "Preparando impresión...";
                
                // Imprimir directamente el contenido HTML del WebBrowser
                WebBrowserExcel.InvokeScript("execScript", new object[] { "window.print();", "JavaScript" });
                
                TxtEstado.Text = "Impresión enviada correctamente";
            }
            catch (Exception ex)
            {
                TxtEstado.Text = $"Error al imprimir: {ex.Message}";
                var error = new WarningDialog(
                    "Error al imprimir",
                    $"No se pudo enviar a impresión:\n{ex.Message}",
                    "\uE814"
                );
                error.Owner = this;
                error.ShowDialog();
            }
        }


        private void WebBrowserExcel_LoadCompleted(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            TxtEstado.Text = "Archivo cargado correctamente";
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
