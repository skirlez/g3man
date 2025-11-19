
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
		statusLabel.SetMargin(20);
		statusLabel.SetVexpand(true);
		
		closeButton = Button.NewWithLabel("Close");
		closeButton.SetSensitive(false);
		closeButton.SetValign(Align.End);
		closeButton.SetHalign(Align.Center);
		closeButton.OnClicked += (_, _) => {
			Close();
		};
		closeButton.SetMarginBottom(10);
		
		Box box = Box.New(Orientation.Vertical, 10);
		box.Append(statusLabel);
		box.Append(closeButton);

		OnCloseRequest += (sender, args) => !closeButton.IsSensitive();
		SetChild(box);
	}

	public void Dialog(List<Mod> mods) {
		SetTransientFor(owner);
		SetModal(true);
		
		new Thread(() => {

			void setStatus(string status, bool leave) {
				statusLabel.SetText(status);
				closeButton.SetSensitive(leave);
				if (!IsVisible())
					Present();
			}
			UndertaleData data;
			lock (Program.DataLoader.Lock) {
				while (!Program.DataLoader.CanSnatch()) {
					if (Program.DataLoader.HasErrored()) {
						setStatus($"Failed to load game's data.win. I don't know what to do in this situation yet. TODO", true);
						return;
					}
					setStatus($"Waiting for game data to load...", false);
					Monitor.Wait(Program.DataLoader.Lock);
				}
				data = Program.DataLoader.Snatch();
			}
			Patcher patcher = new Patcher();
			patcher.Patch(mods, Program.GetProfile()!, 
				Path.Combine(Program.GetGame()!.Directory, "g3man"), data, Program.GetGame()!.Directory, (status, leave) => {
				Program.RunOnMainThreadEventually(() => setStatus(status, leave));
			});
		}).Start();

	}
}