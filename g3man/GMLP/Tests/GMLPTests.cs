namespace g3man.GMLP.Tests;

public class GMLPTests {

	public static void TestAll() {
		GMLPTest[] tests = [new WritesTest(), new WritesTest(), new BracesTest(), new StringTest(), new AtStringTest()];
		foreach (GMLPTest test in tests) {
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