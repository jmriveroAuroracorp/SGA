using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Helpers;
using System.Threading.Tasks;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string usuario;

        [ObservableProperty]
        private string contraseña;

        [RelayCommand]
        public async Task IniciarSesion()
        {
            if (!int.TryParse(Usuario, out int operario))
            {
                MessageBox.Show("El campo usuario debe ser numérico.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Contraseña))
            {
                MessageBox.Show("Introduce la contraseña.");
                return;
            }

            var loginService = new LoginService();
            var respuesta = await loginService.LoginAsync(new LoginRequest
            {
                operario = operario,
                contraseña = Contraseña
            });

            if (respuesta != null)
            {
                SessionManager.UsuarioActual = respuesta;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var main = new MainWindow();
                    main.Show();
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is Login) window.Close();
                    }
                });
            }
            else
            {
                MessageBox.Show("Login incorrecto.");
            }
        }
    }
}