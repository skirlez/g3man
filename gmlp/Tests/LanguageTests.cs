using System;

namespace gmlp.Tests;

public class LanguageTests {

	public static void TestAll() {
		LanguageTest[] tests = [new WritesTest(), new StringTest(), new AtStringTest(), new ForkTest(), new ForkTestReverse(),
			new RegexTest(), new ScopeTest(), new ImplicitOrderingTest(), new SkipScopeTest(), new WriteConditionsTest()];

		foreach (LanguageTest test in tests) {
			Console.WriteLine($"Running test: {test.Name}");
			string result = test.GetResult();
			string expected = test.GetExpected();
			#if WINDOWS
				result = expected.Replace("\r\n", "\n");
				expected = expected.Replace("\r\n", "\n");
			#endif
			if (result == expected)
				Console.WriteLine("Passed");
			else
				Console.WriteLine($"Mismatch!\nExpected:\n{expected}\nGot:\n{result}\n");
		}
	}
}