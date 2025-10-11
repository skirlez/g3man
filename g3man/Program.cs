using Gtk;

namespace g3man;

public static class Program
{
	public static DataLoader DataLoader;
	public static int Main(string[] args)
	{
		Application application = Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
		application.OnActivate += (sender, args) =>
		{
			DataLoader = new DataLoader();
			State.Read();
			
			MainWindow window = new MainWindow();
			application.AddWindow(window);
			window.Show();
		};
		return application.RunWithSynchronizationContext([]);
	}
}