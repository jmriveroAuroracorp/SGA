using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGA_Desktop.Models;
using SGA_Desktop.Services;
using SGA_Desktop.Dialog;
using SGA_Desktop.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System;

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
        
        [ObservableProperty]
        private bool separadorVisible;
        
        [ObservableProperty]
        private bool estaCargando;
        
        [ObservableProperty]
        private bool hayErrorConexion;
        
        [ObservableProperty]
        private string mensajeError;
        
        public NotificacionesModalViewModel()
        {
            CargarNotificaciones();
            
            // Suscribirse a cambios en el contador para actualizar automáticamente
            NotificacionesManager.OnContadorCambiado += OnContadorCambiado;
        }
        
        /// <summary>
        /// Carga las notificaciones pendientes del usuario actual
        /// </summary>
        private async void CargarNotificaciones()
        {
            try
            {
                EstaCargando = true;
                HayErrorConexion = false;
                MensajeError = string.Empty;
                
                System.Diagnostics.Debug.WriteLine("📥 Cargando notificaciones en modal...");
                
                // Obtener notificaciones del NotificacionesManager (que ya tiene las de BD)
                var notificaciones = NotificacionesManager.ObtenerNotificacionesPendientes();
                
                Notificaciones.Clear();
                foreach (var n in notificaciones)
                {
                    Notificaciones.Add(n);
                }
                
                ActualizarContadores();
                
                System.Diagnostics.Debug.WriteLine($"✅ Cargadas {notificaciones.Count} notificaciones en modal");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al cargar notificaciones en modal: {ex.Message}");
                HayErrorConexion = true;
                MensajeError = "Error al cargar notificaciones. Verificando conexión...";
            }
            finally
            {
                EstaCargando = false;
            }
        }
        
        /// <summary>
        /// Actualiza los contadores de notificaciones
        /// </summary>
        private void ActualizarContadores()
        {
            ContadorTotal = Notificaciones.Count;
            ContadorNotificacionesPositivas = Notificaciones.Count(n => n.EsPositiva);
            ContadorNotificacionesNegativas = Notificaciones.Count(n => n.EsNegativa);
            TieneNotificaciones = ContadorTotal > 0;
            
            // Mostrar separador solo cuando hay ambos tipos de notificaciones
            SeparadorVisible = ContadorNotificacionesPositivas > 0 && ContadorNotificacionesNegativas > 0;
        }
        
        /// <summary>
        /// Maneja cambios en el contador de notificaciones
        /// </summary>
        private void OnContadorCambiado(int nuevoContador)
        {
            // Recargar notificaciones cuando cambie el contador
            CargarNotificaciones();
        }
        
        /// <summary>
        /// Comando para marcar una notificación específica como leída
        /// </summary>
        [RelayCommand]
        private async Task MarcarComoLeida(NotificacionDto notificacion)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"✅ Marcando notificación como leída: {notificacion.Titulo}");
                
                var exito = await NotificacionesManager.MarcarComoLeidaAsync(notificacion.Id);
                
                if (exito)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Notificación marcada como leída exitosamente");
                    
                    // Recargar notificaciones para actualizar la UI
                    CargarNotificaciones();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ No se pudo marcar la notificación como leída");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al marcar notificación como leída: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Comando para marcar todas las notificaciones como leídas
        /// </summary>
        [RelayCommand]
        private async Task MarcarTodasLeidas()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("✅ Marcando todas las notificaciones como leídas...");
                
                var cantidadMarcadas = await NotificacionesManager.MarcarTodasComoLeidasAsync();
                
                if (cantidadMarcadas > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ {cantidadMarcadas} notificaciones marcadas como leídas");
                    
                    // Recargar notificaciones para actualizar la UI
                    CargarNotificaciones();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No había notificaciones pendientes para marcar como leídas");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al marcar notificaciones como leídas: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Comando para recargar notificaciones
        /// </summary>
        [RelayCommand]
        private async Task RecargarNotificaciones()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Recargando notificaciones...");
                
                // Actualizar contador desde BD
                await NotificacionesManager.ActualizarContadorAsync();
                
                // Recargar notificaciones
                CargarNotificaciones();
                
                System.Diagnostics.Debug.WriteLine("✅ Notificaciones recargadas");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al recargar notificaciones: {ex.Message}");
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
                // Desuscribirse de eventos
                NotificacionesManager.OnContadorCambiado -= OnContadorCambiado;
                
                // Buscar la ventana actual y cerrarla
                var currentWindow = Application.Current.Windows.OfType<NotificacionesModal>().FirstOrDefault();
                if (currentWindow != null)
                {
                    currentWindow.Close();
                }
                
                System.Diagnostics.Debug.WriteLine("✅ Modal de notificaciones cerrado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al cerrar modal: {ex.Message}");
            }
        }
    }
}
