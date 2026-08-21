using System.Diagnostics;
using System.IO.Compression;
using g3man.Core.Models;
using Gtk;

namespace g3man.GTK;

public class UnzipperWindow : G3manWindow {
	private Button doneButton;
	
	private Stack stack;
	private Box questionBox;
	private Box mainBox;
	private Label questionLabel;
	private Label mainLabel;
	private Box optionsBox;

	public enum ZipType {
		Mod,
		Profile
	}

	
	private ZipType type;
	private string basePath;

	private class QuestionLock(int choice) {
		public int Choice = choice;
	}

	private QuestionLock Lock = new QuestionLock(0);
	
	public UnzipperWindow(ZipType type) {
		SetSizeRequest(500, 300);
		Box box = Box.New(Orientation.Vertical, 5);
		
		stack = new Stack();
		box.SetHalign(Align.Center);
		box.SetValign(Align.Center);

		mainBox = Box.New(Orientation.Vertical, 30);
		mainLabel = Label.New("Extracting...");
		mainLabel.SetWrap(true);
		mainBox.Append(mainLabel);
		
		doneButton = Button.NewWithLabel("Done");
		doneButton.SetSensitive(false);
		doneButton.OnClicked += (_, _) => {
			Close();
		};
		mainBox.Append(doneButton);

		doneButton.SetHalign(Align.Center);
		
		questionBox = Box.New(Orientation.Vertical, 30);
		
		questionLabel = Label.New("");
		questionLabel.SetWrap(true);

		
		questionBox.Append(questionLabel);

		optionsBox = Box.New(Orientation.Horizontal, 5);
		Button yesToAll = Button.NewWithLabel("Yes to all");
		Button yes = Button.NewWithLabel("Yes");
		Button no = Button.NewWithLabel("No");
		Button cancel = Button.NewWithLabel("Cancel");
		Button[] options = [yesToAll, yes, no, cancel];
		for (int i = 0; i < options.Length; i++) {
			optionsBox.Append(options[i]);
			options[i].OnClicked += (button, _) => {
				lock (Lock) {
					Lock.Choice = options.IndexOf(button);
					optionsBox.SetSensitive(false);
					Monitor.PulseAll(Lock);
				}
			};
		}
		optionsBox.SetHalign(Align.Center);
		questionBox.Append(optionsBox);
		
		box.Append(stack);
		stack.AddChild(mainBox);
		stack.AddChild(questionBox);
		stack.SetVisibleChild(mainBox);
		
		SetChild(box);

		this.type = type;
		if (type == ZipType.Mod) {
			basePath = UI.CurrentProfileFolderPath();
		}
		else {
			basePath = Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles");
		}
		
		
	}
	private void Ask(string text) {
		UI.RunOnMainThreadEventually(() => {
			stack.SetVisibleChild(questionBox);
			questionLabel.SetText(text);
			optionsBox.SetSensitive(true);
		});
	}
	
	private void ShowTextAndLeave(string text) {
		UI.RunOnMainThreadEventually(() => {
			stack.SetVisibleChild(mainBox);
			mainLabel.SetText(text);
			doneButton.SetSensitive(true);
		});
	}

	private static string GetDirectoryNameWithSlash(string path) {
		string? directoryName = Path.GetDirectoryName(path);
		if (directoryName is null)
			return "";
		return $"{directoryName}/";
	}
	
	public ZipArchiveEntry[] ReadZipJsonEntries(ZipArchive archive, Gio.File file) {
		ZipArchiveEntry[] profileJsonEntries = archive.Entries.Where(entry => entry.FullName.EndsWith("/profile.json") || entry.FullName == "profile.json").ToArray();
		ZipArchiveEntry[] modJsonEntries = archive.Entries.Where(entry => entry.FullName.EndsWith("/mod.json") || entry.FullName == "mod.json").ToArray();
		
		ZipArchiveEntry[] filterSubentries(ZipArchiveEntry[] entries) {
			return entries.Where(entry => entries.Count(entry2 => entry2 != entry 
				&& entry.FullName.StartsWith(GetDirectoryNameWithSlash(entry2.FullName))) == 0).ToArray();
				
		}
		// filter out mod/profile.jsons who are contained inside folders of other ones
		modJsonEntries = filterSubentries(modJsonEntries);
		profileJsonEntries = filterSubentries(profileJsonEntries);

		ZipArchiveEntry[] jsonEntries;
		if (type == ZipType.Mod) {
			jsonEntries = modJsonEntries;
			if (profileJsonEntries.Length != 0) {
				ShowTextAndLeave("This is a profile zip. You should install it as a profile in the profiles tab.");
				return [];
			}
			if (modJsonEntries.Length == 0) {
				ZipArchiveEntry[] xdeltaEntries = archive.Entries.Where(entry => entry.FullName.EndsWith(".xdelta")).ToArray();
				if (xdeltaEntries.Length == 1) {
					return xdeltaEntries;
				}
				if (xdeltaEntries.Length > 1)
					ShowTextAndLeave("Found several .xdelta files in this zip, but don't know which one to apply. Please extract them manually, and install the one you want.");
				else
					ShowTextAndLeave("No mod folders found in this zip.");
				return [];
			}
		}
		else {
			jsonEntries = profileJsonEntries;
			if (profileJsonEntries.Length == 0) {
				string message;
				if (modJsonEntries.Length == 0) {
					message = "This zip contains no profiles and no mods. Did you select the right file?";
				}
				else {
					string has = (modJsonEntries.Length == 1) ? "a mod" : "a collection of mods";
					message = $"This zip contains no profiles, but it does have {has}. Try installing it in the mods tab.";
				}
				ShowTextAndLeave(message);
				return [];
			}
		}
		return jsonEntries;
	}
	
	

	public ZipArchiveEntry[] AskQuestions(ZipArchiveEntry[] entries, Gio.File file) {
		bool[] answers = new bool[entries.Length];
		for (int i = 0; i < entries.Length; i++) {
			ZipArchiveEntry jsonEntry = entries[i];
			string precedingPath = Path.GetDirectoryName(jsonEntry.FullName) ?? "";
			string folderName = 
				precedingPath != "" ? Path.GetFileName(precedingPath)
					: Path.GetFileNameWithoutExtension(file.GetBasename()!);
			string folder = Path.Combine(basePath, folderName);
			if (!Directory.Exists(folder)) {
				answers[i] = true;
			}
			else {
				answers[i] = false;
			}
		}

		int count = answers.Count(b => !b);
		int asked = 1;
		for (int i = 0; i < entries.Length; i++) {
			ZipArchiveEntry jsonEntry = entries[i];
			string precedingPath = Path.GetDirectoryName(jsonEntry.FullName) ?? "";
			string folderName = 
				precedingPath != "" ? Path.GetFileName(precedingPath)
					: Path.GetFileNameWithoutExtension(file.GetBasename()!);
			if (!answers[i]) {
				lock (Lock) {
					if (type == ZipType.Mod) {
						Mod mod;
						{
							using Stream s = jsonEntry.Open();
							mod = Mod.Parse(s);
						}
						Ask($"Mod {mod.Identify()} already exists - overwrite it? ({asked}/{count})");
						asked++;
					}
					if (type == ZipType.Profile) {
						Profile profile;
						{
							using Stream s = jsonEntry.Open();
							profile = Profile.Parse(s, folderName);
						}
						Ask($"Profile {profile.Identify()} already exists - overwrite it? ({asked}/{count})");
						asked++;
					}
					Monitor.Wait(Lock);
				}

				if (Lock.Choice == 0) {
					for (int j = i; j < entries.Length; j++) {
						answers[j] = true;
					}
					break;
				}

				if (Lock.Choice == 1) {
					answers[i] = true;
					continue;
				}
				if (Lock.Choice == 2) {
					continue;
				}
				if (Lock.Choice == 3) {
					return [];
				}
			}
		}
		return entries.Where((_, i) => answers[i]).ToArray();
	}

	public void TryExtractingZip(Gio.File file, ZipArchive archive, ZipArchiveEntry[] jsonEntries) {
		foreach (ZipArchiveEntry jsonEntry in jsonEntries) {
			if (jsonEntry.FullName.EndsWith(".xdelta")) {
				jsonEntry.ExtractToFile($"{basePath}/{jsonEntry.Name}");
				continue;
			}
			string precedingPath = Path.GetDirectoryName(jsonEntry.FullName) ?? "";
			string folderName = 
				precedingPath != "" ? Path.GetFileName(precedingPath)
				: Path.GetFileNameWithoutExtension(file.GetBasename()!);
			string folder = Path.Combine(basePath, folderName);
			if (Directory.Exists(folder))
				Directory.Delete(folder, true);
			Directory.CreateDirectory(folder);

			Dictionary<bool, ZipArchiveEntry[]> groups = archive.Entries
				.Where(entry => entry.FullName.StartsWith($"{precedingPath}/") && entry.FullName != precedingPath)
				.GroupBy(entry => entry.FullName.EndsWith('/'))
				.ToDictionary(group => group.Key, group => group.ToArray());

			// we are going to ignore "foldermates". they don't show up on all platforms, and we know
			// which folders files need from their path anyway
			//ZipArchiveEntry[] foldermates = groups.GetValueOrDefault(true, []);
			
			ZipArchiveEntry[] filemates = groups.GetValueOrDefault(false, []);
			
			int precedingPathLength = precedingPath == "" ? 0 : precedingPath.Length + 1; // one more for trailing slash
			foreach (ZipArchiveEntry filemate in filemates) {
				string relativePath = filemate.FullName.Remove(0, precedingPathLength);
				string? relativeDirectory = Path.GetDirectoryName(relativePath);
				if (relativeDirectory is not null)
					Directory.CreateDirectory(Path.Combine(folder, relativeDirectory));
				filemate.ExtractToFile(Path.Combine(folder, relativePath), true);
			}
		}
		
	}

	public void Dialog(Window window, Gio.File file, Action successCallback) {
		SetTransientFor(window);
		SetModal(true);
		Present();

		Thread t = new(() => {
			try {
				using ZipArchive archive = ZipFile.OpenRead(file.GetPath()!);

				ZipArchiveEntry[] entries = ReadZipJsonEntries(archive, file);
				if (entries.Length == 0) {
					return;
				}
				entries = AskQuestions(entries, file);
				if (entries.Length == 0) {
					UI.RunOnMainThreadEventually(Close);
					return;
				}
				
				UI.RunOnMainThreadEventually(() => {
					stack.SetVisibleChild(mainBox);
					mainLabel.SetText("Extracting...");
				});
				TryExtractingZip(file, archive, entries);
				UI.RunOnMainThreadEventually(() => {
					Close();
					successCallback();
				});
			}
			catch (Exception e) {
				UI.Logger.Error(e);
				ShowTextAndLeave("Failed to import from ZIP (error logged). This file might not be a ZIP file.");
			}
		});
		t.Start();
	}
}