using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using System.Collections.ObjectModel;

namespace SGA_Desktop.ViewModels
{
    public partial class OrdenTraspasoViewModel : ObservableObject
    {
        private readonly OrdenTraspasoService _ordenTraspasoService;

        [ObservableProperty]
        private ObservableCollection<OrdenTraspasoDto> ordenesTraspaso = new();

        [ObservableProperty]
        private OrdenTraspasoDto? ordenSeleccionada;

        [ObservableProperty]
        private bool isLoading;

        public OrdenTraspasoViewModel()
        {
            _ordenTraspasoService = new OrdenTraspasoService();
            _ = LoadOrdenesTraspasoAsync();
        }

        [RelayCommand]
        private async Task LoadOrdenesTraspasoAsync()
        {
            try
            {
                IsLoading = true;
                var ordenes = await _ordenTraspasoService.GetOrdenesTraspasoAsync();
                
                OrdenesTraspaso.Clear();
                foreach (var orden in ordenes)
                {
                    OrdenesTraspaso.Add(orden);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar órdenes de traspaso: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void CrearOrden()
        {
            var dialog = new CrearOrdenTraspasoDialog();
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                _ = LoadOrdenesTraspasoAsync();
            }
        }
    }
} 