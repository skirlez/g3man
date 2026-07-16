using System.Diagnostics;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;

namespace g3man.GTK.MainUI;

public partial class MainWindow {


	private void SetupLogsPage(Box page) {

		Label logsLabel = Label.New($"Current log: {UI.LogFilename}");
		logsLabel.SetMargin(10);
		
		Debug.Assert(UI.GtkLogWriter is not null);
		TextView view = TextView.NewWithBuffer(UI.GtkLogWriter.GetBuffer());
		view.SetWrapMode(WrapMode.WordChar);
		view.SetLeftMargin(5);
		view.SetTopMargin(3);
		view.SetRightMargin(5);
		view.SetBottomMargin(3);
		
		view.SetEditable(false);
		view.SetMonospace(true);
		
		ScrolledWindow logWindow = ScrolledWindow.New();
		logWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
		logWindow.SetVexpand(true);
		logWindow.SetChild(view);
		logWindow.SetMargin(10);
		logWindow.SetHasFrame(true);
		
		/*
		// if you're at the bottom of the log, it should stay there if new lines come in
		
		// ... unfortunately doesn't work because there's seemingly no (working) way to handle
		// all ways to move the scrollbar.
		
		// checking for if the value of the scrollbar moves away from the bottom is not sufficient
		// seems to cause it to always think it's moved away from the bottom *after* we try to set it to 
		// the new bottom when the scrollbar's upper bound is changed. I don't know.
		
		Adjustment scrollbar = logWindow.GetVadjustment();
		bool stuckToBottom = true;
		logWindow.OnEdgeReached += (sender, args) => {
		   	if (!stuckToBottom && args.Pos == PositionType.Bottom) {
		   		Console.WriteLine(args.Pos);
		   		stuckToBottom = true;
		   	}
		};
		Adjustment.UpperPropertyDefinition.Notify(
			sender: scrollbar,
			signalHandler: (_, _) => {
				if (stuckToBottom) {
					scrollbar.SetValue(scrollbar.GetUpper() - scrollbar.GetPageSize());
				}
			}
		);
		*/
		
		
		

		Button openLogsFolderButton = Button.NewWithLabel("Open logs folder");
		openLogsFolderButton.SetMargin(10);
		openLogsFolderButton.SetHalign(Align.Center);
		openLogsFolderButton.OnClicked += (_, _) => {
			IO.OpenFileExplorer(Path.Combine(ProgramPaths.GetDataDirectory(), "logs"));
		};
		
		page.Append(logsLabel);
		page.Append(logWindow);
		page.Append(openLogsFolderButton);
	}
	
}