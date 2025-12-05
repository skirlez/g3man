namespace gmlp.Tests;

public class WriteConditionsTest() : LanguageTest("WriteConditions") {
	public override string GetCode() {
		return 
"""
if (condition)
""";
	}
	public override string[] GetPatchSections() {
		return [
"""
move(1)
write_or_condition('test0')
write_and_condition('test1')
write_and_condition('test2')
write_or_condition('test3')
write_and_condition('test4')
write_or_condition('test5')
"""
		];
	}

	public override string GetExpected() {
		return
"""
if (((((((condition)
|| test0)
&& test1)
&& test2)
|| test3)
&& test4)
|| test5)
""";
	}
}