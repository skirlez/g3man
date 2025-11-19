using System.CommandLine;
using g3man.Models;
using g3man.Patching;
using UndertaleModLib;

namespace g3man;

public class CLI {
    public static int Invoke(string[] args) {
        RootCommand root = new RootCommand("This program can apply g3man mods or g3man profiles without using the graphical interface. It is mostly made for build system purposes.");

        Command applyCommand = new Command("apply");
        applyCommand.Description = "Apply g3man mods or profiles";
        root.Subcommands.Add(applyCommand);

        {
            Command applyProfileCommand = new Command("profile");
            applyProfileCommand.Description = "Applies a profile to a datafile and saves the resulting new datafile";
            applyCommand.Subcommands.Add(applyProfileCommand);

            Option<DirectoryInfo> profileLocation = new Option<DirectoryInfo>("--path", "-p");
            profileLocation.Description = "Path to the profile folder containing profile.json";
            profileLocation.Required = true;
            profileLocation.Arity = ArgumentArity.ExactlyOne;
            applyProfileCommand.Options.Add(profileLocation);

            
            Option<FileInfo> datafileLocation = new Option<FileInfo>("--datafile", "-d");
            datafileLocation.Description = "Path to the game's clean datafile";
            datafileLocation.Required = true;
            datafileLocation.Arity = ArgumentArity.ExactlyOne;
            applyProfileCommand.Options.Add(datafileLocation);

            
            Option<FileInfo> outLocation = new Option<FileInfo>("--out", "-o");
            outLocation.Description = "Path to where the output datafile should be saved";
            outLocation.Required = true;
            outLocation.Arity = ArgumentArity.ExactlyOne;
            applyProfileCommand.Options.Add(outLocation);
      
			applyProfileCommand.SetAction(parseResult => {
                DirectoryInfo profileDirectoryInfo = parseResult.GetRequiredValue(profileLocation)!;
                Console.WriteLine("Parsing profile and mods...");
                Profile? profile = Profile.Parse(profileDirectoryInfo.FullName);
                if (profile == null) {
                    return 1;
                }

                List<Mod> mods = Mod.Parse(Path.Combine(profileDirectoryInfo.FullName, "mods"));
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
                
                
                FileInfo outFileInfo = parseResult.GetRequiredValue(outLocation);
                
                Patcher patcher = new Patcher();
                patcher.Patch(mods, profile, profileDirectoryInfo.Parent!.FullName, data, outFileInfo.FullName,
                    (status, leave) => {
                        Console.WriteLine(status);        
                    }
                );
                
                return 0;
            }); 
        }

        ParseResult result = root.Parse(args);
        return result.Invoke();
    }
}