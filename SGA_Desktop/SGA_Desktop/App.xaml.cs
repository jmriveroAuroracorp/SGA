using System.Configuration;
using System.Data;
using System.Windows;
using System;

namespace SGA_Desktop
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public App()
		{
			AppDomain.CurrentDomain.UnhandledException += (s, e) =>
			{
				MessageBox.Show(e.ExceptionObject.ToString(), "Error global");
			};
			this.DispatcherUnhandledException += (s, e) =>
			{
				MessageBox.Show(e.Exception.ToString(), "Error de UI");
				e.Handled = true;
			};
		}
	}

}
