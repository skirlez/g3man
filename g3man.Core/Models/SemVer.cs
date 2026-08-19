namespace g3man.Core.Models;

public readonly struct SemVer() {
	public readonly int Major;
	public readonly int Minor;
	public readonly int Patch;

	public SemVer(string version, bool shorteningAllowed = false) : this() {
		const string help1 = "Mods should have versions of the form \"major.minor.patch\", like \"1.0.0\", or \"2.3.4\"";
		const string help2 = "Mod relations should have versions of the form \"major.minor.patch\" (with shortening allowed), like \"1.0.0\" or \"2.3\"";
		string help = shorteningAllowed ? help2 : help1;
		string[] sections = version.Split(".");

		int ParseSection(string section) {
			return int.Parse(section);
		}

		if (!shorteningAllowed && sections.Length != 3) {
			throw new InvalidSemVerException($"Field \"version\" has too little dots. {help}");
		}
		try {
			switch (sections.Length) {
				case 0:
					throw new InvalidSemVerException($"Field \"version\" is blank. {help}");
				case 1:
					Major = ParseSection(sections[0]);
					Minor = 0;
					Patch = 0;
					break;
				case 2:
					Major = ParseSection(sections[0]);
					Minor = ParseSection(sections[1]);
					Patch = 0;
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
	}
	public override string ToString() {
		return $"{Major}.{Minor}.{Patch}";
	}
}
public class InvalidSemVerException(string message) : InvalidModException(message);

public readonly struct SemVerRequirements {
	private readonly (SemVer, SemVerComparison)[] Conditions;

	private (SemVerComparison, int) GetComparison(string requirementString) {
		char first = requirementString[0];
		char second = requirementString[1];
		if (first == '~')
			return (SemVerComparison.RoughlyEquals, 1);
		if (first == '=')
			return (SemVerComparison.Equals, 1);
		if (first == '>') {
			if (second == '=')
				return (SemVerComparison.GreaterEquals, 2);
			return (SemVerComparison.Greater, 1);
		}
		if (first == '<') {
			if (second == '=')
				return (SemVerComparison.LesserEquals, 2);
			return (SemVerComparison.Lesser, 1);
		}
		return (SemVerComparison.RoughlyEquals, 0);
	}
	public SemVerRequirements(string[] requirementStrings) {
		Conditions = new (SemVer, SemVerComparison)[requirementStrings.Length];
		for (int i = 0; i < requirementStrings.Length; i++) {
			string requirementString = requirementStrings[i];
			if (requirementString.Length < 2)
				throw new InvalidSemVerRequirementException("Version requirement string is too short! ");

			(SemVerComparison comparison, int start) = GetComparison(requirementString);
			string version = requirementString.Substring(start);
			Conditions[i] = (new SemVer(version, true), comparison);
		}
	}

	public bool IsCompatibleWith(SemVer other) {
		foreach ((SemVer requirement, SemVerComparison comparison) in Conditions) {
			bool compatible = isCompatibleWith(requirement, comparison, other);
			if (compatible)
				return true;
		}
		return false;
	}

	private static bool isCompatibleWith(SemVer requirement, SemVerComparison comparison, SemVer other) {
		bool exactEqual = requirement.Major == other.Major 
						  && requirement.Minor == other.Minor 
						  && requirement.Patch == other.Patch;
		bool greater = semVerGreaterCompatible(other, requirement);
		bool lesser = semVerGreaterCompatible(requirement, other);
		switch (comparison) {
			case SemVerComparison.RoughlyEquals:
				if (requirement.Major != other.Major)
					return false;
				if (requirement.Minor != other.Minor)
					return false;
				return (requirement.Patch <= other.Patch);
			case SemVerComparison.Equals:
				return exactEqual;
			case SemVerComparison.Greater:
				return greater;
			case SemVerComparison.Lesser:
				return lesser;
			case SemVerComparison.GreaterEquals:
				return exactEqual || greater;
			case SemVerComparison.LesserEquals:
				return exactEqual || lesser;
			default:
				return false;
		}
	}
	
	private static bool semVerGreaterCompatible(SemVer one, SemVer two) {
		if (one.Major > two.Major)
			return true;
		if (one.Major < two.Major)
			return false;
		if (one.Minor > two.Minor)
			return true;
		if (one.Minor < two.Minor)
			return false;
		return one.Patch < two.Patch;
	}

	public override string ToString() {
		if (Conditions.Length == 0)
			return "None";
		
		string result = conditionToString(Conditions[0].Item1, Conditions[0].Item2);
		for (int i = 1; i < Conditions.Length; i++) {
			result += $" OR {conditionToString(Conditions[i].Item1, Conditions[i].Item2)}";
		}

		return result;
	}

	private string conditionToString(SemVer version, SemVerComparison comparison) {
		string operation = comparison switch {
			SemVerComparison.Equals => "=",
			SemVerComparison.RoughlyEquals => "~",
			SemVerComparison.Greater => ">",
			SemVerComparison.Lesser => "<",
			SemVerComparison.GreaterEquals => ">=",
			SemVerComparison.LesserEquals => "<=",

			_ => "="
		};

		return $"{operation}{version}";
	}
}

public enum SemVerComparison {
	GreaterEquals,
	LesserEquals,
	Greater,
	Lesser,
	RoughlyEquals,
	Equals
}
public class InvalidSemVerRequirementException(string message) : InvalidModException(message);