namespace gmlp.Tests;

public class SkipScopeTest() : LanguageTest("SkipScopes") {
    	public override string GetCode() {
		return 
"""
aaa
{
bbb
{
ccc
}
ddd
}
eee
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
skip_scope()
write('4')

move_to_start()
find_line_with('{')
skip_scope()
write('5')

move_to_start()
find_line_with('{')
move(1)
skip_scope()
write('1')

move_to_start()
find_line_with('{')
move(1)
find_line_with('{')
skip_scope()
write('2')


move_to_start()
find_line_with('{')
move(1)
find_line_with('{')
move(1)
skip_scope()
write('3')
""",

		];
	}
	public override string GetExpected() {
		return 
"""
aaa
{
bbb
{
ccc
}
1
2
3
ddd
}
4
5
eee
""";
	}
}