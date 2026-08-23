using System.CommandLine;
using System.Diagnostics;
using System.Text;
using g3man.Core;
using g3man.Core.Models;
using g3man.Core.Patching;
using g3man.Core.Util;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace g3man;

public static class CLI {
	public static int Invoke(string[] args, Logger.LoggerPipe pipe) {
		Logger logger = Logger.Make("", pipe);
		
		RootCommand root = new("This program can apply g3man mods or g3man profiles without using the graphical interface. It is mostly made for build system purposes.");
		Command applyCommand = new("apply") {
			Description = "Apply a g3man profile to a game"
		};
		root.Subcommands.Add(applyCommand);
		
		{
			Option<bool> launch = new("--launch", "-l") {
				Description = "Also launch the game",
				Required = false,
				Arity = ArgumentArity.Zero
			};
			applyCommand.Options.Add(launch);
			Option<string> steamExe = new("--steam", "-s") {
				Description = "Path to Steam's executable. Must be provided if -l is provided and the game is configured to launch through steam.",
				Required = false,
				Arity = ArgumentArity.ExactlyOne
			};
			applyCommand.Options.Add(steamExe);
			
			Option<FileInfo> gameJson = new("--game-json", "-gj") {
				Description = "Path to the game.json file",
				Required = true,
				Arity = ArgumentArity.ExactlyOne
			};
			applyCommand.Options.Add(gameJson);

			Option<DirectoryInfo> gameLocation = new("--game-folder", "-gf") {
				Description = "Path to the game's folder",
				Required = true,
				Arity = ArgumentArity.ExactlyOne
			};
			applyCommand.Options.Add(gameLocation);
			
			Option<FileInfo> profileJson = new("--profile-json", "-pj") {
				Description = "Path to the profile.json file",
				Required = true,
				Arity = ArgumentArity.ExactlyOne
			};
			applyCommand.Options.Add(profileJson);

			
			Option<DirectoryInfo> modsLocation = new("--mods-folder", "-pf") {
				Description = "Path to the mods folder",
				Required = true,
				Arity = ArgumentArity.ExactlyOne
			};
			applyCommand.Options.Add(modsLocation);
			
			Option<FileInfo> cleanDataLocation = new("--clean_data", "-d") {
				Description = "Path to the clean datafile of the game",
				Required = true,
				Arity = ArgumentArity.ExactlyOne
			};
			applyCommand.Options.Add(cleanDataLocation);
			
	  
			applyCommand.SetAction(parseResult => {
				FileInfo gameJsonInfo = parseResult.GetRequiredValue(gameJson);
				DirectoryInfo gameDirectoryInfo = parseResult.GetRequiredValue(gameLocation);
				
				FileInfo profileJsonInfo = parseResult.GetRequiredValue(profileJson);
				DirectoryInfo modsDirectoryInfo = parseResult.GetRequiredValue(modsLocation);

				bool shouldLaunch = parseResult.GetValue(launch);
				string? steamPath = parseResult.GetValue(steamExe);
				
				GameEntry entry = new GameEntry(gameDirectoryInfo.FullName, "");
				Game game; 
				try {
					using FileStream stream = new FileStream(gameJsonInfo.FullName, FileMode.Open, FileAccess.Read);
					game = Game.Parse(stream, entry);
				}
				catch (Exception e) {
					logger.Error($"Failed to parse game:\n{e.Message}");
					return 1;
				}
				
				string profilePath = modsDirectoryInfo.FullName;
		
				logger.Info("Parsing profile and mods...");

				Profile profile;
				try {
					using FileStream stream = new FileStream(profileJsonInfo.FullName, FileMode.Open, FileAccess.Read);
					profile = Profile.Parse(stream, "");
				}
				catch (Exception e) {
					logger.Error($"Failed to parse profile:\n{e.Message}");
					return 1;
				}

				string profileLivePath = game.GetProfileLiveFolderPath(profile);  
				
				bool anyFailed = false;
				List<Mod> mods = Mod.ParseAll(modsDirectoryInfo.FullName, (e, path) => {
					logger.Info($"Mod at {path} failed to parse:\n{e.Message}");
					Interlocked.Exchange(ref anyFailed, true);
				}).Where(mod => !profile.ModsDisabled.Contains(mod.ModId)).ToList();

				if (anyFailed)
					return 1;
				
				if (mods.Count == 0) {
					logger.Info("No mods found");
					return 1;
				}

				string[] missingIds = mods.Select(mod => mod.ModId).Where(id => !profile.ModOrder.Contains(id)).ToArray();
				missingIds.Sort();
				
				string[] modOrder = profile.ModOrder.Concat(missingIds).ToArray();
				
				mods.Sort((mod1, mod2) => int.Sign(Array.IndexOf(modOrder, mod1.ModId) - Array.IndexOf(modOrder, mod2.ModId)));

				FileInfo cleanData = parseResult.GetRequiredValue(cleanDataLocation);
				logger.Info("Loading datafile...");
				UndertaleData data;
				try {
					using FileStream stream = new(cleanData.FullName, FileMode.Open, FileAccess.Read);
					data = UndertaleIO.Read(stream);
				}
				catch (Exception e) {
					logger.Error(e);
					return 1;
				}

				int vanillaAudioGroupsCount = data.AudioGroups.Count;
				
				IO.CreateLiveFolder(profilePath, profileLivePath);
				IO.CreateXdeltaFoldersAndApply(gameDirectoryInfo.FullName, profilePath, profileLivePath, mods);
				
				string relativeProfileLivePath = $"g3man/live/{profile.ID}";
				string relativeProfilePath = $"g3man/live/{profile.ID}/profile";
									
				DatafilePatcher datafilePatcher = new(message => {
					logger.Info(message);
				});
				DatafilePatcher.PatchProduct output;
				try {
					output = datafilePatcher.Patch(mods, profile,
						modsDirectoryInfo.FullName, relativeProfilePath, relativeProfileLivePath, data,
						allowModScripting: true);
				}
				catch (DatafilePatcher.PatcherException e) {
					logger.Error($"{e}");
					return 1;
				}
				catch (Exception e) {
					logger.Error($"Unhandled error while patching: {e}");
					return 1;
				}
				
				data = output.Data;
				bool createOldSymlink = mods.Any(m => m.CreateOldProfileSymlink);
				if (createOldSymlink)
					IO.CreateLegacySymlink(game.Directory, game.GetProfileFolderPath(profile));
				logger.Info("Writing...");
				
				try {
					IO.Apply(data, vanillaAudioGroupsCount, output.AudioGroupTransfers, game, profile, modsDirectoryInfo.FullName);
				}
				catch (Exception e) {
					logger.Error("Failed to save output");
					logger.Error(e.ToString());
				}

				if (shouldLaunch) {
					Config config = new();
					if (game.ChosenExecutableType == Game.ExecutableType.Steam) {
						if (steamPath is null) {
							logger.Error(
								"Game is set to launch through Steam, but no Steam path was provided!");
							return 1;
						}
						config.SteamExecutable = steamPath;
					}
				
					logger.Info("Launching game...");
					try {
						Process? p = game.Launch(config, profile);
						if (p is null) {
							logger.Error("Failed to launch game");
							return 1;
						}
						logger.Info("Launched, waiting for process to close...");
						p.WaitForExit();
					}
					catch (Exception e) {
						logger.Error($"Error occurred while trying to launch game:\n{e}");
					}
				}
				
				return 0;
			}); 
		}

		ParseResult result = root.Parse(args);
		return result.Invoke();
	}
}