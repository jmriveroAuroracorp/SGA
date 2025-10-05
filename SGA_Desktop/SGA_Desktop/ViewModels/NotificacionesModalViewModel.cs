using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SGA_Desktop.ViewModels
{
    /// <summary>
    /// ViewModel para el modal de notificaciones
    /// </summary>
    public partial class NotificacionesModalViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<NotificacionDto> notificaciones = new();
        
        [ObservableProperty]
        private int contadorTotal;
        
        [ObservableProperty]
        private int contadorNotificacionesPositivas;
        
        [ObservableProperty]
        private int contadorNotificacionesNegativas;
        
        [ObservableProperty]
        private bool tieneNotificaciones;
        
        public NotificacionesModalViewModel()
        {
            CargarNotificaciones();
        }
        
        /// <summary>
        /// Carga las notificaciones pendientes del usuario actual
        /// </summary>
        private void CargarNotificaciones()
        {
            Notificaciones.Clear();
            var notificaciones = NotificacionesManager.ObtenerNotificacionesPendientes();
            
            foreach (var n in notificaciones)
            {
                Notificaciones.Add(n);
            }
            
            ContadorTotal = notificaciones.Count;
            ContadorNotificacionesPositivas = notificaciones.Count(n => n.EsPositiva);
            ContadorNotificacionesNegativas = notificaciones.Count(n => n.EsNegativa);
            TieneNotificaciones = ContadorTotal > 0;
        }
        
        /// <summary>
        /// Comando para marcar todas las notificaciones como leídas
        /// </summary>
        [RelayCommand]
        private void MarcarTodasLeidas()
        {
            try
            {
                NotificacionesManager.MarcarTodasComoLeidas();
                CargarNotificaciones();
                
                System.Diagnostics.Debug.WriteLine("✅ Todas las notificaciones marcadas como leídas");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al marcar notificaciones como leídas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Comando para cerrar el modal
        /// </summary>
        [RelayCommand]
        private void Cerrar()
        {
            try
            {
                // Buscar la ventana actual y cerrarla
                var currentWindow = Application.Current.Windows.OfType<NotificacionesModal>().FirstOrDefault();
                if (currentWindow != null)
                {
                    currentWindow.Close();
                }
                
                System.Diagnostics.Debug.WriteLine("✅ Modal de notificaciones cerrado");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al cerrar modal: {ex.Message}");
            }
        }
    }
}
