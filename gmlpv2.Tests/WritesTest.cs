namespace gmlpv2.Tests;

public class WritesTest {
	private string code = 
"""
a
b
c
d
e
""";

	private string patch = 
"""
patch('only', function(t)
    t:write_before(1, 'start')
    t:write_before(1, 'start 2')
    t:write(1, 'after a')
    t:write(1, 'after a 2')
    
    local i = t:find_line_with(1, 'c')
    t:write(i - 1, 'beforest c')
    t:write_before(i, 'before c')
    t:write(i, 'after c')
    
    t:write(t:last_line(), 'after end')
    t:write_before(t:last_line(), 'before end')
end)
""";

	private string expected =
"""
start 2
start
a
after a
after a 2
b
beforest c
before c
c
after c
d
before end
e
after end
""";
	
	[Test]
	public void Test() {
		Shared.ApplyAndCompare(code, patch, expected);
	}
}