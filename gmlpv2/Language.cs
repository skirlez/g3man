using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using common;
using Lua;
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
	


	public static void Acquaint(LuaTable table, LuaState state, PatchIntentionAggregate<FileRecord> aggregate) {
		table["patch"] = new LuaFunction("patch", (context, ct) => 
			PatchBase(state, context, ct, aggregate));
	}


	public static void FindIntentions(string patch, string filename, PatchIntentionAggregate<FileRecord> aggregate) {
		LuaState state = LuaState.Create();
		state.OpenBasicLibrary();
		Acquaint(state.Environment, state, aggregate);
		try {
			_ = state.DoStringAsync(patch, $"@{filename}").Result;
		}
		catch (LuaCompileException e) {
			aggregate.AddError(PrettyString(e));
		}
		catch (LuaRuntimeException e) {
			aggregate.AddError(PrettyString(e, filename));
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