using System.CommandLine;
using g3man.Models;
using g3man.Patching;
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
                Console.WriteLine("Parsing profile and mods...");
                Profile? profile = Profile.Parse(profileDirectoryInfo.FullName);
                if (profile == null) {
                    return 1;
                }

                List<Mod> mods = Mod.ParseAll(Path.Combine(profileDirectoryInfo.FullName));
                if (mods.Count == 0) {
                    return 1;
                }
                
                FileInfo dataFileInfo = parseResult.GetRequiredValue(datafileLocation);
                Console.WriteLine("Loading clean datafile...");
                UndertaleData data;
                try {
                    using FileStream stream = new FileStream(dataFileInfo.FullName, FileMode.Open, FileAccess.Read);
                    data = UndertaleIO.Read(stream);
                }
                catch (Exception e) {
                    Console.Error.WriteLine(e.ToString());
                    return 1;
                }
                
                
                DirectoryInfo outLocationInfo = parseResult.GetRequiredValue(outLocation);

                string datafileName = parseResult.GetValue(outName) ?? "data.win";
                
                Patcher patcher = new Patcher();
                UndertaleData? output = patcher.Patch(mods, profile, profileDirectoryInfo.FullName, data,
                    (status, leave) => {
                         
                    }
                );
                if (output == null)
                    return 1;
                try {
                    IO.Apply(data,  outLocationInfo.FullName, profileDirectoryInfo.FullName, datafileName);
                }
                catch (Exception e) {
                    Console.Error.WriteLine("Failed to save output data.win");
                    Console.Error.WriteLine(e.ToString());
                }

                return 0;
            }); 
        }

        ParseResult result = root.Parse(args);
        return result.Invoke();
    }
}