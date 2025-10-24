using System.Diagnostics;
using System.Reflection;
using gmlp;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
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
		RemoveSingleLineBlockBraces = true,
		EmptyLineAroundBranchStatements = true,
		EmptyLineBeforeSwitchCases = true
	};

	private static void Main(string[] args) {
		data = UndertaleData.CreateNew();
		data.GeneralInfo.Major = 2024;
		data.GeneralInfo.Minor = 13;
		data.GeneralInfo.BytecodeVersion = 17;
		data.ToolInfo.DecompilerSettings = settings;
		
		ReadDefinitions();
		
		codeEntry = UndertaleCode.CreateEmptyEntry(data, "gml_Script_singular");
		
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
	
	
	[JSInvokable("patch")]
	public static string Patch(string patch, string code) {
		PatchesRecord record = new PatchesRecord();
		CodeSource source = new SingleCodeSource(code);
		Language.ExecuteEntirePatch(patch, source, record, new PatchOwner(""));
		Language.ApplyPatches(record, source, []);

		CodeFile? result = source.GetCodeFile("");
		Debug.Assert(result is not null);
		string newCode = result.GetAsString();
		return newCode;
	}

	[JSInvokable("compile_and_decompile")]
	public static string CompileAndDecompile(string code) {
		importGroup.QueueReplace("gml_Script_singular", code);
		importGroup.Import();
		return new DecompileContext(context, codeEntry, settings).DecompileToString();
	}
	
	[JSInvokable("compile_and_dissassemble")]
	public static string CompileAndDisassemble(string code) {
		importGroup.QueueReplace(codeEntry, code);
		importGroup.Import();
		return codeEntry.Disassemble(data.Variables, data.CodeLocals?.For(codeEntry));
	}
	
	
	/**
	 * gmlp expects to work with a class that can provide several code files, but
	 * we only need to work with 1.
	 */
	private class SingleCodeSource(string only) : CodeSource {
		private string only = only;
		public override CodeFile? GetCodeFile(string _) {
			return new StringCodeFile(only);
		}

		public override void Replace(string _, string code) {
			only = code;
		}
	}
}