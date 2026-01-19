using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SGA_Desktop.Dialog;
using SGA_Desktop.Models;

namespace SGA_Desktop.Services
{
    /// <summary>
    /// Manager para gestionar la visualización de notificaciones toast
    /// </summary>
    public class ToastNotificationManager
    {
        private static ToastNotificationManager? _instancia;
        private static readonly object _lock = new object();
        
        private readonly Queue<NotificacionDto> _colaNotificaciones = new();
        private ToastNotification? _toastActual;
        private Button? _botonCampanita;
        private bool _mostrandoToast = false;

        private ToastNotificationManager() { }

        public static ToastNotificationManager Instancia
        {
            get
            {
                if (_instancia == null)
                {
                    lock (_lock)
                    {
                        _instancia ??= new ToastNotificationManager();
                    }
                }
                return _instancia;
            }
        }

        /// <summary>
        /// Inicializa el manager con la referencia al botón de la campanita
        /// </summary>
        public void Inicializar(Button botonCampanita)
        {
            _botonCampanita = botonCampanita;
        }

        /// <summary>
        /// Muestra un toast de notificación. Si hay uno activo, lo encola.
        /// </summary>
        public void MostrarToast(NotificacionDto notificacion)
        {
            if (notificacion == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ ToastNotificationManager: Notificación es null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Intentando mostrar toast - {notificacion.Titulo}");

            lock (_lock)
            {
                _colaNotificaciones.Enqueue(notificacion);
                
                if (!_mostrandoToast)
                {
                    System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: No hay toast activo, mostrando inmediatamente");
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        MostrarSiguienteToast();
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Hay toast activo, encolando. Cola: {_colaNotificaciones.Count}");
                }
            }
        }

        private void MostrarSiguienteToast()
        {
            System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: MostrarSiguienteToast - Cola: {_colaNotificaciones.Count}");
            
            if (_colaNotificaciones.Count == 0)
            {
                _mostrandoToast = false;
                return;
            }

            NotificacionDto? notificacion = null;
            lock (_lock)
            {
                if (_colaNotificaciones.Count > 0)
                {
                    notificacion = _colaNotificaciones.Dequeue();
                }
            }

            if (notificacion == null)
            {
                _mostrandoToast = false;
                return;
            }

            _mostrandoToast = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Creando toast para: {notificacion.Titulo}");
                
                var toast = new ToastNotification(notificacion);
                toast.ToastClosed += OnToastClosed;
                
                // Calcular posición debajo de la campanita
                var posicion = CalcularPosicion();
                System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Posición calculada - X: {posicion.X}, Y: {posicion.Y}");
                
                toast.Left = posicion.X;
                toast.Top = posicion.Y;
                
                System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Mostrando toast...");
                toast.Show();
                _toastActual = toast;
                
                System.Diagnostics.Debug.WriteLine($"✅ ToastNotificationManager: Toast mostrado correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al mostrar toast: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                _mostrandoToast = false;
                // Intentar mostrar el siguiente
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MostrarSiguienteToast();
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
        }

        private void OnToastClosed(object? sender, EventArgs e)
        {
            if (sender is ToastNotification toast)
            {
                toast.ToastClosed -= OnToastClosed;
            }

            _toastActual = null;
            
            // Esperar un pequeño delay antes de mostrar el siguiente (para mejor UX)
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                Thread.Sleep(300); // 300ms de pausa entre toasts
                MostrarSiguienteToast();
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        private Point CalcularPosicion()
        {
            if (_botonCampanita == null)
            {
                System.Diagnostics.Debug.WriteLine("⚠️ ToastNotificationManager: Botón de campanita es null, usando fallback");
                // Fallback: esquina superior derecha de la pantalla principal
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                return new Point(screenWidth - 420, 50); // 420 = ancho estimado del toast + margen
            }

            try
            {
                // Verificar que el botón esté cargado
                if (!_botonCampanita.IsLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ ToastNotificationManager: Botón no está cargado aún");
                }
                
                // Obtener posición del botón en coordenadas de pantalla
                var puntoOrigen = new System.Windows.Point(0, 0);
                var puntoPantalla = _botonCampanita.PointToScreen(puntoOrigen);
                
                System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Botón posición pantalla - X: {puntoPantalla.X}, Y: {puntoPantalla.Y}, Width: {_botonCampanita.ActualWidth}, Height: {_botonCampanita.ActualHeight}");
                
                // Calcular posición del toast: debajo del botón, alineado a la derecha
                var anchoToast = 400; // Ancho máximo del toast
                var alturaBotón = _botonCampanita.ActualHeight > 0 ? _botonCampanita.ActualHeight : 40;
                var margen = 10; // Margen entre botón y toast
                
                var x = puntoPantalla.X + _botonCampanita.ActualWidth - anchoToast;
                var y = puntoPantalla.Y + alturaBotón + margen;
                
                // Asegurar que no se salga de la pantalla
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                if (x < 0) x = 10; // Margen izquierdo mínimo
                if (x + anchoToast > screenWidth) x = screenWidth - anchoToast - 10;
                if (y < 0) y = 10; // Margen superior mínimo
                
                System.Diagnostics.Debug.WriteLine($"🔔 ToastNotificationManager: Posición final - X: {x}, Y: {y}");
                
                return new Point(x, y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al calcular posición del toast: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                // Fallback: esquina superior derecha
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                return new Point(screenWidth - 420, 50);
            }
        }

        /// <summary>
        /// Limpia la cola de notificaciones pendientes
        /// </summary>
        public void LimpiarCola()
        {
            lock (_lock)
            {
                _colaNotificaciones.Clear();
            }
        }
    }
}
