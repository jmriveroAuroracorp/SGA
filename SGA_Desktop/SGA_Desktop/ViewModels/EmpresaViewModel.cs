using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Helpers;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels;

public partial class EmpresaViewModel : ObservableObject
{
    public ObservableCollection<EmpresaDto> Empresas { get; }

    [ObservableProperty] private EmpresaDto? empresaSeleccionada;

    private readonly LoginService _loginService;            // ← solo UNA instancia

    // === 1 · Constructor: recibe el servicio =====
    public EmpresaViewModel(LoginService loginService)
    {
        _loginService = loginService;

        // lista que llegó en el login
        Empresas = new ObservableCollection<EmpresaDto>(
            SessionManager.UsuarioActual?.empresas ?? new());

        // ► Sin selección inicial:
        empresaSeleccionada = null;
    }

    // === 2 · Comando Aceptar ======================
    [RelayCommand]
    private async Task Aceptar()
    {
        if (EmpresaSeleccionada is null)
        {
            MessageBox.Show("Selecciona una empresa primero");
            return;
        }

        // 1) guarda globalmente y lanza evento
        SessionManager.SetEmpresa(EmpresaSeleccionada.Codigo);

        // 2) PATCH/PUT a la API
        var (ok, detalle, status) = await _loginService
            .EstablecerEmpresaPreferidaAsync(SessionManager.UsuarioActual!.operario,
                                             EmpresaSeleccionada.Codigo);

        if (!ok)
        {
            MessageBox.Show($"Status: {(int)status} {status}\n{detalle}",
                            "No se pudo guardar la empresa por defecto");
            return; // evita cerrar si falló
        }

        //// 3) volver a la pantalla anterior
        //NavigationStore.MainFrame.GoBack();
    }
}
