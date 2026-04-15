using System.CommandLine;
using g3man.Models;
using g3man.Patching;
using g3man.Util;
using UndertaleModLib;

namespace g3man;

public class CLI {
	public static int Invoke(string[] args) {
		Program.Config = new Config();
		Program.Config.AllowModScripting = true;
		
		RootCommand root = new RootCommand("This program can apply g3man mods or g3man profiles without using the graphical interface. It is mostly made for build system purposes.");
		Command applyCommand = new Command("apply");
		applyCommand.Description = "Apply a g3man profile";
		root.Subcommands.Add(applyCommand);
		{
			Option<DirectoryInfo> profileLocation = new Option<DirectoryInfo>("--path", "-p");
			profileLocation.Description = "Path to the profile folder containing profile.json";
			profileLocation.Required = true;
			profileLocation.Arity = ArgumentArity.ExactlyOne;
			applyCommand.Options.Add(profileLocation);

			
			Option<FileInfo> datafileLocation = new Option<FileInfo>("--datafile", "-d");
			datafileLocation.Description = "Path to the game's clean datafile";
			datafileLocation.Required = true;
			datafileLocation.Arity = ArgumentArity.ExactlyOne;
			applyCommand.Options.Add(datafileLocation);

			
			Option<DirectoryInfo> outLocation = new Option<DirectoryInfo>("--out", "-o");
			outLocation.Description = "Directory where the output datafile should be saved";
			outLocation.Required = true;
			outLocation.Arity = ArgumentArity.ExactlyOne;
			applyCommand.Options.Add(outLocation);
			
			
			Option<String> outName = new Option<String>("--outname", "-n");
			outLocation.Description = "What name should the output datafile have";
			outLocation.Arity = ArgumentArity.ExactlyOne;
			applyCommand.Options.Add(outName);
	  
			applyCommand.SetAction(parseResult => {
				DirectoryInfo profileDirectoryInfo = parseResult.GetRequiredValue(profileLocation)!;
				Program.Logger.Info("Parsing profile and mods...");

				Profile profile;
				try {
					profile = Profile.Parse(profileDirectoryInfo.FullName, doFolderCheck: false);
				}
				catch (Exception e) {
					Program.Logger.Error($"Failed to parse profile:\n{e.Message}");
					return 1;
				}



				bool anyFailed = false;
				List<Mod> mods = Mod.ParseAll(profileDirectoryInfo.FullName, (e, path) => {
					Program.Logger.Info($"Mod at {path} failed to parse: {e.Message}");
					Interlocked.Exchange(ref anyFailed, true);
				}).Where(mod => !profile.ModsDisabled.Contains(mod.ModId)).ToList();

				if (anyFailed)
					return 1;
				
				if (mods.Count == 0) {
					Program.Logger.Info("No mods found");
					return 1;
				}

				string[] missingIds = mods.Select(mod => mod.ModId).Where(id => !profile.ModOrder.Contains(id)).ToArray();
				missingIds.Sort();
				
				string[] modOrder = profile.ModOrder.Concat(missingIds).ToArray();
				
				mods.Sort((mod1, mod2) => int.Sign(Array.IndexOf(modOrder, mod1.ModId) - Array.IndexOf(modOrder, mod2.ModId)));
				FileInfo dataFileInfo = parseResult.GetRequiredValue(datafileLocation);
				Program.Logger.Info("Loading clean datafile...");
				UndertaleData data;
				try {
					using FileStream stream = new FileStream(dataFileInfo.FullName, FileMode.Open, FileAccess.Read);
					data = UndertaleIO.Read(stream);
				}
				catch (Exception e) {
					Program.Logger.Error(e);
					return 1;
				}
				
				DirectoryInfo outLocationInfo = parseResult.GetRequiredValue(outLocation);

				string outputDatafileName = parseResult.GetValue(outName) ?? "data.win";

				string ulid = Ulid.NewUlid().ToString();
				string relativeProfilePath = $"g3man/links/{ulid}";
									
				DatafilePatcher datafilePatcher = new DatafilePatcher();
				UndertaleData? output = datafilePatcher.Patch(mods, profile, 
					profileDirectoryInfo.FullName, relativeProfilePath, data, Program.Logger, status => {});
				if (output == null)
					return 1;
				bool createOldSymlink = mods.Any(m => m.CreateOldProfileSymlink);
				Program.Logger.Info("Writing...");

				bool writeHash = (IO.DatafileRelativePaths.Contains(outputDatafileName));
				try {
					IO.Apply(data, outLocationInfo.FullName, profileDirectoryInfo.FullName, outputDatafileName, writeHash,
						createOldSymlink, ulid);
				}
				catch (Exception e) {
					Program.Logger.Error("Failed to save output data.win");
					Program.Logger.Error(e.ToString());
				}

				return 0;
			}); 
		}

		ParseResult result = root.Parse(args);
		return result.Invoke();
	}
}