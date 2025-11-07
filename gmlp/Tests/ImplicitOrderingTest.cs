namespace gmlp.Tests;

public class ImplicitOrderingTest() : LanguageTest("ImplicitOrdering") {
	public override string GetCode() {
		return 
"""
a = 0;
b = 1;
c = 2;
if (a) {
    b = 0;
}

""";
	}
	public override string[] GetPatchSections() {
		return [
"""
find_line_with('}')
write_else_if('(c == 0) {
should be first
}')
""",
"""
find_line_with('}')
write_else('should be third')
""",
"""
find_line_with('}')
write_else_if('(b == 0) {
should be second
}')
""",
"""
find_line_with('}')
write('should be last')
""",
		];
	}
	public override string GetExpected() {
		return
"""
a = 0;
b = 1;
c = 2;
if (a) {
    b = 0;
}
else if (c == 0) {
should be first
}
else if (b == 0) {
should be second
}
else { 
should be third
}
should be last

""";
	}
}