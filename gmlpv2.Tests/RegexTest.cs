namespace gmlpv2.Tests;

public class RegexTest {
	private string code = 
"""
a = "hello"
b = "this is text"
c = "this text has a number (6)"
d = "this text's number will get removed (1293921495)"
e = "something something 43565239432394239492349329529392"
f = "end"
""";

	private string patch = 
"""
patch('only', function(t)
    local i = t:find_line_with_regex(1, '\\d+')
    t:write(i, 'number above me')
    i = t:find_line_with_reverse_regex(t:last_line(), '\\d+')
    t:write_before(i, 'number used to be longer below this')
    t:write_replace_substring_regex(i, '\\d+', '5')
end)
""";

private string expected =
"""
a = "hello"
b = "this is text"
c = "this text has a number (6)"
number above me
d = "this text's number will get removed (1293921495)"
number used to be longer below this
e = "something something 5"
f = "end"
""";
	[Test]
	public void Test() {
		Shared.ApplyAndCompare(code, patch, expected);
	}
}