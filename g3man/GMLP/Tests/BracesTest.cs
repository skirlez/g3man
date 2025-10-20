namespace g3man.GMLP.Tests;

public class BracesTest() : GMLPTest("Braces") {
	public override string GetCode() {
		return 
"""
aaa
bbb
ccc
ddd
eee
fff
ggg
hhh
iii
jjj
kkk
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
open_brace_before()
move(3)
close_brace_after()
find_line_with('jjj')
open_brace_before()
close_brace_after()
move_to_end()
reverse_find_line_with('hhh')
open_brace_before()
close_brace_before()
open_brace_after()
close_brace_after()
""",
"""
find_line_with('bbb')
open_brace_before()
move(1)
close_brace_after()
"""
		];
	}
	public override string GetExpected() {
		return 
"""
{
aaa
{
bbb
ccc
}
ddd
}
eee
fff
ggg
{
}
hhh
}
{
iii
{
jjj
}
kkk
""";
	}
}