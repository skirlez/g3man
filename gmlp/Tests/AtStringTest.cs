namespace gmlp.Tests;

public class AtStringTest() : LanguageTest("AtStrings") {
	public override string GetCode() {
		return 
"""
cccc
aaaa

bbbb
dddd
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
find_line_with(
@'
bbbb')
write('1')

move_to_end()
reverse_find_line_with(
@'
bbbb')
write('2')
""",

		];
	}
	public override string GetExpected() {
		return 
"""
cccc
aaaa

1
2
bbbb
dddd
""";
	}
}