using Gtk;

namespace g3man.UI.Main;

public partial class MainWindow {
	

	
	// Prevent user from sending too many requests. I don't know how common of a practice this
	// (maybe github can just handle the maximum requests a human can send by spam clicking)
	// but why not
	private long lastCheckedUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
	
	private void SetupAboutPage(Box page) {
		Label title = Label.New("");
		title.SetMarkup("<span size=\"large\">g3man</span>");
		title.SetSizeRequest(100, 20);
		Label subtitle = Label.New("");
		subtitle.SetMarkup("<b>G</b>ame<b>M</b>aker <b>M</b>od <b>Man</b>ager");
		Label versionLabel = Label.New($"Version {Program.Version}");
		
		
		Label updateFoundLabel = Label.New("");

		void setUpdateFoundText(int version) {
			updateFoundLabel.SetMarkup(
				$"You are on an outdated version!"
				+ $"\n(Latest is {version}, you are on {Program.Version})"
				+ $"\nYou may download it <a href=\"https://github.com/skirlez/g3man/releases/latest\">here</a>.");
		}

		setUpdateFoundText(Program.Version + 1);
		
		Label checkingUpdateLabel = Label.New("Checking for updates...");
		Label latestVersionLabel = Label.New("You are on the latest version.");
		Label futureVersionLabel = Label.New("You are from the future!\nOr just using bleeding edge.");
		Label errorLabel = Label.New("Could not check for updates.\nYou should probably check manually.");
		Label empty = Label.New("");
		
		// We're using a stack here so it scales up to the size of the largest text (so the UI doesn't move around when the text updates)
		Stack updateStatusStack = new Stack();
		updateStatusStack.AddChild(updateFoundLabel);
		updateStatusStack.AddChild(checkingUpdateLabel);
		updateStatusStack.AddChild(latestVersionLabel);
		updateStatusStack.AddChild(futureVersionLabel);
		updateStatusStack.AddChild(errorLabel);
		updateStatusStack.AddChild(empty);
		updateStatusStack.SetVisibleChild(empty);
		
		Widget? child = updateStatusStack.GetFirstChild()!;
		do {
			((Label)child).SetJustify(Justification.Center);
			child = child.GetNextSibling();
		} while (child != null);
		
		UpdateChecker checker = new UpdateChecker(() => {
			updateStatusStack.SetVisibleChild(checkingUpdateLabel);
		}, 
		(int version) => {
			Program.RunOnMainThreadEventually(() => {
				if (version == 0) {
					updateStatusStack.SetVisibleChild(errorLabel);
					
				}
				else if (version > Program.Version) {
					setUpdateFoundText(version);
					updateStatusStack.SetVisibleChild(updateFoundLabel);
					AddExclamationToAbout();
				}
				else if (version == Program.Version) {
					updateStatusStack.SetVisibleChild(latestVersionLabel);
				}
				else {
					updateStatusStack.SetVisibleChild(futureVersionLabel);
				}
			});
		});
		
		Button checkForUpdatesButton = Button.NewWithLabel("Check for Updates");
		checkForUpdatesButton.SetHalign(Align.Center);
		checkForUpdatesButton.OnClicked += (_, _) => {
			// user could rollback their system clock (probably), so do this in absolute value
			if (Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastCheckedUpdate) < 1000)
				return;
			lastCheckedUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
			checker.Check();
		};
		if (Program.Config.CheckForUpdates)
			checker.Check();
		
		Label license = Label.New("Licensed under the terms of the AGPLv3,\ng3man is Free Software (with Free as in Freedom)");
		license.SetMarginTop(20);
		license.SetJustify(Justification.Center);

		
		Label source = Label.New("");
		source.SetMargin(10);
		source.SetMarkup("<a href=\"https://github.com/skirlez/g3man\">GitHub Repository</a>");
		
		
		Box updateBox = Box.New(Orientation.Vertical, 5);
		updateBox.Append(updateStatusStack);
		updateBox.Append(checkForUpdatesButton);
		updateBox.SetMarginTop(40);
		
		page.Append(title);
		page.Append(subtitle);
		page.Append(versionLabel);
		page.Append(license);
		page.Append(source);
		
		page.Append(Separator.New(Orientation.Horizontal));
		page.Append(updateBox);

		page.SetHalign(Align.Center);
		page.SetValign(Align.Center);
		
	}
	
	private void AddExclamationToAbout() {
		aboutButtonLabelStack.SetVisibleChild(aboutButtonLabelWithUpdate);
	}
}