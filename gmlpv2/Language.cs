using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		PatchIntentionAggregate<FileRecord> aggregate, int timeout) {

		LuaValue argument = context.GetArgument(0);

		LuaFunction callback = context.GetArgument<LuaFunction>(1);
		string patchFilename = callback.Name;
		
		bool last = false;
		bool critical = true;
		bool failFast = true;
		string patchName = patchFilename;
		IEnumerable<string> targets;
		if (argument.Type == LuaValueType.String) {
			targets = [argument.Read<string>()];
		}
		else if (argument.Type == LuaValueType.Table) {
			LuaTable optionsTable = argument.Read<LuaTable>();
			try {
				LuaValue targetsOption = optionsTable["target"];
				if (targetsOption.Type == LuaValueType.String)
					targets = [targetsOption.Read<string>()];
				else
					targets = targetsOption.Read<LuaTable>().GetArraySpan().ToArray().Where(x => x.Type == LuaValueType.String).Select(x => x.Read<string>()).ToList();

				// TODO: specified twice
				if (!optionsTable["last"].TryRead(out last))
					last = false;
				if (!optionsTable["critical"].TryRead(out critical))
					critical = true;
				if (!optionsTable["fail_fast"].TryRead(out failFast))
					failFast = true;
				if (!optionsTable["name"].TryRead(out patchName))
					patchName = patchFilename;
			}
			catch (Exception _) {
				throw new LuaRuntimeException(state, "Missing or wrong type in parameter passed to g3man.patch");
			}
		}
		else {
			throw new LuaRuntimeException(state, "g3man.patch expects either a string or a table as an argument");
		}

		
		

		
		foreach (string targetName in targets) {
			aggregate.AddIntention(last, new PatchIntention<FileRecord>(targetName, patchName, critical, failFast, (record, source, info) => {
				CodeFile? file = source.GetCodeFile(targetName);
				if (file is null) {
					return;
				}

				LuaTable target = new LuaTable();
				target["record"] = (LuaValue.FromObject(record));
				target["code_file"] = (LuaValue.FromObject(file));
				target["name"] = targetName;
				Operation.AcquiantAll(target, info);
				try {
					using CancellationTokenSource cts = new(TimeSpan.FromSeconds(timeout));
					state.CallAsync(callback, [target], cts.Token).GetAwaiter().GetResult();
				}
				catch (LuaRuntimeException e) {
					record.AddError(PrettyString(e, info.Name));
				}
				catch (LuaCanceledException _) {
					record.AddError($"Realizing patch intention in \"{patchFilename}\" took too long (infinite loop?)");
				}
				catch (Exception e) {
					record.AddError($"Unhandled error while realizing patch intention in \"{patchFilename}\":\n{e}");
				}

			}));
		}
		return new(context.Return());
	}
	


	public static void AddModule(LuaState state, PatchIntentionAggregate<FileRecord> aggregate, int timeout) {
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
				PatchBase(state, context, ct, aggregate, timeout));
			return new (mcontext.Return(g3man));
		});
	}


	private class SandboxedFileSystem(string basis) : ILuaFileSystem {
		private FileSystem s = new(basis);
		public bool IsReadable(string path) {
			string norm = Path.GetFullPath(Path.Combine(basis, path));
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
			// TODO
			throw new UnauthorizedAccessException(STOP);
		}
		public ValueTask<ILuaStream> OpenTempFileStream(CancellationToken cancellationToken) {
			// TODO
			throw new UnauthorizedAccessException(STOP);
			//return s.OpenTempFileStream(cancellationToken);
		}
	}


	
	public static void FindIntentions(string patch, string? folder, string filename, PatchIntentionAggregate<FileRecord> aggregate, int timeout = 1000) {
		LuaPlatform platform = new LuaPlatform(new SandboxedFileSystem(folder ?? ""), new SystemOsEnvironment(), new ConsoleStandardIO(),
				TimeProvider.System);
	
		LuaState state = LuaState.Create(platform);
		state.OpenStandardLibraries();
		AddModule(state, aggregate, timeout);
		
		try {
			using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(timeout));
			state.DoStringAsync(patch, $"@{filename}", cts.Token).GetAwaiter().GetResult();
		}
		catch (LuaCompileException e) {
			aggregate.AddError(PrettyString(e));
		}
		catch (LuaRuntimeException e) {
			aggregate.AddError(PrettyString(e, filename));
		}
		catch (LuaCanceledException _) {
			aggregate.AddError($"Patch \"{filename}\" took too long to find the intentions of (infinite loop?)");
		}
		catch (PatchBadIntentionsException e) {
			aggregate.AddError($"In patch \"{filename}\": {e.Message}");
		}
		catch (Exception e) {
			aggregate.AddError($"Unhandled error while finding patch intentions of \"{filename}\":\n{e}");
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
				int line = lineChangesPair.Key;
				LineState state = new LineState(lines[line - 1]);
				foreach (PerformedOperation performed in lineChanges) {
					try {
						performed.Apply(state);
					}
					catch (LuaRuntimeException e) {
						results.AddError(targetName, PrettyString(e));
					}
				}
				lines[line - 1] = state.GetResult();
				

			}
			string newCode = string.Join("\n", lines);
			results.AddResult(targetName, newCode);
		}

		return results;
	}

	

	public static string PrettyString(LuaRuntimeException e, string? name = null) {
		if (e.ErrorObject == LuaValue.Nil) {
			return e.ToString();
		}
		if (name is null)
			return $"In line {e.LuaTraceback?.LastLine}:\n{e.ErrorObject.ToString()}";
		return $"In patch \"{name}\", line {e.LuaTraceback?.LastLine}:\n{e.ErrorObject.ToString()}";
	}
	public static string PrettyString(LuaCompileException e) {
		return $"In file \"{e.ChunkName}\", line {e.Position.Line}, column {e.Position.Column}:\n{e.MessageWithNearToken}";
	}
}