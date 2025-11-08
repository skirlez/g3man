namespace gmlp.Tests;

public class ExplicitOrderingTest() : LanguageTest("ExplicitOrdering") {
	public override string GetCode() {
		return 
"""
a = 0
""";
	}
	public override string[] GetPatchSections() {
		return [
// higher priority: if statement should be the outer if statement
"""
find_line_with('a = 0')
write_before('if condition {')
write_before('statement1')
write_last('statement2')
write_last('}')
""",

// lower priority: if statement should be the inner if statement
"""
find_line_with('a = 0')
write_before('if condition2 {')
write_before('statement3')
write_last('statement4')
write_last('}')
"""
		];
	}
	public override string GetExpected() {
		return 
"""
if condition {
statement1
if condition2 {
statement3
a = 0
statement4
}
statement2
}
""";
	}
}