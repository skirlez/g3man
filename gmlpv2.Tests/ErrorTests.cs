namespace gmlpv2.Tests;

public class ErrorTests {
	private string code1 = 
		"""
		wow
		""";
	private string patch1 = 
		"""
		patch('only', function(t)
		    t:find_line_with(1, 'does not exist')
		end)
		""";
	private string patch2 = 
		"""
		patch({target = 'only', fail_fast = false }, function(t)
		    t:find_line_with(1, 'does not exist')
		end)
		""";

	[Test]
	public void Test() {
		Shared.AssertFailsRealization(code1, patch1);
		Shared.ApplyAndCompare(code1, patch2, code1);
	}
}