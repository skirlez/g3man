using g3man.Models;
using Gtk;

namespace g3man.UI;

public class ManageProfileWindow : Window {
	private MainWindow owner;
	private Profile profile;
	private bool create;
	
	public ManageProfileWindow(MainWindow owner, Profile profile, bool create) {
		SetSizeRequest(350, 150);
		SetResizable(false);
		this.owner = owner;
		this.profile = profile;
		this.create = create;
	}

	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
	}
}