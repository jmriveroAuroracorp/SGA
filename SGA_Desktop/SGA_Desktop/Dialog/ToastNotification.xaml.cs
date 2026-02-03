using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SGA_Desktop.Models;
using SGA_Desktop.ViewModels;

namespace SGA_Desktop.Dialog
{
    public partial class ToastNotification : Window
    {
        private DispatcherTimer _autoCloseTimer;
        private Storyboard? _fadeOutStoryboard;
        
        public static readonly DependencyProperty ColorFondoProperty =
            DependencyProperty.Register(
                nameof(ColorFondo),
                typeof(Brush),
                typeof(ToastNotification),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(243, 246, 251))));

        public static readonly DependencyProperty ColorBordeProperty =
            DependencyProperty.Register(
                nameof(ColorBorde),
                typeof(Brush),
                typeof(ToastNotification),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 103, 195))));

        public static readonly DependencyProperty ColorIconoProperty =
            DependencyProperty.Register(
                nameof(ColorIcono),
                typeof(Brush),
                typeof(ToastNotification),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 103, 195))));

        public Brush ColorFondo
        {
            get => (Brush)GetValue(ColorFondoProperty);
            set => SetValue(ColorFondoProperty, value);
        }

        public Brush ColorBorde
        {
            get => (Brush)GetValue(ColorBordeProperty);
            set => SetValue(ColorBordeProperty, value);
        }

        public Brush ColorIcono
        {
            get => (Brush)GetValue(ColorIconoProperty);
            set => SetValue(ColorIconoProperty, value);
        }

        public event EventHandler? ToastClosed;

        public ToastNotification(NotificacionDto notificacion)
        {
            InitializeComponent();
            
            // Configurar ventana para no interferir con desplegables
            this.Focusable = false;
            this.IsHitTestVisible = true; // Permitir clics en los botones
            
            // Configurar contenido
            TitleText.Text = notificacion.Titulo;
            MessageText.Text = notificacion.Mensaje;
            
            // Configurar colores según tipo
            ConfigurarColoresPorTipo(notificacion.Tipo);
            
            // Configurar icono según tipo
            ConfigurarIconoPorTipo(notificacion.Tipo);
            
            // Configurar auto-cierre después de 5 segundos
            InicializarAutoCierre();
        }

        private void ConfigurarColoresPorTipo(string tipo)
        {
            switch (tipo?.ToLower())
            {
                case "success":
                    ColorFondo = new SolidColorBrush(Color.FromRgb(232, 245, 232)); // Verde claro
                    ColorBorde = new SolidColorBrush(Color.FromRgb(76, 175, 80));   // Verde
                    ColorIcono = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
                    
                case "error":
                    ColorFondo = new SolidColorBrush(Color.FromRgb(255, 235, 238)); // Rojo claro
                    ColorBorde = new SolidColorBrush(Color.FromRgb(244, 67, 54));     // Rojo
                    ColorIcono = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    break;
                    
                case "warning":
                    ColorFondo = new SolidColorBrush(Color.FromRgb(255, 243, 224)); // Naranja claro
                    ColorBorde = new SolidColorBrush(Color.FromRgb(255, 152, 0));    // Naranja
                    ColorIcono = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    break;
                    
                case "info":
                default:
                    ColorFondo = new SolidColorBrush(Color.FromRgb(227, 242, 253)); // Azul claro
                    ColorBorde = new SolidColorBrush(Color.FromRgb(33, 150, 243));    // Azul
                    ColorIcono = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;
            }
        }

        private void ConfigurarIconoPorTipo(string tipo)
        {
            switch (tipo?.ToLower())
            {
                case "success":
                    IconText.Text = "\uE73E"; // CheckMark
                    break;
                    
                case "error":
                    IconText.Text = "\uE783"; // Cancel
                    break;
                    
                case "warning":
                    IconText.Text = "\uE7BA"; // Warning
                    break;
                    
                case "info":
                default:
                    IconText.Text = "\uE946"; // Info
                    break;
            }
        }

        private void InicializarAutoCierre()
        {
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoCloseTimer.Tick += AutoCloseTimer_Tick;
            _autoCloseTimer.Start();
        }

        private void AutoCloseTimer_Tick(object? sender, EventArgs e)
        {
            _autoCloseTimer.Stop();
            CerrarConAnimacion();
        }

        private void CerrarConAnimacion()
        {
            // Obtener la animación de fade out
            _fadeOutStoryboard = (Storyboard)Resources["FadeOutAnimation"];
            if (_fadeOutStoryboard != null)
            {
                // Especificar el objeto objetivo (this Window) para la animación
                // Cuando se llama manualmente, necesitamos pasar el objeto objetivo
                _fadeOutStoryboard.Begin(this);
            }
            else
            {
                // Si no hay animación, cerrar directamente
                CerrarToast();
            }
        }

        private void FadeOutAnimation_Completed(object? sender, EventArgs e)
        {
            CerrarToast();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true; // Evitar que se propague el click
            _autoCloseTimer?.Stop();
            CerrarConAnimacion();
        }

        private void CloseButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Evitar que se propague el click al Border y cierre desplegables
        }

        private void Border_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Solo procesar si no se hizo clic en el botón de cerrar
            if (e.OriginalSource is Button closeBtn && closeBtn.Name == "CloseButton")
                return;
                
            // Abrir la ventana de notificaciones
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.AbrirNotificacionesCommand?.Execute(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al abrir notificaciones desde toast: {ex.Message}");
            }
        }

        private void CerrarToast()
        {
            ToastClosed?.Invoke(this, EventArgs.Empty);
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _autoCloseTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
