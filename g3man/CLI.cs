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
                Profile? profile = Profile.Parse(profileDirectoryInfo.FullName);
                if (profile == null) {
                    return 1;
                }

                List<Mod> mods = Mod.ParseAll(Path.Combine(profileDirectoryInfo.FullName));
                if (mods.Count == 0) {
                    return 1;
                }
                mods.Sort((mod1, mod2) => int.Sign(Array.IndexOf(profile.ModOrder, mod1.ModId) - Array.IndexOf(profile.ModOrder, mod2.ModId)));
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

                string datafileName = parseResult.GetValue(outName) ?? "data.win";
                
                Patcher patcher = new Patcher();
                UndertaleData? output = patcher.Patch(mods, profile, profileDirectoryInfo.FullName, data, Program.Logger, status => {});
                if (output == null)
                    return 1;
                try {
                    IO.Apply(data,  outLocationInfo.FullName, profileDirectoryInfo.FullName, datafileName);
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