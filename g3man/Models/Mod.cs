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
		Version = new SemVer(JsonUtil.GetStringOrThrow(root, "version"));
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
			
			try {
				Mod mod = new Mod(jsonDoc.RootElement, Path.GetFileName(modFolder));
				mods.Add(mod);
			}
			catch (InvalidDataException e) {
				logger.Error("Invalid mod.json at " + fullPath + ":\n" + e.Message);
			}
		});


		return mods.ToList();
	}
}


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
public class InvalidPatchTypeException(string message) : Exception(message);


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
public class InvalidOrderRequirementException(string message) : Exception(message);


public readonly struct SemVer() {
	public readonly uint Major;
	public readonly uint Minor;
	public readonly uint Patch;

	public SemVer(string version) : this() {
		const string help = "Mods should have versions of the form\"major.minor.patch\", like \"1.0.0\"";
		string[] sections = version.Split(".");
		try {
			switch (sections.Length) {
				case 0:
					throw new InvalidSemVerException($"Field \"version\" is blank. {help}");
				case 1:
					Major = uint.Parse(sections[0]);
					Minor = 0;
					Patch = 0;
					break;
				case 2:
					Major = uint.Parse(sections[0]);
					Minor = uint.Parse(sections[1]);
					Patch = 0;
					break;
				case 3:
					Major = uint.Parse(sections[0]);
					Minor = uint.Parse(sections[1]);
					Patch = uint.Parse(sections[3]);
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
	}
	public override string ToString() {
		return $"{Major}.{Minor}.{Patch}";
	}
}
public class InvalidSemVerException(string message) : Exception(message);

public readonly struct SemVerRequirement() {
	private readonly SemVer Version;
	private readonly SemVerComparison Comparison;
	public SemVerRequirement(string version) : this() {
		if (version.Length == 0)
			throw new InvalidSemVerException("Field \"version\" is empty.");
		string[] sections = Regex.Split(version, "(>=|<=|>|<|=)");
		if (sections.Length >= 2)
			throw new InvalidSemVerRequirementException("Field \"version\" does not contain a valid version requirement."
				+ "Version requirements look like (comparison)(major.minor.version). Examples: >=1.3.2, 3.2.1, <4.3.5");
		if (sections.Length == 1) {
			Version = new SemVer(sections[0]);
			Comparison = SemVerComparison.Equals;
		}
		else {
			Version = new SemVer(sections[1]);
			Comparison = sections[0] switch {
				">=" => SemVerComparison.GreaterEquals,
				"<=" => SemVerComparison.LesserEquals,
				">" => SemVerComparison.Greater,
				"<" => SemVerComparison.Lesser,
				"=" => SemVerComparison.Equals,
				_ => throw new InvalidSemVerRequirementException($"Field \"Version\" starts with invalid string \"{sections[0]}\"."
					+ "Valid starting strings are \"\", \">=\", \"<=\", \">\", \"<\", and \"=\"."),
			};
		}
	}

	public bool IsCompatibleWith(SemVer dependencyVersion) {
		bool equals = (dependencyVersion.Major == Version.Major)
			&& dependencyVersion.Minor == Version.Minor
			&& dependencyVersion.Patch == Version.Patch;
		switch (Comparison) {
			case SemVerComparison.Equals:
				return equals;
				break;
			case SemVerComparison.Greater:
				break;
			case SemVerComparison.Lesser:
				break;
			case SemVerComparison.GreaterEquals:
				break;
			case SemVerComparison.LesserEquals:
				break;
		}
	}
}
public enum SemVerComparison {
	GreaterEquals,
	LesserEquals,
	Greater,
	Lesser,
	Equals
}
public class InvalidSemVerRequirementException(string message) : Exception(message);
