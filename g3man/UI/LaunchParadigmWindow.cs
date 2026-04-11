using Gtk;

namespace g3man.UI;

public class LaunchParadigmWindow : G3manWindow {

	public static Widget CreateLaunchParadigmWidgets(bool showRegretLabel, Action<LaunchParadigm?> callback) { 
		Label questionLabel = Label.New("Choose your paradigm:");
		questionLabel.SetJustify(Justification.Center);
		questionLabel.SetMargin(10);
		questionLabel.SetValign(Align.Start);
		
		Label modifyParadigmLabel =
			Label.New("I want g3man to modify this game's files directly, so I can launch this game by any means (Recommended for split games like DELTARUNE)");
		modifyParadigmLabel.SetWrap(true);
		modifyParadigmLabel.SetJustify(Justification.Center);
		Button modifyParadigmButton = Button.NewWithLabel("Select");
		modifyParadigmButton.OnClicked += (_, _) => {
			callback(LaunchParadigm.Modify);
		};
		
		Label launchParadigmLabel =
			Label.New("I want to launch this game through g3man's interface, so this game's files won't be overwriten");
		launchParadigmLabel.SetWrap(true);
		launchParadigmLabel.SetJustify(Justification.Center);
		Button launchParadigmButton = Button.NewWithLabel("Select");
		launchParadigmButton.OnClicked += (_, _) => {
			callback(LaunchParadigm.Launch);
		};
		
		launchParadigmLabel.SetAlign(Align.Center);
		launchParadigmButton.SetAlign(Align.Center);
		Box launchParadigm = Box.New(Orientation.Vertical, 10)
			.With(launchParadigmLabel, launchParadigmButton);
		
		modifyParadigmLabel.SetAlign(Align.Center);
		modifyParadigmButton.SetAlign(Align.Center);
		Box modifyParadigm = Box.New(Orientation.Vertical, 10)
			.With(modifyParadigmLabel, modifyParadigmButton);

		Box paradigmBox = Box.New(Orientation.Horizontal, 60).With(launchParadigm, modifyParadigm);
		paradigmBox.SetHomogeneous(true);
		
		//OnCloseRequest += (_, _) => true;
		/* spammed a ton of warnings. would've been ideal, because the buttons line up. don't know how to fix.
		Grid paradigmGrid = Grid.New();
		paradigmGrid.Attach(launchParadigmLabel, 0, 0, 1, 1);
		paradigmGrid.AttachNextTo(modifyParadigmLabel, launchParadigmLabel,PositionType.Right, 1, 1);
		
		paradigmGrid.AttachNextTo(launchParadigmButton, launchParadigmLabel, PositionType.Bottom, 1, 1);
		paradigmGrid.AttachNextTo(modifyParadigmButton, modifyParadigmLabel,PositionType.Bottom, 1, 1);
		
		paradigmGrid.SetRowHomogeneous(true);
		paradigmGrid.SetColumnHomogeneous(true);
		paradigmGrid.SetColumnSpacing(60);
		paradigmGrid.SetRowSpacing(15);
		*/



		Box box = Box.New(Orientation.Vertical, 30);
		box.Append(questionLabel);
		box.Append(paradigmBox);
		
		if (showRegretLabel) {
			Label regretLabel =
				Label.New("Should you regret your choice, you can change this later!");
			regretLabel.SetWrap(true);
			regretLabel.SetMargin(15);
			
			box.Append(regretLabel);
		}
		
		box.SetAlign(Align.Center);
		box.SetVexpand(true);
		
		return box;
	}
	
	public LaunchParadigmWindow(bool showRegretLabel, Action<LaunchParadigm?> callback) { 
		SetDefaultSize(600, 400);
		Widget widget = CreateLaunchParadigmWidgets(showRegretLabel, choice => {
			callback(choice);
			Close();
		});
		OnCloseRequest += (_, _) => {
			callback(null);
			return false;
		};
		SetChild(widget);
	}
	public void Dialog(Window window) {
		SetTransientFor(window);
		SetModal(true);
		Present();
	}
}