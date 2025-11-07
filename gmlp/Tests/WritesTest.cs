namespace gmlp.Tests;

public class WritesTest() : LanguageTest("Writes") {
	public override string GetCode() {
		return 
"""
a
b
c
d
e
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
write('start')
find_line_with('b') move(-1)
write('before b')
move_to_end()
write('end')
""",

"""
write('start 2')
find_line_with('b') move(-1)
write('also before b')
""",

"""
find_line_with('c')
write('after c')
""",

"""
find_line_with('e')
write('after e')
""",

"""
move_to_end()
write('end 2')
"""
		];
	}
	public override string GetExpected() {
		return 
"""
start
start 2
a
before b
also before b
b
c
after c
d
e
end
after e
end 2
""";
	}
}