namespace gmlp.Tests;

public class RegexTest() : LanguageTest("Regex") {
	public override string GetCode() {
		return
"""
a = "hello"
b = "this is text"
c = "this text has a number (6)"
d = "this text also has a number (1293921495)"
e = "something something 4356"
f = "end"
""";
	}
	// TODO this needs more but I don't know regex.
	public override string[] GetPatchSections() {
		return [
"""
find_line_with(r'\d+')
write('number above me')
move_to_end()
reverse_find_line_with(r'\d+')
move(-1)
write_last('number below me')
"""
		];
	}

	public override string GetExpected() {
		return
"""
a = "hello"
b = "this is text"
c = "this text has a number (6)"
number above me
d = "this text also has a number (1293921495)"
number below me
e = "something something 4356"
f = "end"
""";
	}
}