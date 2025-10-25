
using g3man.Models;
using g3man.Patching;
using Gtk;
using UndertaleModLib;

namespace g3man.UI;

public class PatcherWindow : Window {
	private Label statusLabel;
	private Button closeButton;
	private MainWindow owner;
	public PatcherWindow(MainWindow owner) {
		this.owner = owner;
		
		SetSizeRequest(400, 300);
		SetResizable(false);
		statusLabel = Label.New("");
		statusLabel.SetJustify(Justification.Center);
		statusLabel.SetValign(Align.Center);
		statusLabel.SetHalign(Align.Center);
		
		closeButton = Button.NewWithLabel("Close");
		closeButton.SetSensitive(false);
		closeButton.SetVexpand(true);
		closeButton.SetValign(Align.End);
		closeButton.SetHalign(Align.Center);
		closeButton.OnClicked += (_, _) => {
			Close();
		};
		
		Box box = Box.New(Orientation.Vertical, 10);
		box.Append(statusLabel);
		box.Append(closeButton);
		
		SetChild(box);
	}

	public void Dialog(List<Mod> mods) {
		SetTransientFor(owner);
		SetModal(true);
		Present();
		
		new Thread(() => {
			Patcher patcher = new Patcher();
			patcher.Patch(mods, Program.GetProfile()!, Program.GetGame()!, (status, leave) => {
				Program.RunOnMainThreadEventually(() => {
					statusLabel.SetText(status);
					closeButton.SetSensitive(leave);
				});
			});
		}).Start();

	}
}