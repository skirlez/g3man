using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gmlp;

/**
* Can you tell I've never written something like this before?
*/
public static class Language {

	private static readonly Dictionary<string, OperationType> WriteFunctionTypes = new Dictionary<string, OperationType> {
		{"write_before", OperationType.WriteBefore},
		{"write_replace", OperationType.WriteReplace},
		{"write_after", OperationType.WriteAfter}
	};
	
	private static readonly Dictionary<string, (OperationType, char)> BraceFunctionTypes = new Dictionary<string, (OperationType, char)> {
		{"open_brace_before", (OperationType.WriteBefore, '{') },
		{"open_brace_after", (OperationType.WriteAfter, '{') },
		{"close_brace_before", (OperationType.WriteBefore, '}') },
		{"close_brace_after", (OperationType.WriteAfter, '}') }
	};
	


	public static void ExecuteEntirePatch(string patchText, CodeSource data, PatchesRecord record, PatchOwner owner) {
		int patchIncrement = 0;
		
		Token[] tokens = tokenize(patchText);
		int pos = 0;
		while (pos < tokens.Length) {
			int lastLineNumber = tokens[pos].LineNumber;
			if (tokens[pos] is SectionToken metaSectionToken && metaSectionToken.Section == "meta") {
				(string target, bool critical, pos) = ExecuteMetadataSection(tokens, pos + 1);

				CodeFile? codeFile = data.GetCodeFile(target);
				if (codeFile is null)
					throw new InvalidPatchException($"Target \"{target}\" does not exist");
				string code = codeFile.GetAsString();
				
				if (pos < tokens.Length || tokens[pos] is SectionToken patchSectionToken
						&& patchSectionToken.Section == "patch") {
					pos = ExecutePatchSection(tokens, pos + 1, target, code, critical, owner, record, ref patchIncrement);
				}
				else {
					throw new InvalidPatchException($"Incomplete patch; meta section without patch section");
				}
			}
			else {
				throw new InvalidPatchException($"Expected \"meta:\" section at start of patch (line {lastLineNumber})");
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
							throw new InvalidPatchException($"At line {valueToken.LineNumber}: Expected \"true\" or \"false\"");
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
						throw new InvalidPatchException($"At line {nameToken.LineNumber}: invalid metadata name {nameToken.Name}");
						break;
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
	
	private static int ExecutePatchSection(Token[] tokens, int pos, string target, string code, bool critical, PatchOwner owner, PatchesRecord record, ref int patchIncrement) {
		// TODO make sure code has \n line endings only
		string[] lines = code.Split('\n');
		UnitOperations unitOperations = record.GetUnitOperationsOrCreate(target, code);
		
		int filePos = 0;
		while (pos < tokens.Length) {
			Token token = tokens[pos];
			if (token is SectionToken) {
				break;
			}

			if (token is NameToken nameToken) {
				switch (nameToken.Name) {
					case "move_to_end": {
						Token startToken = Expect(tokens, pos + 1, typeof(ParensStartToken), nameToken.LineNumber);
						pos++;
						Token endToken = Expect(tokens, pos + 1, typeof(ParensEndToken), startToken.LineNumber);
						pos++;
						filePos = lines.Length - 1;
						break;
					}
					case "move_to":
					case "move": {
						Token startToken = Expect(tokens, pos + 1, typeof(ParensStartToken), nameToken.LineNumber);
						pos++;
						NumberToken numberToken =
							(NumberToken)Expect(tokens, pos + 1, typeof(NumberToken), startToken.LineNumber);
						pos++;
						Token endToken = Expect(tokens, pos + 1, typeof(ParensEndToken), numberToken.LineNumber);
						pos++;

						if (nameToken.Name == "move_to")
							filePos = numberToken.Number - 1;
						else
							filePos += numberToken.Number;

						break;
					}
					case "find_line_with": {
						Token startToken = Expect(tokens, pos + 1, typeof(ParensStartToken), nameToken.LineNumber);
						pos++;
						StringToken stringToken =
							(StringToken)Expect(tokens, pos + 1, typeof(StringToken), startToken.LineNumber);
						pos++;
						Token endToken = Expect(tokens, pos + 1, typeof(ParensEndToken), stringToken.LineNumber);
						pos++;

						
						int positionSum = 0;
						for (int i = 0; i < filePos; i++) {
							positionSum += lines[i].Length + 1;
						}
						int index = code.IndexOf(stringToken.Text, positionSum, StringComparison.Ordinal);
						
						for (int i = filePos; i < lines.Length; i++) {
							positionSum += lines[i].Length + 1; // incl newline
							if (positionSum > index) {
								filePos = i;
								break;
							}
						}
						break;
					}
					case "reverse_find_line_with": {
						Token startToken = Expect(tokens, pos + 1, typeof(ParensStartToken), nameToken.LineNumber);
						pos++;
						StringToken stringToken =
							(StringToken)Expect(tokens, pos + 1, typeof(StringToken), startToken.LineNumber);
						pos++;
						Token endToken = Expect(tokens, pos + 1, typeof(ParensEndToken), stringToken.LineNumber);
						pos++;
						
						
						int positionSum = 0;
						for (int i = 0; i <= filePos; i++) {
							positionSum += lines[i].Length + 1;
						}
						if (positionSum == code.Length + 1) // final line might not have newline
							positionSum = code.Length;
						int index = code.LastIndexOf(stringToken.Text, positionSum, StringComparison.Ordinal);
						
						for (int i = filePos; i >= 0; i--) {
							positionSum -= lines[i].Length + 1;
							if (positionSum < index) {
								filePos = i;
								break;
							}
						}
						
						
						break;
					}
					case "open_brace_before":
					case "open_brace_after":
					case "close_brace_before":
					case "close_brace_after": {
						Token startToken = Expect(tokens, pos + 1, typeof(ParensStartToken), nameToken.LineNumber);
						pos++;
						Token endToken = Expect(tokens, pos + 1, typeof(ParensEndToken), startToken.LineNumber);
						pos++;

						List<PatchOperation> linePatches = unitOperations.GetPatchOperationsOrCreate(filePos);
						(OperationType type, char character) = BraceFunctionTypes[nameToken.Name];
						
						linePatches.Add(new PatchOperation($"{character}", critical, type, owner, patchIncrement));
						patchIncrement++;
						break;
					}
					case "write_before":
					case "write_replace":
					case "write_after": {
						Token startToken = Expect(tokens, pos + 1, typeof(ParensStartToken), nameToken.LineNumber);
						pos++;
						StringToken stringToken =
							(StringToken)Expect(tokens, pos + 1, typeof(StringToken), startToken.LineNumber);
						pos++;
						Token endToken = Expect(tokens, pos + 1, typeof(ParensEndToken), stringToken.LineNumber);
						pos++;

						List<PatchOperation> linePatches = unitOperations.GetPatchOperationsOrCreate(filePos);
						OperationType type = WriteFunctionTypes[nameToken.Name];
						linePatches.Add(new PatchOperation(stringToken.Text, critical, type, owner, patchIncrement));
						patchIncrement++;
						break;
					}
					default:
						throw new InvalidPatchException($"At line {nameToken.LineNumber}: unknown operation {nameToken.Name}");
				}
			}
			else {
				throw new Exception($"Unexpected token {token.GetType().Name} at line {token.LineNumber}");
			}

			pos++;
		}

		return pos;
	}

	public static void ExecutePatchSection(string patchSection, string target, string code, bool critical, PatchOwner owner, PatchesRecord record, ref int patchIncrement) {
		Token[] tokens = tokenize(patchSection);
		ExecutePatchSection(tokens, 0, target, code, critical, owner, record, ref patchIncrement);
	}

	public static void ApplyPatches(PatchesRecord record, CodeSource source, List<PatchOwner> order) {
		foreach (KeyValuePair<string, UnitOperations> recordPair in record.GetData()) {
			string file = recordPair.Key;
			
			UnitOperations unitOperations = recordPair.Value;
			string[] lines = unitOperations.Code.Split('\n');
			
			foreach (KeyValuePair<int, List<PatchOperation>> unitPatchPair in unitOperations.GetData()) {
				int line = unitPatchPair.Key;
				List<PatchOperation> operations = unitPatchPair.Value;
				List<PatchOperation> replacers = unitPatchPair.Value.Where(op => op.Type == OperationType.WriteReplace).ToList();
				
				// can't merge if we have two replacers on the same line and both are critical (can't choose)
				// if we have two non-critical patches then we can pick the one with higher priority
				bool invalidUnitPatch = (replacers.Count >= 2 && (replacers.Count(op => op.Critical) >= 2));
				if (invalidUnitPatch) {
					// TODO message
					continue;
				}
				
				replacers.Sort((a, b) => a.IsHigherPriorityThan(b, order));
				if (replacers.Count >= 2) {
					// pick out the last critical replacer, or the last non-critical replacer if we don't have any
					// (last has highest priority)
					PatchOperation chosenReplacer = replacers.LastOrDefault(op => op.Critical, replacers.Last(op => op.Type == OperationType.WriteReplace));
					operations.RemoveAll(op => replacers.Contains(op)
						&& op != chosenReplacer);
				}
				operations.Sort((a, b) => a.IsHigherPriorityThan(b, order));
				
				StringBuilder before = new StringBuilder();
				StringBuilder after = new StringBuilder();
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
						default:
							break;
					}
				}


				lines[line] = $"{before}{lines[line]}{after}";

			}
		
			string finalResult = string.Join("\n", lines);
			source.Replace(file, finalResult);
		}
	}
	
	private class Token(int lineNumber) {
		public readonly int LineNumber = lineNumber;
	}

	private class NumberToken(int number, int lineNumber) : Token(lineNumber) {
		public readonly int Number = number;
	}

	private class NameToken(string name, int lineNumber) : Token(lineNumber) {
		public readonly string Name = name;
	}

	private class SectionToken(string section, int lineNumber) : Token(lineNumber) {
		public readonly string Section = section;
	}

	class EqualsToken(int lineNumber) : Token(lineNumber);

	class ParensStartToken(int lineNumber) : Token(lineNumber);

	class ParensEndToken(int lineNumber) : Token(lineNumber);

	class StringToken(string text, int lineNumber) : Token(lineNumber) {
		public readonly string Text = text;
	}

	private static Token[] tokenize(string patch) {
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
					continue;
				}
				else if (patch[i + 1] == '*') {
					i += 2;
					while (i + 1 < patch.Length && !(patch[i] == '*' && patch[i + 1] == '/'))
						i++;
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

			if (c == '\'' || c == '@') {
				if (!string.IsNullOrWhiteSpace(build)) {
					tokens.Add(new NameToken(build, lineNumber));
				}

				if (c == '@') {
					i++;
					if (patch[i] != '\'') {
						throw new InvalidPatchException(
							$"At line {lineNumber}: Expected a string after the \'@\' character");
					}
				}

				int startCharPosition = i;
				
				bool stripNewlines = c != '@'; 
				

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
					throw new InvalidPatchException($"At line {lineNumber}: Reached end of file before string terminated");
					continue;
				}
				
				// go over the ' we're currently on
				i++;

				if (!string.IsNullOrWhiteSpace(text)) {
					tokens.Add(new StringToken(text, lineNumberStart));
				}

				continue;
			}

			build += c;
		}

		return tokens.ToArray();
	}
	
	private static Token Expect(Token[] tokens, int pos, Type type, int lastLineNumber) {
		if (pos >= tokens.Length)
			throw new Exception($"At line {lastLineNumber}: Expected {type.Name}, found end of file");
		Token token = tokens[pos];
		if (!type.IsInstanceOfType(token))
			throw new Exception($"At line {token.LineNumber}: Expected {type.Name}, found {token.GetType().Name}");
		return token;
	}


}

public class InvalidPatchException(string message) : Exception(message);

