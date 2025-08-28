using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace SGA_Desktop.Helpers
{
    /// <summary>
    /// Behavior para validar entrada de decimales en TextBox
    /// </summary>
    public static class DecimalInputBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(DecimalInputBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if ((bool)e.NewValue)
                {
                    textBox.PreviewTextInput += OnPreviewTextInput;
                    textBox.PreviewKeyDown += OnPreviewKeyDown;
                }
                else
                {
                    textBox.PreviewTextInput -= OnPreviewTextInput;
                    textBox.PreviewKeyDown -= OnPreviewKeyDown;
                }
            }
        }

        private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Construir el texto que resultaría después de la entrada
                var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
                
                // Permitir solo números y punto decimal
                foreach (char c in e.Text)
                {
                    if (!char.IsDigit(c) && c != '.')
                    {
                        e.Handled = true;
                        return;
                    }
                }

                // Verificar que no haya más de un punto decimal en el texto completo
                var dotCount = newText.Count(c => c == '.');
                if (dotCount > 1)
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Permitir teclas de navegación y edición
                if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Tab ||
                    e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
                {
                    return;
                }

                // Permitir Ctrl+A, Ctrl+C, Ctrl+V, Ctrl+X
                if (e.Key == Key.A || e.Key == Key.C || e.Key == Key.V || e.Key == Key.X)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        return;
                    }
                }

                // Para cualquier otra tecla, verificar si es un número o punto
                if (e.Key >= Key.D0 && e.Key <= Key.D9)
                {
                    return; // Números
                }

                if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                {
                    return; // Números del teclado numérico
                }

                if (e.Key == Key.Decimal || e.Key == Key.OemPeriod)
                {
                    // Verificar que no haya ya un punto decimal
                    if (!textBox.Text.Contains('.'))
                    {
                        return;
                    }
                }

                // Bloquear cualquier otra tecla
                e.Handled = true;
            }
        }
    }
} 