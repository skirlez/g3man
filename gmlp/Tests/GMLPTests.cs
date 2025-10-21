using System;

namespace gmlp.Tests;

public class LanguageTests {

	public static void TestAll() {
		LanguageTest[] tests = [new WritesTest(), new WritesTest(), new BracesTest(), new StringTest(), new AtStringTest()];
		foreach (LanguageTest test in tests) {
			Console.WriteLine($"Running test: {test.Name}");
			string result = test.GetResult();
			string expected = test.GetExpected();
			if (result == expected)
				Console.WriteLine("Passed");
			else
				Console.WriteLine($"Mismatch!\nExpected:\n{expected}\nGot:\n{result}\n");
		}
	}
}