using g3man.Util;
using Gtk;

namespace g3man.UI.Main;

public partial class MainWindow {


	private void SetupLogsPage(Box page) {

		Label logsLabel = Label.New($"Current log: {Program.LogFilename}");
		logsLabel.SetMargin(10);
		
		TextView view = TextView.NewWithBuffer(Program.GtkBufferLogfile!.GetBuffer());

		view.SetEditable(false);
		view.SetMonospace(true);
		
		ScrolledWindow logWindow = ScrolledWindow.New();
		logWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
		logWindow.SetVexpand(true);
		logWindow.SetChild(view);
		

		Frame frame = Frame.New(null);
		frame.Child = logWindow;
		frame.SetChild(logWindow);
		frame.SetMarginTop(5);
		frame.SetMarginStart(10);
		frame.SetMarginEnd(10);
		frame.SetMarginBottom(5);
	

		Button openLogsFolderButton = Button.NewWithLabel("Open logs folder");
		openLogsFolderButton.SetMargin(10);
		openLogsFolderButton.SetHalign(Align.Center);
		openLogsFolderButton.OnClicked += (_, _) => {
			IO.OpenFileExplorer(Path.Combine(ProgramPaths.GetDataDirectory(), "logs"));
		};
		
		page.Append(logsLabel);
		page.Append(frame);
		page.Append(openLogsFolderButton);
	}
	
}