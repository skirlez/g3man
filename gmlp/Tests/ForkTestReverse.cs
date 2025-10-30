namespace gmlp.Tests;

public class ForkTestReverse() : LanguageTest("ForkReverse") {

	public override string GetCode() {
		return
"""
bbb;
eeeee;
eeeee;
eeeee;
eeeee;
bbb;
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
move_to_end()
reverse_find_all_lines_with('ee')
consolidate_into_bottom(2)
write_after('fff')
"""
		];
	}

	public override string GetExpected() {
		return
"""
bbb;
eeeee;
eeeee;
eeeee;
fff
eeeee;
fff
bbb;
""";
	}
}