namespace g3man.GMLP.Tests;

public class WritesTest() : GMLPTest("Writes") {
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
write_before('start')
find_line_with('b')
write_before('before b')
move_to_end()
write_after('end')
""",

"""
write_before('start 2')
find_line_with('b')
write_before('also before b')
""",

"""
find_line_with('c')
write_after('after c')
""",

"""
find_line_with('e')
write_after('after e')
""",

"""
move_to_end()
write_after('end 2')
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
end 2
after e
end
""";
	}
}