
using g3man.Patching;
using Gtk;
using UndertaleModLib;

namespace g3man.UI;

public class PatcherWindow : Window {
	private Label statusLabel;
	
	MainWindow owner;
	public PatcherWindow(MainWindow owner) {
		SetSizeRequest(350, 150);
		SetResizable(false);
		this.owner = owner;
		statusLabel = Label.New("Waiting for the game's data to load...");
		SetChild(statusLabel);
	}

	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
		

		new Thread(() => {
			Patcher patcher = new  Patcher();
			patcher.Patch([], Program.GetProfile()!, Program.GetGame()!, (status => {
				Program.RunOnMainThreadEventually(() => {
					statusLabel.SetText(status);
				});
			}));
		}).Start();

	}
}