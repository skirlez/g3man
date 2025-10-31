namespace gmlp.Tests;

public class ScopeTest() : LanguageTest("Scopes") {
	public override string GetCode() {
		return
"""
a
a
a
{
b
b
{
c
c
c
}
b
b
}
a
a
a
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
find_line_with('{')
move(1)
enter_scope() move_to_end()
write_after('end of outer scope')
exit_scope()
move_to_end()
reverse_find_line_with('{')
move(1)
enter_scope() move_to_end()
write_after('end of inner scope')
"""
		];
	}
	public override string GetExpected() {
		return
"""
a
a
a
{
b
b
{
c
c
c
end of inner scope
}
b
b
end of outer scope
}
a
a
a
""";
	}
}