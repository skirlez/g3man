using Gtk;

namespace g3man;

public class Program {
	public static int Main(string[] args) {
		Application application = Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
		application.OnActivate += (sender, args) =>  {
			MainWindow window = new MainWindow();
			application.AddWindow(window);
			window.Show();
		};
		return application.RunWithSynchronizationContext([]);
	}
}