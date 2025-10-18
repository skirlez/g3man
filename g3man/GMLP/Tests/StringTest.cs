namespace g3man.GMLP.Tests;

public class StringTest() : GMLPTest("Strings") {
	public override string GetCode() {
		return 
"""
ccccc;
aaaaabbbbb;
ddddd;
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
find_line_with('aaaaabbbbb')
write_after('1')
""",

"""
find_line_with('
aaaaabbbbb')
write_after('2')
""",

"""
find_line_with('
aaaaabbbbb
')
write_after('3')
""",

"""
find_line_with(
'aaaaabbbbb'
)
write_after('4')
"""

		];
	}
	public override string GetExpected() {
		return 
"""
ccccc;
aaaaabbbbb;
4
3
2
1
ddddd;
""";
	}
}