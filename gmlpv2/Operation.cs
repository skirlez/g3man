using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Lua;
using PatchCommon;

namespace gmlpv2;

public class LineState(string line) {
	public readonly StringBuilder Before = new StringBuilder();
	public string LineToReinsert = line;
	public readonly StringBuilder After = new StringBuilder();

	public string GetResult() {
		return $"{Before}{LineToReinsert}{After}";
	}
}

public abstract class Operation(string name, LuaValueType[] additionalTypes) {
	public static readonly List<Operation> All = new List<Operation>();

	static Operation() {
		All.Add(new WriteLineOperation("write", (state, text) => {
			state.After.Append("\n" + text);
		}));
		All.Add(new WriteLineOperation("write_before", (state, text) => {
			state.Before.Insert(0, text + "\n");
		}));
		All.Add(new WriteLineOperation("write_replace", (state, text) => {
			state.LineToReinsert = text;
		}));
		All.Add(new FindLineWithOperation("find_line_with", false, false));
		All.Add(new FindLineWithOperation("find_line_with_reverse", true, false));
		All.Add(new FindLineWithOperation("find_line_with_regex", false, true));
		All.Add(new FindLineWithOperation("find_line_with_reverse_regex", true, true));
		All.Add(new WriteSubstringOperation("write_replace_substring", (state, old, replacer) => {
			state.LineToReinsert = state.LineToReinsert.Replace(old, replacer);
		}));

		All.Add(new WriteSubstringOperation("write_replace_substring_regex", (state, pattern, replacer) => {
			Regex regex = new Regex(pattern, RegexOptions.CultureInvariant);
			state.LineToReinsert = regex.Replace(state.LineToReinsert, replacer);
		}));
		
		All.Add(new EndOperation("last_line"));
	}

	public static void AcquiantAll(LuaTable table, PatchInfo info) {
		foreach (Operation operation in All) {
			operation.Acquaint(table, info);
		}
	}
	
	private readonly LuaValueType[] additionalTypes = additionalTypes;

	public abstract ValueTask<int> Perform(FileRecord record, CodeFile file, PatchInfo info, LuaFunctionExecutionContext context, CancellationToken ct);
	
	protected void AddWrappedOperation(LuaTable table, PatchInfo info, Func<FileRecord, CodeFile, PatchInfo, LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> func) {
		table[name] = new LuaFunction(name, (context, ct) => {
			AssertSignature(context);
			FileRecord record = table["record"].Read<FileRecord>();
			CodeFile file = table["code_file"].Read<CodeFile>();
			return func(record, file, info, context, ct);
		});
	}

	protected static void ThrowIfOutOfBounds(LuaState state, int line, CodeFile file) {
		string[] lines = file.GetAsLines();
		
		if (line < 1 || line > lines.Length) {
			throw new LuaRuntimeException(state, $"Line {line} is outside the scope of the target code file (1, {lines.Length})");
		}
	}

	protected void AssertSignature(LuaFunctionExecutionContext context) {
		string callHelp = $"Function \"{name}\" expects to be called with the target/t as the first parameter, or with \"t:{name}(...)\" syntax.";
		if (context.Arguments.Length == 0) {
			throw new LuaRuntimeException(context.State, callHelp);
		}
		LuaValue first = context.Arguments[0];
		if (first.Type != LuaValueType.Table) {
			throw new LuaRuntimeException(context.State, callHelp);
		}
		int typesAmount = additionalTypes.Length + 1;
		if (context.ArgumentCount != typesAmount) {
			throw new LuaRuntimeException(context.State, $"Function \"{name}\" expects {typesAmount - 1} arguments, but got {context.ArgumentCount - 1}");
		}

		for (int i = 0; i < additionalTypes.Length; i++) {
			if (additionalTypes[i] != context.Arguments[i + 1].Type) {
				throw new LuaRuntimeException(context.State,
					$"Function \"{name}\" expected argument {i + 1} to be a {LuaValue.ToString(additionalTypes[i])}, but got a {LuaValue.ToString(context.Arguments[i + 1].Type)}");
			}	
		}
	}
	private void Acquaint(LuaTable table, PatchInfo info) {
		AddWrappedOperation(table, info, this.Perform);
	}
}

public interface WritingOperation {
	public void Apply(LineState state, object[] arguments);
}

public class WriteLineOperation(string name, Action<LineState, string> apply) : 
		Operation(name, [LuaValueType.Number, LuaValueType.String]), 
		WritingOperation {
	public void Apply(LineState state, object[] arguments) {
		string text = (string)arguments[0];
		apply(state, text);
	}
	public override ValueTask<int> Perform(FileRecord record, CodeFile file, PatchInfo _, LuaFunctionExecutionContext context, CancellationToken ct) {
		int line = context.GetArgument<int>(1);
		ThrowIfOutOfBounds(context.State, line, file);
		
		string text = context.GetArgument<string>(2);
		record.Add(line, new PerformedOperation(this, [text]));
		return new ValueTask<int>(context.Return());
	}
}

public class WriteSubstringOperation(string name, Action<LineState, string, string> apply) : 
		Operation(name, [LuaValueType.Number, LuaValueType.String, LuaValueType.String]),
		WritingOperation {
	public void Apply(LineState state, object[] arguments) {
		string old = (string)arguments[0];
		string replacer = (string)arguments[1];
		apply(state, old, replacer);
	}
	public override ValueTask<int> Perform(FileRecord record, CodeFile file, PatchInfo _, LuaFunctionExecutionContext context, CancellationToken ct) {
		int line = context.GetArgument<int>(1);
		ThrowIfOutOfBounds(context.State, line, file);

		string old = context.GetArgument<string>(2);
		string replacer = context.GetArgument<string>(3);
		record.Add(line, new PerformedOperation(this, [old, replacer]));
		return new ValueTask<int>(context.Return());
	}
}

public readonly struct PerformedOperation(WritingOperation operation, params object[] arguments) {
	public void Apply(LineState state) {
		operation.Apply(state, arguments);
	}
}


public class FindLineWithOperation(string name, bool isReverse, bool isRegex) : Operation(name, [LuaValueType.Number, LuaValueType.String]) {
	public override ValueTask<int> Perform(FileRecord record, CodeFile file, PatchInfo info, LuaFunctionExecutionContext context, CancellationToken ct) {
		int line = context.GetArgument<int>(1);
		ThrowIfOutOfBounds(context.State, line, file);
		
		string str = context.GetArgument<string>(2);
		string code = file.GetAsString();
		string[] lines = file.GetAsLines();
		
		int lineIndex;
		if (isReverse)
			lineIndex = reverseFindLineWith(line - 1, lines, code, str, isRegex);
		else
			lineIndex = findLineWith(line - 1, lines, code, str, isRegex);

		if (info.FailFast && lineIndex == -1) {
			string range = !isReverse ? $"({line}, {lines.Length})" : ($"({lines.Length}, {line})");
			throw new LuaRuntimeException(context.State,
				$"Could not find any line matching \"{str}\", in the range {range}");
		}

		return new ValueTask<int>(context.Return(lineIndex == -1 ? -1 : lineIndex + 1));
	}
	private static int findLineWith(int start, string[] lines, string code, string str, bool isRegex) {
		int positionSum = 0;
		for (int j = 0; j < start; j++)
			positionSum += lines[j].Length + 1;

		if (positionSum >= code.Length)
			return -1;

		int index;
		if (isRegex) {
			Regex regex = new Regex(str, RegexOptions.Multiline | RegexOptions.CultureInvariant);
			Match match = regex.Match(code, positionSum);
			if (!match.Success)
				return -1;
			index = match.Index;
		}
		else
			index = code.IndexOf(str, positionSum, StringComparison.Ordinal);

		if (index == -1)
			return -1;
		for (int j = start; j < lines.Length; j++) {
			positionSum += lines[j].Length + 1; // incl newline
			if (positionSum > index) {
				return j;
			}
		}

		return -1;
	}
	
	private static int reverseFindLineWith(int start, string[] lines, string code, string str, bool isRegex) {
		int positionSum = 0;
		for (int j = 0; j <= start; j++)
			positionSum += lines[j].Length + 1;
		int index;
		if (isRegex) {
			Regex regex = new Regex(str, RegexOptions.Multiline | RegexOptions.CultureInvariant);
			Match match = regex.Match(code, 0);
			if (!match.Success)
				return -1;
			while (true) {
				Match next = match.NextMatch();
				if (!next.Success || next.Index >= positionSum)
					break;
				match = next;
			}

			index = match.Index;
		}
		else {
			int positionInFile;
			if (positionSum == code.Length + 1) // final line might not have newline
				positionInFile = code.Length;
			else
				positionInFile = positionSum;
			index = code.LastIndexOf(str, positionInFile, StringComparison.Ordinal);
			if (index == -1)
				return -1;
		}

		for (int j = start; j >= 0; j--) {
			positionSum -= lines[j].Length + 1;
			if (positionSum <= index)
				return j;
		}

		return -1;
	}
}

public class EndOperation(string name) : Operation(name, []) {
	public override ValueTask<int> Perform(FileRecord record, CodeFile file, PatchInfo _, LuaFunctionExecutionContext context, CancellationToken ct) {
		string[] lines = file.GetAsLines();
		return new ValueTask<int>(context.Return(lines.Length));
	}
}
