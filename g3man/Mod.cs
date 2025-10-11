using System.Text.Json;


namespace g3man;

public class Mod {
    public string ModId;
    public string DisplayName;
    public string Description;
    public string Version;
    public string TargetGameVersion;
    public string TargetPatcherVersion;
    public string PatchesPath;
    public string DatafilePath;
    public string PostMergeScriptPath;
    public string[] Credits;
    
    public RelatedMod[] Depends;
    public RelatedMod[] Breaks;
    
    /*
    public Mod(string modId, string displayName, string description, string[] credits, string version, string targetGameVersion, 
    string targetPatcherVersion, string patchesPath, string datafilePath,  RelatedMod[] depends, RelatedMod[] breaks) {
        ModId = modId;
        DisplayName = displayName;
        Description = description;
        Credits = credits;
        Version = version;
        TargetGameVersion = targetGameVersion;
        TargetPatcherVersion = targetPatcherVersion;
        PatchesPath = patchesPath;
        DatafilePath = datafilePath;
        
        Depends = depends;
        Breaks = breaks;
    }
    */

    private Mod(JsonElement root)
    {
        ModId = root.GetProperty("mod_id").GetString()!;
        DisplayName = root.GetProperty("display_name").GetString()!;
        Description = root.GetProperty("description").GetString()!;
        Credits = root.GetProperty("credits").EnumerateArray().ToArray().Select(x => x.GetString()!).ToArray();
        Version = root.GetProperty("version").GetString()!;
        TargetGameVersion = root.GetProperty("target_game_version").GetString()!;
        TargetPatcherVersion = root.GetProperty("target_patcher_version").GetString()!;
        PatchesPath = root.GetProperty("patches_path").GetString()!;
        DatafilePath = root.GetProperty("datafile_path").GetString()!;
        PostMergeScriptPath = root.GetProperty("post_merge_script_path").GetString()!;
        
        Depends = root.GetProperty("depends").EnumerateArray().ToArray()
            .Select(x => new RelatedMod(x)).ToArray();
        Breaks = root.GetProperty("breaks").EnumerateArray().ToArray()
            .Select(x => new RelatedMod(x)).ToArray();
    }
    
    
    public static List<Mod> ParseMods(string directory)
    {
        List<Mod> mods = new List<Mod>();
        string[] modFolders = Directory.GetDirectories(directory);
        foreach (string modFolder in modFolders)
        {
            string fullPath = Path.Combine(modFolder, "mod.json");
            string text = File.ReadAllText(fullPath);
            JsonDocument jsonDoc = JsonDocument.Parse(text);
            Mod mod;

            void OnError(Exception e) => Console.WriteLine("Invalid mod.json at " + fullPath + ":\n" + e.ToString());

            try {
                mod = new Mod(jsonDoc.RootElement);
                mods.Add(mod);
            }
            catch (KeyNotFoundException e) { OnError(e); }
            catch (InvalidOperationException e) { OnError(e); }
            catch (InvalidOrderRequirementException e) { OnError(e); }
        }

        return mods;
    }
}



public class RelatedMod {
    public string ModId;
    public string Version;
    public OrderRequirement OrderRequirement;

    public RelatedMod(JsonElement root)
    {
        ModId = root.GetProperty("mod_id").GetString()!;
        Version = root.GetProperty("version").GetString()!;
        string orderRequirement = root.GetProperty("order_requirement").GetString()!;
        OrderRequirement = orderRequirement switch
        {
            "before_us" => OrderRequirement.BeforeUs,
            "after_us" => OrderRequirement.AfterUs,
            "irrelevant" => OrderRequirement.Irrelevant,
            _ => throw new InvalidOrderRequirementException("Invalid order requirement: " + orderRequirement +
                                                   "\nOrder requirements can be \"before_us\", \"after_us\", or \"irrelevant\".")
        };
    }
}

public enum OrderRequirement {
    BeforeUs,
    AfterUs,
    Irrelevant
}
public class InvalidOrderRequirementException(string message) : Exception(message);