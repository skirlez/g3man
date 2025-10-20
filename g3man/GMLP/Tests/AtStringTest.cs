namespace g3man.GMLP.Tests;

public class AtStringTest() : GMLPTest("AtStrings") {
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
write_after('1')

move_to_end()
reverse_find_line_with(
@'
bbbb')
write_after('2')
""",

		];
	}
	public override string GetExpected() {
		return 
"""
cccc
aaaa

2
1
bbbb
dddd
""";
	}
}