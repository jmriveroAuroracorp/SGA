using System.Windows;

namespace SGA_Desktop.Dialog
{
    /// <summary>
    /// Modal para mostrar las notificaciones del usuario
    /// </summary>
    public partial class NotificacionesModal : Window
    {
        public NotificacionesModal()
        {
            InitializeComponent();
            this.Loaded += NotificacionesModal_Loaded;
        }

        private void NotificacionesModal_Loaded(object sender, RoutedEventArgs e)
        {
            // Asegurar que la ventana sea visible
            this.Visibility = Visibility.Visible;
            this.ShowActivated = true;
            
            // Asegurar que esté en primer plano
            this.Activate();
            this.Focus();
            
            // Log para debug
            System.Diagnostics.Debug.WriteLine($"✅ Modal cargado - Posición: X={this.Left}, Y={this.Top}, Width={this.Width}, Height={this.Height}, IsVisible={this.IsVisible}");
        }
    }
}
