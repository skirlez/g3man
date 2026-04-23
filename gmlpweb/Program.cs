using System.Diagnostics;
using System.Reflection;
using gmlpv2;
using Lua;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using PatchCommon;
using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace gmlpweb;

public class Program {
	private static UndertaleData data;
	private static UndertaleCode codeEntry;
	private static GlobalDecompileContext context;
	private static CodeImportGroup importGroup;
	private static DecompileSettings settings = new DecompileSettings() {
		UnknownArgumentNamePattern = "arg{0}",
		EmptyLineAroundBranchStatements = true,
		RemoveSingleLineBlockBraces = false,
		EmptyLineBeforeSwitchCases = true
	};

	private const string TheOnlyCodeFileName = "gmlpweb";

	private static void Main(string[] args) {
		data = UndertaleData.CreateNew();
		data.GeneralInfo.Major = 2024;
		data.GeneralInfo.Minor = 13;
		data.GeneralInfo.BytecodeVersion = 17;
		data.ToolInfo.DecompilerSettings = settings;
		
		ReadDefinitions();
		
		codeEntry = UndertaleCode.CreateEmptyEntry(data, TheOnlyCodeFileName);
		
		context = new GlobalDecompileContext(data);
		importGroup = new CodeImportGroup(data, context);
		init(args);
	}

	private static void ReadDefinitions() {
		Assembly assembly = Assembly.GetExecutingAssembly();
		using Stream? stream = assembly.GetManifestResourceStream("gmlpweb.gamemaker.json");
		Debug.Assert(stream is not null);
		using StreamReader reader = new StreamReader(stream);
		string definitons = reader.ReadToEnd();
		data.GameSpecificRegistry.DeserializeFromJson(definitons.AsSpan());
	}
	
	private static async void init(string[] args) {
		try {
			WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
			builder.Services.AddScoped(sp => new HttpClient
				{ BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

			WebAssemblyHost host = builder.Build();
			IJSRuntime js = host.Services.GetRequiredService<IJSRuntime>();
			await js.InvokeVoidAsync("onBlazorInitialized");
			
			await host.RunAsync();
		}
		catch (Exception e) {
			// need to do this (seemingly redundant) catch because of the async keyword, apparently
			Console.Error.WriteLine(e);
		}
	}
	
	
	/**
	 * Runs the gmlp patch `patch` on `code`.
	 * 
	 * Returns an object with a string result and an integer type.
	 *
	 * 0 - success
	 * 1 - patch failure
	 * 2 - unhandled program error
	 */
	[JSInvokable("patch")]
	public static object Patch(string patch, string code) {
		
		SingleCodeSource source = new SingleCodeSource(code);
		PatchIntentionAggregate<FileRecord> aggregate = new();
		string newCode;
		try {
			gmlpv2.Language.FindIntentions(patch, "gmlpweb.lua", aggregate);
		}
		catch (Exception e) {
			return new { result = e.ToString(), type = 2 };
		}

		if (aggregate.HasErrors()) {
			return new { result = string.Join('\n', aggregate.GetAllErrors()), type = 1 };
		}

		RecordAggregate<FileRecord> recordAggregate = aggregate.RealizeAll(source);
		PatchResults results = gmlpv2.Language.Apply(recordAggregate, source);
		if (results.HasErrors())
		{
			return new { result = string.Join('\n', results.GetAllErrors()), type = 1 };
		}
		newCode = results.GetAllResults().First().Value;
		
		
		return new { result = newCode, type = 0 };
	}

	[JSInvokable("compile_and_decompile")]
	public static object CompileAndDecompile(string code) {
		importGroup.QueueReplace(TheOnlyCodeFileName, code);
		CompileResult result = importGroup.Import(false);
		if (!result.Successful) {
			return new { result = CreateCompilationError(result), type = 1 };
		}
		string decompiled = new DecompileContext(context, codeEntry, settings).DecompileToString();
		return new { result = decompiled, type = 0 };
	}
	
	[JSInvokable("compile_and_disassemble")]
	public static object CompileAndDisassemble(string code) {
		importGroup.QueueReplace(codeEntry, code);
		CompileResult result = importGroup.Import(false);
		if (!result.Successful) {
			return new { result = CreateCompilationError(result), type = 1 };
		}
		string disassembly = codeEntry.Disassemble(data.Variables, data.CodeLocals?.For(codeEntry));
		return new { result = disassembly, type = 0 };
	}

	private static string CreateCompilationError(CompileResult result) {
		HashSet<string> alreadySaid = new HashSet<string>();
		
		foreach (CompileError error in result.Errors) {
			string detailedMessage = error.GenerateDetailedMessage();
			alreadySaid.Add(detailedMessage);
		}
		var s = (alreadySaid.Count > 1) ? "s" : "";
		return $"Code compilation error{s}:\n {string.Join("\n", alreadySaid)}";
	}
	
	/**
	 * gmlp expects to work with a class that can provide several code files, but
	 * we only need to work with one.
	 */
	private class SingleCodeSource(string only) : CodeSource {
		public override CodeFile GetCodeFile(string _) {
			return new CodeFile(only);
		}
	}
}