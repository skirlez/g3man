using PatchCommon;

namespace gmlpv2.Tests;

public static class Shared {
	public static void ApplyAndCompare(string code, string patch, string expected) {

		Dictionary<string, string> dictionary = new Dictionary<string, string> {
			["only"] = code,
		};
		CodeSource source = new DictionaryCodeSource(dictionary);


		PatchIntentionAggregate<FileRecord> aggregate = new();
		Language.FindIntentions($"local patch = (require \"g3man\").patch\n{patch}",  null, "test", aggregate);
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
	public static void AssertFailsIntentions(string patch) {
		PatchIntentionAggregate<FileRecord> aggregate = new();
		Language.FindIntentions($"local patch = (require \"g3man\").patch\n{patch}",  null, "test", aggregate);
		if (aggregate.HasErrors()) {
			Assert.Pass();
		}
		Console.WriteLine("This patch didn't fail and it should have!!!");
		Assert.Fail();
	}

	public static void AssertFailsRealization(string code, string patch) {
		Dictionary<string, string> dictionary = new Dictionary<string, string> {
			["only"] = code,
		};
		CodeSource source = new DictionaryCodeSource(dictionary);
		PatchIntentionAggregate<FileRecord> aggregate = new();
		Language.FindIntentions($"local patch = (require \"g3man\").patch\n{patch}",  null, "test", aggregate);
		if (aggregate.HasErrors()) {
			Console.WriteLine("This patch should fail, but it failed on intention finding, not realization");
			Assert.Fail();
		}
		RecordAggregate<FileRecord> record = aggregate.RealizeAll(source);
		PatchResults results = gmlpv2.Language.Apply(record, source);
		if (results.HasErrors()) {
			Assert.Pass();
		}
		Console.WriteLine("This patch didn't fail and it should have!!!");
		Console.WriteLine($"Here is what it put out:\n{ results.GetResult("only") }");
		Assert.Fail();
	}
}