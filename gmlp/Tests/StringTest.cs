namespace gmlp.Tests;

public class StringTest() : LanguageTest("Strings") {
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
write('1')
""",

"""
find_line_with('
aaaaabbbbb')
write('2')
""",

"""
find_line_with('
aaaaabbbbb
')
write('3')
""",

"""
find_line_with(
'aaaaabbbbb'
)
write(
'
4
')
"""

		];
	}
	public override string GetExpected() {
		return 
"""
ccccc;
aaaaabbbbb;
1
2
3
4
ddddd;
""";
	}
}