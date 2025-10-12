using System.Collections.Concurrent;
using System.Text.Json;
using g3man.Util;

namespace g3man.Models;

public class Mod {
	public static Logger logger = new Logger("MOD-PARSER");
	
	public string ModId;
	public string DisplayName;
	public string Description;
	public string IconPath;
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


	


	private Mod(JsonElement root) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		Description = JsonUtil.GetStringOrThrow(root, "description");
		IconPath = JsonUtil.GetStringOrThrow(root, "icon_path");
		Credits = JsonUtil.GetStringArrayOrThrow(root, "credits");
		Version = JsonUtil.GetStringOrThrow(root, "version");
		TargetGameVersion = JsonUtil.GetStringOrThrow(root, "target_game_version");
		TargetPatcherVersion = JsonUtil.GetStringOrThrow(root, "target_patcher_version");
		PatchesPath = JsonUtil.GetStringOrThrow(root, "patches_path");
		DatafilePath = JsonUtil.GetStringOrThrow(root, "datafile_path");
		PostMergeScriptPath = JsonUtil.GetStringOrThrow(root, "post_merge_script_path");
		
		Depends = JsonUtil.GetObjectArrayOrThrow(root, "depends")
			.Select(x => new RelatedMod(x)).ToArray();
		Breaks = JsonUtil.GetObjectArrayOrThrow(root, "breaks")
			.Select(x => new RelatedMod(x)).ToArray();
	}
	
	
	public static List<Mod> ParseMods(string directory) {
		ConcurrentBag<Mod> mods = new ConcurrentBag<Mod>();
		string[] modFolders;
		try {
			modFolders = Directory.GetDirectories(directory);
		}
		catch (Exception e) {
			// ignored
			return [];
		}

		Parallel.ForEach(modFolders, modFolder => {
			string fullPath = Path.Combine(modFolder, "mod.json");
			string text;
			try {
				text = File.ReadAllText(fullPath);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load mod.json at " + fullPath + ":\n" + e.Message);
				return;
			}


			JsonDocument jsonDoc = JsonDocument.Parse(text);

			try {
				Mod mod = new Mod(jsonDoc.RootElement);
				mods.Add(mod);
			}
			catch (InvalidDataException e) {
				logger.Error("Invalid mod.json at " + fullPath + ":\n" + e.Message);
			}
		});


		return mods.ToList();
	}
}



public class RelatedMod {
	public string ModId;
	public string Version;
	public OrderRequirement OrderRequirement;

	public RelatedMod(JsonElement root) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		Version = JsonUtil.GetStringOrThrow(root, "version");
		string orderRequirement = JsonUtil.GetStringOrThrow(root, "order_requirement");
		OrderRequirement = orderRequirement switch {
			"before_us" => OrderRequirement.BeforeUs,
			"after_us" => OrderRequirement.AfterUs,
			"irrelevant" => OrderRequirement.Irrelevant,
			_ => throw new InvalidOrderRequirementException("Invalid order requirement: " + orderRequirement
					+ "\nOrder requirements can be \"before_us\", \"after_us\", or \"irrelevant\".")
		};
	}
}

public enum OrderRequirement {
	BeforeUs,
	AfterUs,
	Irrelevant
}
public class InvalidOrderRequirementException(string message) : Exception(message);