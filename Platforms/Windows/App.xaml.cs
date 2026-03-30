using Microsoft.UI.Xaml;

namespace LocalBackupMaster.WinUI;

public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.UnhandledException += OnUnhandledException;
		this.InitializeComponent();
	}

	private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		try
		{
			string log = $"[{DateTime.Now}] {e.Exception}\n\nMessage: {e.Message}\n";
			File.AppendAllText(Path.Combine(Path.GetTempPath(), "lbm_crash.txt"), log);
		}
		catch { /* ignorar errores de logging */ }
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

