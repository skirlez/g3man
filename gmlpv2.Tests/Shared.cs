using PatchCommon;

namespace gmlpv2.Tests;

public class Shared {
	public static void ApplyAndCompare(string code, string patch, string expected) {

		Dictionary<string, string> dictionary = new Dictionary<string, string> {
			["only"] = code,
		};
		CodeSource source = new DictionaryCodeSource(dictionary);


		PatchIntentionAggregate<FileRecord> aggregate = new();
		Language.FindIntentions(patch, "test", aggregate);
		if (aggregate.HasErrors()) {
			Console.WriteLine(string.Join('\n', aggregate.GetAllErrors()));
			Assert.Fail();
		}

		RecordAggregate<FileRecord> record = aggregate.RealizeAll(source);
		PatchResults results = gmlpv2.Language.Apply(record, source);
	
		if (results.HasErrors()) {
			Console.WriteLine(string.Join('\n', results.GetAllErrors()));
			Assert.Fail();
		}
		
		Assert.That( results.GetResult("only"), Is.EqualTo(expected));
	}
}