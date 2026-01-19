using CommunityToolkit.Mvvm.ComponentModel;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace SGA_Desktop.ViewModels
{
    public partial class HistorialRenovacionesDialogViewModel : ObservableObject
    {
        private readonly ConteoPeriodicoDto _conteoPeriodico;
        private readonly ConteosService _conteosService;
        private readonly LoginService _loginService;

        [ObservableProperty]
        private ObservableCollection<OrdenConteoDto> _renovaciones = new();

        [ObservableProperty]
        private bool _isCargando = false;

        public string TituloConteo => _conteoPeriodico?.Titulo ?? "Sin título";
        public int TotalRenovaciones => Renovaciones.Count;
        public int RenovacionesCumplidas => Renovaciones.Count(r => r.Estado == "CERRADO");
        public int RenovacionesPendientes => Renovaciones.Count(r => r.Estado != "CERRADO" && r.Estado != "CANCELADO");
        public double PorcentajeCumplimiento => TotalRenovaciones > 0 
            ? (double)RenovacionesCumplidas / TotalRenovaciones * 100 
            : 0;
        
        public bool TieneRenovaciones => Renovaciones.Count > 0;

        public HistorialRenovacionesDialogViewModel(ConteoPeriodicoDto conteoPeriodico, ConteosService conteosService, LoginService loginService)
        {
            _conteoPeriodico = conteoPeriodico;
            _conteosService = conteosService;
            _loginService = loginService;
            
            _ = CargarRenovacionesAsync();
        }

        private async Task CargarRenovacionesAsync()
        {
            try
            {
                IsCargando = true;

                var renovaciones = await _conteosService.ObtenerRenovacionesAsync(_conteoPeriodico.GuidID);

                // Cargar operarios para mapear nombres
                var operarios = await _loginService.ObtenerOperariosConAccesoConteosAsync();
                var operariosDict = operarios
                    .Where(op => op.Operario > 0)
                    .ToDictionary(op => op.Operario.ToString(), op => op.NombreOperario ?? "");

                Renovaciones.Clear();
                foreach (var renovacion in renovaciones.OrderByDescending(r => r.FechaCreacion))
                {
                    // Mapear nombre del operario si existe
                    if (!string.IsNullOrEmpty(renovacion.CodigoOperario) && 
                        operariosDict.TryGetValue(renovacion.CodigoOperario, out var nombreOperario))
                    {
                        renovacion.NombreOperario = nombreOperario;
                    }
                    
                    // Mapear nombre del creador si existe
                    if (!string.IsNullOrEmpty(renovacion.CreadoPorCodigo) && 
                        operariosDict.TryGetValue(renovacion.CreadoPorCodigo, out var nombreCreador))
                    {
                        renovacion.NombreCreador = nombreCreador;
                    }
                    
                    Renovaciones.Add(renovacion);
                }

                OnPropertyChanged(nameof(TotalRenovaciones));
                OnPropertyChanged(nameof(RenovacionesCumplidas));
                OnPropertyChanged(nameof(RenovacionesPendientes));
                OnPropertyChanged(nameof(PorcentajeCumplimiento));
                OnPropertyChanged(nameof(TieneRenovaciones));
            }
            catch (Exception ex)
            {
                // El error se manejará en el diálogo
                System.Diagnostics.Debug.WriteLine($"Error al cargar renovaciones: {ex.Message}");
            }
            finally
            {
                IsCargando = false;
            }
        }
    }
}
