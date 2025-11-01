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
		EmptyLineAroundBranchStatements = true,
		EmptyLineBeforeSwitchCases = true
	};

	private const string TheOnlyCodeFileName = "singular";

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

		PatchesRecord record = new PatchesRecord();
		CodeSource source = new SingleCodeSource(code);

		try {
			Language.Token[] tokens = Language.Tokenize(patch);

			// our job is to get to the patch section, people may be pasting their entire patch here, and that should be valid
			int i = 0;
			while (i < tokens.Length && tokens[i] is Language.SectionToken sectionToken &&
					sectionToken.Section != "patch") {
				i++;
				while (i < tokens.Length && tokens[i] is not Language.SectionToken)
					i++;
				i++;
			}

			if (i >= tokens.Length) {
				// error the user for being stupid
				i = 0;
			}

			int increment = 0;
			Language.ExecutePatchSection(tokens, i, TheOnlyCodeFileName, code, true, new PatchOwner("gmlpweb"), record,
				false, ref increment);
			Language.ApplyPatches(record, source, []);
		}
		catch (PatchExecutionException e) {
			return new { result = e.Message, type = 1 };
		}
		catch (InvalidPatchException e) {
			return new { result = e.Message, type = 1 };
		}
		catch (Exception e) {
			return new { result = e.ToString(), type = 2 };
		}

		CodeFile? result = source.GetCodeFile("");
		Debug.Assert(result is not null);
		string newCode = result.GetAsString();
		return new { result = newCode, type = 0 };
	}

	[JSInvokable("compile_and_decompile")]
	public static string CompileAndDecompile(string code) {
		importGroup.QueueReplace(TheOnlyCodeFileName, code);
		importGroup.Import();
		return new DecompileContext(context, codeEntry, settings).DecompileToString();
	}
	
	[JSInvokable("compile_and_disassemble")]
	public static string CompileAndDisassemble(string code) {
		importGroup.QueueReplace(codeEntry, code);
		importGroup.Import();
		return codeEntry.Disassemble(data.Variables, data.CodeLocals?.For(codeEntry));
	}
	
	
	/**
	 * gmlp expects to work with a class that can provide several code files, but
	 * we only need to work with one.
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