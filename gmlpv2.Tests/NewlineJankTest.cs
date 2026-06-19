namespace gmlpv2.Tests;

public class NewlineJankTest {
	private string code = 
"""
a
b
c
d
""";

	private string patch = 
"""
patch('only', function(t)
   t:find_line_with(1, [[
		b
   ]])
   t:find_line_with(1, [[b
   ]])
   t:find_line_with(1, [[
	b]])
   t:find_line_with(1, 'b')
   t:find_line_with(1, "\nb")
   t:find_line_with(1, [[
   
   
   
   b
   
   
   
   ]])
end)
""";
	
	
	[Test]
	public void Test() {
		Shared.ApplyAndCompare(code, patch, code);
	}
}