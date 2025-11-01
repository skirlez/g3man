using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gmlp;

/**
* Can you tell I've never written something like this before?
*/
public static class Language {
	private static readonly Dictionary<string, OperationType> WriteFunctionTypes = new Dictionary<string, OperationType> {
		{ "write_before", OperationType.WriteBefore },
		{ "write_replace", OperationType.WriteReplace },
		{ "write_after", OperationType.WriteAfter },
		{ "write_after_else_if",  OperationType.WriteAfterElseIf },
		{ "write_after_else", OperationType.WriteAfterElse },
	};

	private static readonly Dictionary<string, (OperationType, char)> BraceFunctionTypes =
		new Dictionary<string, (OperationType, char)> {
			{ "open_brace_before", (OperationType.WriteBefore, '{') },
			{ "open_brace_after", (OperationType.WriteAfter, '{') },
			{ "close_brace_before", (OperationType.WriteBefore, '}') },
			{ "close_brace_after", (OperationType.WriteAfter, '}') }
		};


	public static void ExecuteEntirePatch(string patchText, CodeSource data, PatchesRecord record, PatchOwner owner) {
		int patchIncrement = 0;

		Token[] tokens = Tokenize(patchText);
		int pos = 0;
		while (pos < tokens.Length) {
			int lastLineNumber = tokens[pos].LineNumber;
			if (tokens[pos] is SectionToken metaSectionToken && metaSectionToken.Section == "meta") {
				(string target, bool critical, pos) = ExecuteMetadataSection(tokens, pos + 1);

				CodeFile? codeFile = data.GetCodeFile(target);
				if (codeFile is null)
					throw new InvalidPatchException($"Target \"{target}\" does not exist");
				string code = codeFile.GetAsString();

				if (pos < tokens.Length && tokens[pos] is SectionToken patchSectionToken &&
					patchSectionToken.Section == "patch") {
					pos = ExecutePatchSection(tokens, pos + 1, target, code, critical, owner, record, true,
						ref patchIncrement);
				}
				else {
					throw new InvalidPatchException($"Incomplete patch; meta section without patch section");
				}
			}
			else {
				throw new InvalidPatchException(
					$"Expected \"meta:\" section at start of patch (line {lastLineNumber})");
			}
		}
	}


	private static (string target, bool critical, int pos) ExecuteMetadataSection(Token[] tokens, int pos) {
		bool critical = true;
		string? target = null;
		while (pos < tokens.Length) {
			Token token = tokens[pos];
			if (token is NameToken nameToken) {
				switch (nameToken.Name) {
					case "critical": {
						Token equalsToken = Expect(tokens, pos + 1, typeof(EqualsToken), nameToken.LineNumber);
						pos++;
						NameToken valueToken =
							(NameToken)Expect(tokens, pos + 1, typeof(NameToken), equalsToken.LineNumber);
						pos++;

						if (valueToken.Name != "true" && valueToken.Name != "false") {
							throw new InvalidPatchException(
								$"At line {valueToken.LineNumber}: Expected \"true\" or \"false\"");
						}

						critical = valueToken.Name == "true";
						break;
					}
					case "target": {
						Token equalsToken = Expect(tokens, pos + 1, typeof(EqualsToken), nameToken.LineNumber);
						pos++;
						NameToken targetToken =
							(NameToken)Expect(tokens, pos + 1, typeof(NameToken), equalsToken.LineNumber);
						pos++;
						target = targetToken.Name;
						break;
					}
					default:
						throw new InvalidPatchException(
							$"At line {nameToken.LineNumber}: invalid metadata variable {nameToken.Name}");
				}
			}
			else {
				break; // leave as soon as we stop seeing name tokens
			}

			pos++;
		}

		if (target is null)
			throw new InvalidPatchException($"Meta section must contain \"target\"");

		return (target, critical, pos);
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
			if (positionSum > index)
				return j;
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


	private struct Caret(int line, int startLine, int endLine) : IEquatable<Caret> {
		public int Line = line;
		public int StartLine = startLine;
		public int EndLine = endLine;

		public override bool Equals(object? obj) {
			if (obj is Caret other)
				return Equals(other);
			return base.Equals(obj);
		}

		public bool Equals(Caret other) {
			return Line == other.Line && StartLine == other.StartLine && EndLine == other.EndLine;
		}

		public override int GetHashCode() {
			return HashCode.Combine(Line, StartLine, EndLine);
		}
	}

	private static void unifyCarets(List<Caret> carets) {
		for (int i = 0; i < carets.Count; i++) {
			Caret caret = carets[i];
			while (true) {
				if (i + 1 >= carets.Count)
					break;
				int ind = carets.FindIndex(i + 1, (c) => c.Line == caret.Line);
				if (ind == -1)
					break;
				Caret other = carets[ind];
				// if the other one's scope is wider, Steal it
				if (other.StartLine > caret.StartLine || other.EndLine < caret.EndLine) {
					caret.StartLine = other.StartLine;
					caret.EndLine = other.EndLine;
				}
				carets.RemoveAt(ind);
			}
			carets[i] = caret;
		}
	}

	public static int ExecutePatchSection(Token[] tokens, int pos, string target, string code, bool critical,
		PatchOwner owner, PatchesRecord record, bool bailOnSection, ref int patchIncrement) {
		// TODO make sure code has \n line endings only
		string[] lines = code.Split('\n');
		UnitOperations unitOperations = record.GetUnitOperationsOrCreate(target, code);

		List<Caret> carets = [new Caret(0, 0, lines.Length - 1)];
		string lastRemovalReason = "";
		while (pos < tokens.Length) {
			if (carets.Count == 0) {
				throw new PatchExecutionException($"All carets have been removed (have been sent out of their scope, or have searched for nonexistent lines). Log from last caret:\n{lastRemovalReason}");
			}
			
			Token token = tokens[pos];
			if (token is SectionToken && bailOnSection) {
				break;
			}

			Expect(tokens, pos, typeof(NameToken), token.LineNumber);
			Debug.Assert(token is NameToken);
			NameToken nameToken = (NameToken)token;
			switch (nameToken.Name) {
				case "enter_scope": {
					(_, pos) = ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, []);
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						int stack = 0;
						for (int j = caret.Line - 1; j >= 0; j--) {
							if (lines[j].EndsWith('}'))
								stack++;
							else if (lines[j].EndsWith('{')) {
								if (stack == 0) {
									caret.StartLine = j + 1;
									goto Found;
								}

								stack--;
							}
						}

						// there is no scope. this is a legal operation though
						caret.StartLine = 0;
						caret.EndLine = lines.Length - 1;
						continue;

						Found:
						stack = 0;
						for (int j = caret.Line + 1; j < lines.Length; j++) {
							if (lines[j].EndsWith('{'))
								stack++;
							else if (lines[j].EndsWith('}')) {
								if (stack == 0) {
									caret.EndLine = j - 1;
									break;
								}

								stack--;
							}
						}

						carets[i] = caret;
					}

					break;
				}
				case "exit_scope": {
					(_, pos) = ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, []);
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						caret.StartLine = 0;
						caret.EndLine = lines.Length - 1;
						carets[i] = caret;
					}
					
					break;
				}
				case "move_to_end": {
					(_, pos) = ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, []);
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						caret.Line = caret.EndLine;
						carets[i] = caret;
					}
			
					unifyCarets(carets);
					break;
				}
				case "move_to_start": {
					(_, pos) = ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, []);
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						caret.Line = caret.StartLine;
						carets[i] = caret;
					}

					unifyCarets(carets);
					break;
				}
				
				case "move_to":
				case "move": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(NumberToken));
					NumberToken numberToken = (NumberToken)parameters[0];
					
					if (nameToken.Name == "move_to") {
						int line = numberToken.Number - 1;
						for (int i = 0; i < carets.Count; i++) {
							Caret caret = carets[i];
							if (line > caret.EndLine || line < caret.StartLine) {
								lastRemovalReason = $"Removed because tried running move_to({numberToken.Number}), " 
									+ $"which pushed me out of my scope (lines {caret.StartLine+1}-{caret.EndLine+1})";
								carets.RemoveAt(i);
								i--;
								continue;
							}
							caret.Line = line;
							carets[i] = caret;
						}
						unifyCarets(carets);
					}
					else {
						for (int i = 0; i < carets.Count; i++) {
							Caret caret = carets[i];
							int newLine = caret.Line + numberToken.Number;
							if (newLine > caret.EndLine || newLine< caret.StartLine) {
								lastRemovalReason = $"Removed because tried running move({numberToken.Number}) while on line {caret.Line+1}, " 
									+ $"which pushed me out of my scope (lines {caret.StartLine+1}-{caret.EndLine+1})";
								carets.RemoveAt(i);
								i--;
								continue;
							}
							caret.Line = newLine;
							carets[i] = caret;
						}
					}

					break;
				}
				case "find_line_with": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken));
					StringToken stringToken = (StringToken)parameters[0];
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						int newLinePos = findLineWith(caret.Line, lines, code, stringToken.Text, stringToken.Regex);
						if (newLinePos > caret.EndLine || newLinePos < caret.StartLine) {
							if (newLinePos == -1) {
								lastRemovalReason = $"Removed because tried running find_line_with('{stringToken.Text}') while on line {caret.Line+1} " +
									$"in the scope from lines {caret.StartLine+1}-{caret.EndLine+1}, but no line was found";
							}
							else {
								lastRemovalReason = $"Removed because tried running find_line_with('{stringToken.Text}') while on line {caret.Line+1}, " 
								+ $"but the line was found outside my scope (found at {newLinePos+1}, scope is from lines {caret.StartLine+1}-{caret.EndLine+1})";
							}
							carets.RemoveAt(i);
							i--;
							continue;
						}
						caret.Line = newLinePos;
						carets[i] = caret;
					}
					unifyCarets(carets);
					break;
				}
				case "reverse_find_line_with": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken));
					StringToken stringToken = (StringToken)parameters[0];
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						int newLinePos =
							reverseFindLineWith(caret.Line, lines, code, stringToken.Text, stringToken.Regex);
						if (newLinePos > caret.EndLine || newLinePos < caret.StartLine) {
							if (newLinePos == -1) {
								lastRemovalReason = $"Removed because tried running reverse_find_line_with('{stringToken.Text}') while on line {caret.Line+1} " +
									$"in the scope from lines {caret.StartLine+1}-{caret.EndLine+1}, but no line was found";
							}
							else {
								lastRemovalReason = $"Removed because tried running reverse_find_line_with('{stringToken.Text}') while on line {caret.Line+1}, " 
									+ $"but the line was found outside my scope (found at {newLinePos+1}, scope is from lines {caret.StartLine+1}-{caret.EndLine+1})";
							}
							carets.RemoveAt(i);
							i--;
							continue;
						}
						caret.Line = newLinePos;
						carets[i] = caret;
					}
					unifyCarets(carets);
					break;
				}

				case "find_all_lines_with":
				case "reverse_find_all_lines_with": {
					// UGLY
					Func<int, string[], string, string, bool, int> function =
						nameToken.Name == "find_all_lines_with" ? findLineWith : reverseFindLineWith;
					int direction = nameToken.Name == "find_all_lines_with" ? 1 : -1;

					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken));
					StringToken stringToken = (StringToken)parameters[0];

					List<Caret> newFilePositions = new List<Caret>();
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						int newPos = function(caret.Line, lines, code, stringToken.Text, stringToken.Regex);
						while (newPos >= caret.StartLine && newPos <= caret.EndLine) {
							newFilePositions.Add(new Caret(newPos, caret.StartLine, caret.EndLine));
							newPos = function(newPos + direction, lines, code, stringToken.Text, stringToken.Regex);
						}
					}

					newFilePositions.Sort((c1, c2) => c1.Line.CompareTo(c2.Line));
					carets = newFilePositions;
					if (newFilePositions.Count == 0) {
						lastRemovalReason = $"All carets have been removed because tried running {nameToken.Name}('{stringToken.Text}'), but found nothing";
					}
					else
						unifyCarets(carets);
					break;
				}
				case "consolidate_into_top":
				case "consolidate_into_bottom": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(NumberToken));
					NumberToken numberToken = (NumberToken)parameters[0];
					
					int number = int.Min(carets.Count, int.Max(numberToken.Number, 1));
					if (nameToken.Name == "consolidate_into_top")
						carets.RemoveRange(number, carets.Count - number);
					else
						carets.RemoveRange(0, carets.Count - number);


					break;
				}

				case "open_brace_before":
				case "open_brace_after":
				case "close_brace_before":
				case "close_brace_after": {
					(_, pos) = ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, []);
					for (int i = 0; i < carets.Count; i++) {
						int filePos = carets[i].Line;

						List<PatchOperation> linePatches = unitOperations.GetPatchOperationsOrCreate(filePos);
						(OperationType type, char character) = BraceFunctionTypes[nameToken.Name];

						linePatches.Add(new PatchOperation($"{character}", critical, type, owner, patchIncrement));
						patchIncrement++;
					}

					break;
				}
				case "write_before":
				case "write_replace":
				case "write_after":
				case "write_after_else":
				case "write_after_else_if": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken));
					StringToken stringToken = (StringToken)parameters[0];

					for (int i = 0; i < carets.Count; i++) {
						int filePos = carets[i].Line;
						List<PatchOperation> linePatches = unitOperations.GetPatchOperationsOrCreate(filePos);
						OperationType type = WriteFunctionTypes[nameToken.Name];
						linePatches.Add(new PatchOperation(stringToken.Text, critical, type, owner, patchIncrement));
						patchIncrement++;
					}

					break;
				}
				
				default:
					throw new InvalidPatchException(
						$"At line {nameToken.LineNumber}: unknown operation {nameToken.Name}");
			}


			pos++;
		}

		return pos;
	}

	public static void ExecutePatchSection(string patchSection, string target, string code, bool critical,
		PatchOwner owner, PatchesRecord record, ref int patchIncrement) {
		Token[] tokens = Tokenize(patchSection);
		ExecutePatchSection(tokens, 0, target, code, critical, owner, record, false, ref patchIncrement);
	}

	public static void ApplyPatches(PatchesRecord record, CodeSource source, List<PatchOwner> order) {
		foreach (KeyValuePair<string, UnitOperations> recordPair in record.GetData()) {
			string file = recordPair.Key;

			UnitOperations unitOperations = recordPair.Value;
			string[] lines = unitOperations.Code.Split('\n');

			foreach (KeyValuePair<int, List<PatchOperation>> unitPatchPair in unitOperations.GetData()) {
				int line = unitPatchPair.Key;
				List<PatchOperation> operations = unitPatchPair.Value;
				List<PatchOperation> replacers =
					unitPatchPair.Value.Where(op => op.Type == OperationType.WriteReplace).ToList();

				// can't merge if we have two replacers on the same line and both are critical (can't choose)
				// if we have two non-critical patches then we can pick the one with higher priority
				bool invalidUnitPatch = (replacers.Count >= 2 && (replacers.Count(op => op.Critical) >= 2));
				if (invalidUnitPatch) {
					List<string> atFaultList = replacers.Select(op => op.Owner.Name).ToList();
					throw new PatchApplicationException(
						$"There are two or more critical and incompatible replacers on the same line.",
						"The following mods are at fault", atFaultList);
				}

				replacers.Sort((a, b) => a.IsHigherPriorityThan(b, order));
				if (replacers.Count >= 2) {
					// pick out the last critical replacer, or the last non-critical replacer if we don't have any
					// (last has highest priority)
					PatchOperation chosenReplacer = replacers.LastOrDefault(op => op.Critical,
						replacers.Last(op => op.Type == OperationType.WriteReplace));
					operations.RemoveAll(op => replacers.Contains(op)
						&& op != chosenReplacer);
				}

				operations.Sort((a, b) => a.IsHigherPriorityThan(b, order));

				StringBuilder before = new StringBuilder();
				StringBuilder after = new StringBuilder();
				StringBuilder afterElseIf = new StringBuilder();
				StringBuilder afterElse = new StringBuilder();
				foreach (PatchOperation op in operations) {
					switch (op.Type) {
						case OperationType.WriteBefore:
							before.Insert(0, op.Text + "\n");
							break;
						case OperationType.WriteReplace:
							lines[line] = op.Text;
							break;
						case OperationType.WriteAfter:
							after.Append("\n" + op.Text);
							break;
						case OperationType.WriteAfterElseIf:
							afterElseIf.Append("\nelse if " + op.Text);
							break;
						case OperationType.WriteAfterElse:
							afterElse.Append("\n" + op.Text);
							break;
						default:
							break;
					}
				}

				string afterElseResult;
				if (afterElse.Length == 0)
					afterElseResult = "";
				else
					afterElseResult = $"\nelse {{ {afterElse}\n}}";
				lines[line] = $"{before}{lines[line]}{afterElseIf}{afterElseResult}{after}";
			}

			string finalResult = string.Join("\n", lines);
			try {
				source.Replace(file, finalResult);
			}
			catch (Exception e) {
				// this shit is probably really slow. but it runs on exception anyway, they deserve it
				List<List<PatchOwner>> dictList = unitOperations.GetData().ToList()
					.Select(kvp => kvp.Value.Select(op => op.Owner).ToList()).ToList();

				List<string> atFaultList = dictList.Aggregate((sum, next) => sum.Union(next).ToList())
					.Select(owner => owner.Name).ToList();


				throw new PatchApplicationException(e.Message, "One or more of the following mods are at fault",
					atFaultList, finalResult);
			}
		}
	}

	public class Token(int lineNumber) {
		public readonly int LineNumber = lineNumber;
	}

	public class NumberToken(int number, int lineNumber) : Token(lineNumber) {
		public readonly int Number = number;
	}

	public class NameToken(string name, int lineNumber) : Token(lineNumber) {
		public readonly string Name = name;
	}

	public class SectionToken(string section, int lineNumber) : Token(lineNumber) {
		public readonly string Section = section;
	}

	public class EqualsToken(int lineNumber) : Token(lineNumber);

	public class ParensStartToken(int lineNumber) : Token(lineNumber);

	public class ParensEndToken(int lineNumber) : Token(lineNumber);

	public class StringToken(string text, bool regex, int lineNumber) : Token(lineNumber) {
		public readonly string Text = text;
		public readonly bool Regex = regex;
	}

	public static Token[] Tokenize(string patch) {
		List<Token> tokens = new List<Token>();
		int lineNumber = 1;
		string build = "";
		for (int i = 0; i < patch.Length; i++) {
			char c = patch[i];
			if (c == '/' && i + 1 < patch.Length) {
				if (patch[i + 1] == '/') {
					i += 2;
					while (i < patch.Length && patch[i] != '\n')
						i++;
					lineNumber++;
					continue;
				}

				if (patch[i + 1] == '*') {
					i += 2;
					while (i + 1 < patch.Length && !(patch[i] == '*' && patch[i + 1] == '/')) {
						if (patch[i] == '\n')
							lineNumber++;
						i++;
					}

					i++;
					continue;
				}
			}

			if (char.IsWhiteSpace(c)) {
				if (build != "") {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}

				if (c == '\n')
					lineNumber++;
				continue;
			}

			if (c == ':') {
				if (build != "") {
					tokens.Add(new SectionToken(build, lineNumber));
					build = "";
				}

				continue;
			}

			if (build == "" && (c == '-' || c == '+' || char.IsDigit(c))) {
				build += c;
				i++;
				while (i < patch.Length && char.IsDigit(patch[i])) {
					build += patch[i];
					i++;
				}

				if (build == "-" || build == "+") {
					throw new InvalidPatchException($"At line {lineNumber}: Expected a number after the sign");
				}

				int number = int.Parse(build);
				tokens.Add(new NumberToken(number, lineNumber));

				build = "";
				i--;
				continue;
			}

			// TODO optimize
			if (c == '=') {
				if (build != "") {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}

				tokens.Add(new EqualsToken(lineNumber));
				continue;
			}

			if (c == '(') {
				if (build != "") {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}

				tokens.Add(new ParensStartToken(lineNumber));
				continue;
			}

			if (c == ')') {
				if (!string.IsNullOrWhiteSpace(build)) {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}

				tokens.Add(new ParensEndToken(lineNumber));
				continue;
			}

			if (c == '\'' || (c == '@' || c == 'r') && build.Length == 0) {
				if (c == 'r') {
					if (i + 1 >= patch.Length || patch[i + 1] != '\'') {
						build += c;
						continue;
					}

					i++;
				}

				if (!string.IsNullOrWhiteSpace(build)) {
					tokens.Add(new NameToken(build, lineNumber));
				}

				if (c == '@') {
					i++;
					if (i >= patch.Length || patch[i] != '\'') {
						throw new InvalidPatchException(
							$"At line {lineNumber}: Expected a string after the \'{c}\' character");
					}
				}

				bool stripNewlines = c != '@';
				bool regex = (c == 'r');


				int lineNumberStart = lineNumber;
				build = "";
				string text = "";

				// make sure we're 1 character away from the start of the string contents
				while (stripNewlines && i + 1 < patch.Length && patch[i + 1] == '\n') {
					i++;
				}


				while (i + 1 < patch.Length && !(patch[i] != '\\' && patch[i + 1] == '\'')) {
					if (patch[i + 1] == '\n')
						lineNumber++;
					text += patch[i + 1];
					i++;
				}


				while (stripNewlines && text[text.Length - 1] == '\n') {
					text = text.Substring(0, text.Length - 1);
				}

				if (i >= patch.Length) {
					throw new InvalidPatchException(
						$"At line {lineNumber}: Reached end of file before string terminated");
					continue;
				}

				// go over the ' we're currently on
				i++;

				if (!string.IsNullOrWhiteSpace(text)) {
					tokens.Add(new StringToken(text, regex, lineNumberStart));
				}

				continue;
			}

			build += c;
		}

		return tokens.ToArray();
	}


	private static (Token[], int) ExpectFunctionSignature(Token[] tokens, int pos, int lastLineNumber, params Type[] types) {
		pos++;
		Token parenthesisStart = Expect(tokens, pos, typeof(ParensStartToken), lastLineNumber);
		pos++;
		lastLineNumber = parenthesisStart.LineNumber;

		Token[] ret = new Token[types.Length];
		for (int i = 0; i < types.Length; i++) {
			Token t = Expect(tokens, pos, types[i], lastLineNumber);
			ret[i] = t;
			pos++;
			lastLineNumber = t.LineNumber;
		}

		Token parenthesisEnd = Expect(tokens, pos, typeof(ParensEndToken), lastLineNumber);
		return (ret, pos);
	}

	private static Token Expect(Token[] tokens, int pos, Type type, int lastLineNumber) {
		if (pos >= tokens.Length)
			throw new InvalidPatchException(
				$"At line {lastLineNumber}: Expected {GetHumanTypeName(type)}, found end of file");
		Token token = tokens[pos];
		if (!type.IsInstanceOfType(token))
			throw new InvalidPatchException(
				$"At line {token.LineNumber}: Expected {GetHumanTypeName(type)}, but found {GetHumanTypeName(token.GetType())}");
		return token;
	}

	private static string GetHumanTypeName(Type type) {
		switch (type.Name) {
			case "StringToken":
				return "a string";
			case "NameToken":
				return "a name";
			case "NumberToken":
				return "a number";
			case "SectionToken":
				return "the start of a section";
			case "EqualsToken":
				return "an equals sign";
			case "ParensStartToken":
				return "an opening parenthesis";
			case "ParensEndToken":
				return "a closing parenthesis";
			default:
				return "67";
		}
	}
}

public class InvalidPatchException(string message) : Exception(message);

public class PatchExecutionException(string message) : Exception(message);

public class PatchApplicationException(string message, string blameMessage, List<string> atFault, string? badCode = null) : Exception(message) {
	private readonly List<string> atFault = atFault;

	public string? GetBadCode() {
		return badCode;
	}

	public string HumanError() {
		Debug.Assert(atFault.Count != 0);
		string atFaultString = blameMessage + ":\n" + atFault[0];
		for (int i = 1; i < atFault.Count; i++) {
			atFaultString += ",\n" + atFault[i];
		}

		return $"{message}:\n{atFaultString}";
	}
}