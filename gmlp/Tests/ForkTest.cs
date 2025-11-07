namespace gmlp.Tests;

public class ForkTest() : LanguageTest("Forks") {
	public override string GetCode() {
		return
"""
bbb;
aaaaa;
aaaaa;
aaaaa;
aaaaa;
bbb;
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
find_all_lines_with('aaaaa;')
consolidate_into_top(3)
write('ccc')
move_to_end()
write('end')
"""
		];
	}
	public override string GetExpected() {
		return
"""
bbb;
aaaaa;
ccc
aaaaa;
ccc
aaaaa;
ccc
aaaaa;
bbb;
end
""";
	}
}