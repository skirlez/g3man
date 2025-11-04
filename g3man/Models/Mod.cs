using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using g3man.Util;

namespace g3man.Models;

public class Mod {
	public static Logger logger = new Logger("MOD-PARSER");
	
	public string ModId;
	public string DisplayName;
	public string Description;
	public string IconPath;
	public SemVer Version;
	public string TargetGameVersion;
	public string TargetPatcherVersion;
	public PatchLocation[] Patches;
	public string DatafilePath;
	public string PostMergeScriptPath;
	public string[] Credits;
	public string[] Links;
	
	public RelatedMod[] Depends;
	public RelatedMod[] Breaks;

	public string FolderName;
	
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

	
	private Mod(JsonElement root, string folderName) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		Description = JsonUtil.GetStringOrThrow(root, "description");
		IconPath = JsonUtil.GetStringOrThrow(root, "icon_path");
		Credits = JsonUtil.GetStringArrayOrThrow(root, "credits");
		Links = JsonUtil.GetStringArrayOrThrow(root, "links");
		Version = new SemVer(JsonUtil.GetStringOrThrow(root, "version"), false);
		TargetGameVersion = JsonUtil.GetStringOrThrow(root, "target_game_version");
		TargetPatcherVersion = JsonUtil.GetStringOrThrow(root, "target_patcher_version");
		Patches = JsonUtil.GetObjectArrayOrThrow(root, "patches")
			.Select(x => new PatchLocation(x)).ToArray();
		DatafilePath = JsonUtil.GetStringOrThrow(root, "datafile_path");
		PostMergeScriptPath = JsonUtil.GetStringOrThrow(root, "post_merge_script_path");
		
		Depends = JsonUtil.GetObjectArrayOrThrow(root, "depends")
			.Select(x => new RelatedMod(x)).ToArray();
		Breaks = JsonUtil.GetObjectArrayOrThrow(root, "breaks")
			.Select(x => new RelatedMod(x)).ToArray();
		
		FolderName = folderName;
	}
	
	
	public static List<Mod> Parse(string directory) {
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
			JsonDocument jsonDoc;
			try {
				string text = File.ReadAllText(fullPath);
				jsonDoc = JsonDocument.Parse(text);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load mod.json at " + fullPath + ":\n" + e.Message);
				return;
			}

			void onError(Exception e) {
				logger.Error("Invalid mod.json at " + fullPath + ":\n" + e.Message);
			}

			try {
				Mod mod = new Mod(jsonDoc.RootElement, Path.GetFileName(modFolder));
				mods.Add(mod);
			}
			catch (InvalidDataException e) {
				onError(e);
			}
			catch (InvalidModException e) {
				onError(e);
			}
		});


		return mods.ToList();
	}
}
public class InvalidModException(string message) : Exception(message);

public class PatchLocation {
	public string Path;
	public PatchFormatType Type;
	
	public PatchLocation(JsonElement root) {
		Path = JsonUtil.GetStringOrThrow(root, "path");
		string typeString = JsonUtil.GetStringOrThrow(root, "type");
		Type = typeString switch {
			"gmlp" => PatchFormatType.GMLP,
			_ => throw new InvalidPatchTypeException("Invalid patch format type: " + typeString
				+ "\nRight now the only type is \"gmlp\".)")
		};
	}
}
public class InvalidPatchTypeException(string message) : InvalidModException(message);


public enum PatchFormatType {
	GMLP
}

public class RelatedMod {
	public string ModId;
	public SemVerRequirement Version;
	public OrderRequirement OrderRequirement;

	public RelatedMod(JsonElement root) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		Version = new SemVerRequirement(JsonUtil.GetStringOrThrow(root, "version"));
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
public class InvalidOrderRequirementException(string message) : InvalidModException(message);


public readonly struct SemVer() {
	public readonly int Major;
	public readonly int Minor;
	public readonly int Patch;

	public SemVer(string version, bool starAllowed) : this() {
		const string help1 = "Mods should have versions of the form \"major.minor.patch\", like \"1.0.0\", or \"2.3.4\"";
		const string help2 = "Mod relations should have versions of the form \"major.minor.patch\" (with shortening and '*' allowed), like \"1.0.0\", \"2.3\", or \"3.6.*\"";
		string help = starAllowed ? help2 : help1;
		string[] sections = version.Split(".");

		int ParseSection(string section) {
			if (starAllowed && section == "*")
				return -1;
			return int.Parse(section);
		}

		if (!starAllowed && sections.Length != 3) {
			throw new InvalidSemVerException($"Field \"version\" has too little dots. {help}");
		}
		try {
			switch (sections.Length) {
				case 0:
					throw new InvalidSemVerException($"Field \"version\" is blank. {help}");
				case 1:
					Major = ParseSection(sections[0]);
					Minor = -1;
					Patch = -1;
					break;
				case 2:
					Major = ParseSection(sections[0]);
					Minor = ParseSection(sections[1]);
					Patch = -1;
					break;
				case 3:
					Major = ParseSection(sections[0]);
					Minor = ParseSection(sections[1]);
					Patch = ParseSection(sections[2]);
					break;
				default:
					throw new InvalidSemVerException($"Field \"version\" has too many dots. {help}");
			}
		}
		catch (Exception e) {
			if (e is FormatException || e is OverflowException)
				throw new InvalidSemVerException($"Field \"version\" does not have valid numbers. {help}");
            throw;
        }
		if ((Major == -1 && Minor != -1) || (Major == -1 && Patch != -1) || (Minor == -1 && Patch != -1)) {
			throw new InvalidSemVerException(
				"In Field \"version\", '*' should only appear at the end, and no numbers can show up after it.");
		}
	}
	public override string ToString() {
		string ma = (Major == -1) ? "*" : $"{Major}";
		string mi = (Minor == -1) ? "*" : $"{Minor}";
		string p = (Patch == -1) ? "*" : $"{Patch}";
		return $"{ma}.{mi}.{p}";
	}
}
public class InvalidSemVerException(string message) : InvalidModException(message);

public readonly struct SemVerRequirement() {
	private readonly SemVer Version;
	public SemVerRequirement(string version) : this() {
		Version = new SemVer(version, true);
	}

	public bool IsCompatibleWith(SemVer dependencyVersion) {
		if (Version.Major == -1)
			return true;
		if (Version.Major != dependencyVersion.Major)
			return false;
		if (Version.Minor == -1)
			return true;
		if (Version.Minor > dependencyVersion.Minor)
			return false;
		if (Version.Patch == -1)
			return true;
		return Version.Patch <= dependencyVersion.Patch;
	}

	public override string ToString() {
		return Version.ToString();
	}
}
public enum SemVerComparison {
	GreaterEquals,
	LesserEquals,
	Greater,
	Lesser,
	Equals
}
public class InvalidSemVerRequirementException(string message) : InvalidModException(message);
