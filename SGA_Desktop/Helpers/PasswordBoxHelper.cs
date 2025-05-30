using System.Windows;
using System.Windows.Controls;

namespace SGA_Desktop.Helpers
{
	/// <summary>
	/// Helper que permite hacer binding bidireccional (TwoWay) con PasswordBox.Password
	/// al estilo MVVM, ya que Password no es DependencyProperty por defecto.
	/// </summary>
	public static class PasswordBoxHelper
	{
		// Propiedad a la que se enlazará desde el ViewModel (ej. BoundPassword="{Binding Contraseña}")
		public static readonly DependencyProperty BoundPasswordProperty =
			DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
				new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

		// Habilita o deshabilita el binding del PasswordBox (por si no lo necesitas en todos)
		public static readonly DependencyProperty BindPasswordProperty =
			DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper),
				new PropertyMetadata(false, OnBindPasswordChanged));

		// Interno: evita bucles infinitos al sincronizar cambios
		private static readonly DependencyProperty UpdatingPasswordProperty =
			DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxHelper),
				new PropertyMetadata(false));

		// Getters y Setters públicos (para poder usarlos desde XAML)
		public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
		public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);

		public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPasswordProperty);
		public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPasswordProperty, value);

		// Se dispara cuando cambia el valor desde el ViewModel
		private static void OnBoundPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
		{
			if (dp is PasswordBox passwordBox)
			{
				passwordBox.PasswordChanged -= HandlePasswordChanged;

				// Solo actualizamos si no estamos en ciclo de sincronización
				if (!(bool)passwordBox.GetValue(UpdatingPasswordProperty))
				{
					passwordBox.Password = (string)e.NewValue;
				}

				passwordBox.PasswordChanged += HandlePasswordChanged;
			}
		}

		// Se dispara cuando en XAML activas BindPassword="True"
		private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
		{
			if (dp is PasswordBox passwordBox)
			{
				if ((bool)e.NewValue)
				{
					passwordBox.PasswordChanged += HandlePasswordChanged;
				}
				else
				{
					passwordBox.PasswordChanged -= HandlePasswordChanged;
				}
			}
		}

		// Se ejecuta cada vez que el usuario escribe en el PasswordBox
		private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
		{
			if (sender is PasswordBox passwordBox)
			{
				passwordBox.SetValue(UpdatingPasswordProperty, true);
				SetBoundPassword(passwordBox, passwordBox.Password);
				passwordBox.SetValue(UpdatingPasswordProperty, false);
			}
		}
	}
}
