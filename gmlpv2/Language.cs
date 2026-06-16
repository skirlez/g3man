using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using Lua.IO;
using Lua.Platforms;
using Lua.Standard;
using PatchCommon;

namespace gmlpv2;

using LuaFunctionType = Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>>;



public static class Language {
	private static ValueTask<int> PatchBase(LuaState state, LuaFunctionExecutionContext context, CancellationToken ct, 
		PatchIntentionAggregate<FileRecord> aggregate, bool last = false) {

		LuaValue targets = context.GetArgument(0);
		
		IEnumerable<string> targetsList = targets.Type == LuaValueType.String
			? [targets.Read<string>()] 
			: targets.Read<LuaTable>().GetArraySpan().ToArray().Where(x => x.Type == LuaValueType.String).Select(x => x.Read<string>()).ToList();
		
		LuaFunction callback = context.GetArgument<LuaFunction>(1);
	
		string patchFilename = callback.Name;
		
		foreach (string targetName in targetsList) {
			aggregate.AddIntention(last, new PatchIntention<FileRecord>(targetName, patchFilename, true, (record, source) => {
				CodeFile? file = source.GetCodeFile(targetName);
				if (file is null) {
					return;
				}

				LuaTable target = new LuaTable();
				target["record"] = (LuaValue.FromObject(record));
				target["code_file"] = (LuaValue.FromObject(file));
				target["name"] = targetName;
				Operation.AcquiantAll(target);
				try {
					var _ = state.CallAsync(callback, [target], ct).Result;
				}
				catch (LuaCompileException e) {
					record.AddError(PrettyString(e));
				}
				catch (LuaRuntimeException e) {
					record.AddError(PrettyString(e, patchFilename));
				}

			}));
		}
		return new(context.Return());
	}
	


	public static void AddModule(LuaState state, PatchIntentionAggregate<FileRecord> aggregate) {
		LuaTable preload;
		if (state.Environment["package"].Type == LuaValueType.Nil) {
			LuaTable package = new LuaTable();
			state.Environment["package"] = package;
			preload = new LuaTable();
			package["preload"] = preload;
		}
		else {
			// i don't think this ever happens
			LuaTable package = state.Environment["package"].Read<LuaTable>();
			if (package["preload"].Type == LuaValueType.Nil) {
				preload = new LuaTable();
				package["preload"] = preload;
			}
			else
				preload = package["preload"].Read<LuaTable>();
		}


		preload["g3man"] = new LuaFunction("g3man-module-loader", (mcontext, _) => {
			LuaTable g3man = new LuaTable();
			g3man["patch"] = new LuaFunction("patch", (context, ct) => 
				PatchBase(state, context, ct, aggregate));
			return new (mcontext.Return(g3man));
		});
	}


	private class SandboxedFileSystem(string basis) : ILuaFileSystem {
		private FileSystem s = new FileSystem(basis);
		public bool IsReadable(string path) {
			string norm = Path.GetFullPath(Path.Combine(basis, path));
			Console.WriteLine(basis);
			Console.WriteLine(path);
			Console.WriteLine(norm);
			return norm.StartsWith(Path.GetFullPath(basis));
		}

		private const string STOP = "Path outside the sandbox";
		public ValueTask<ILuaStream> Open(string path, LuaFileOpenMode mode, CancellationToken cancellationToken) {
			if (!IsReadable(path))
				throw new UnauthorizedAccessException(STOP);
			return s.Open(path, mode, cancellationToken);
		}
		public ValueTask Rename(string oldName, string newName, CancellationToken cancellationToken) {
			if (!IsReadable(oldName) || !IsReadable(newName))
				throw new UnauthorizedAccessException(STOP);
			return s.Rename(oldName, newName, cancellationToken);
		}
		public ValueTask Remove(string path, CancellationToken cancellationToken) {
			if (!IsReadable(path))
				throw new UnauthorizedAccessException(STOP);
			return s.Remove(path, cancellationToken);
		}
		public string GetTempFileName() {
			return s.GetTempFileName();
		}
		public ValueTask<ILuaStream> OpenTempFileStream(CancellationToken cancellationToken) {
			return s.OpenTempFileStream(cancellationToken);
		}
	}

	public static void FindIntentions(string patch, string? folder, string filename, PatchIntentionAggregate<FileRecord> aggregate) {
		LuaPlatform platform = new LuaPlatform(new SandboxedFileSystem(folder ?? ""), new SystemOsEnvironment(), new ConsoleStandardIO(),
				TimeProvider.System);
	
		LuaState state = LuaState.Create(platform);
		state.OpenBasicLibrary();
		state.OpenModuleLibrary();
		state.OpenStandardLibraries();
		
		
		AddModule(state, aggregate);
		try {
			_ = state.DoStringAsync(patch, $"@{filename}").Result;
		}
		catch (LuaCompileException e) {
			aggregate.AddError(PrettyString(e));
		}
		catch (LuaRuntimeException e) {
			aggregate.AddError(PrettyString(e, filename));
		}
		catch (PatchBadIntentionsException e) {
			aggregate.AddError(e.Message);
		}
	}


	public static PatchResults Apply(RecordAggregate<FileRecord> aggregate, CodeSource source) {
		PatchResults results = new PatchResults();

		foreach (KeyValuePair<string, FileRecord> kvp in aggregate.GetChanges()) {
			string targetName = kvp.Key;
			FileRecord record = kvp.Value;
			
			if (record.HasErrors())
				results.AddErrors(record.GetErrors());
			
			CodeFile file = source.GetCodeFile(targetName)!;
			string[] lines = (string[])file.GetAsLines().Clone();
			
			foreach (KeyValuePair<int, List<PerformedOperation>> lineChangesPair in record.GetChanges()) {
				List<PerformedOperation> lineChanges = lineChangesPair.Value;
				foreach (PerformedOperation performed in lineChanges) {
					int line = lineChangesPair.Key;
					LineState state = new LineState(lines[line - 1]);
					try {
						performed.Apply(state);
					}
					catch (LuaRuntimeException e) {
						results.AddError(targetName, PrettyString(e));
					}
					lines[line - 1] = state.GetResult();
				}
				

			}
			string newCode = string.Join("\n", lines);
			results.AddResult(targetName, newCode);
		}

		return results;
	}

	

	public static string PrettyString(LuaRuntimeException e, string? filename = null) {
		if (e.ErrorObject == LuaValue.Nil) {
			return e.ToString();
		}
		if (filename is null)
			return $"In line {e.LuaTraceback?.LastLine}:\n{e.ErrorObject.ToString()}";
		return $"In file \"{filename}\", line {e.LuaTraceback?.LastLine}:\n{e.ErrorObject.ToString()}";
	}
	public static string PrettyString(LuaCompileException e) {
		return $"In file {e.ChunkName}, line {e.Position.Line}, column {e.Position.Column}:\n{e.MainMessage}";
	}
}