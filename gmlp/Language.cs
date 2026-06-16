using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PatchCommon;

namespace gmlp;

/**
* This file contains most of the implementation of gmlpv1. It has been superceded by gmlpv2.
* 
* The language is mostly "fake": there are no types, the variables at the start are all hardcoded,
* functions aren't real and each expect specific tokens. It's very bad code.
*
*/
public static class Language {

	public static void FindIntentions(string patch, string patchName, PatchIntentionAggregate<UnitOperations> aggregate) {

		int patchIncrement = 0;
		
		Token[] tokens = Tokenize(patch);
		int pos = 0;
		while (pos < tokens.Length) {
			int lastLineNumber = tokens[pos].LineNumber;
			if (tokens[pos] is SectionToken metaSectionToken && metaSectionToken.Section == "meta") {
				(string[] targets, bool critical, bool last, pos) = ExecuteMetadataSection(tokens, pos + 1);
				
				foreach (string target in targets) {
					int p = pos;
					aggregate.AddIntention(last, new PatchIntention<UnitOperations>(target, patchName, critical, (record, source) => {
						CodeFile? codeFile = source.GetCodeFile(target);
						if (codeFile is null) {
							if (!critical)
								return;
							throw new PatchRealizationException($"Target \"{target}\" does not exist");
						}

						string code = codeFile.GetAsString();
						if (p < tokens.Length && tokens[p] is SectionToken patchSectionToken &&
								patchSectionToken.Section == "patch") {
							ExecutePatchSection(tokens, p + 1, code, critical, record, true,
								ref patchIncrement);
						}
						else {
							throw new PatchRealizationException($"Incomplete patch; meta section without patch section");
						}
					}));
				}
				pos++;
				while (pos < tokens.Length && tokens[pos] is not SectionToken) {
					pos++;
				}
			}
			else {
				throw new PatchBadIntentionsException(
					$"Expected \"meta:\" section at start of patch (line {lastLineNumber})");
			}
		}
	}
	
	

	private static (string[] target, bool critical, bool last, int pos) ExecuteMetadataSection(Token[] tokens, int pos) {
		bool critical = true;
		bool last = false;
		List<string> targets = [];
		List<string> variablesSeen = [];
		while (pos < tokens.Length) {
			Token token = tokens[pos];
			if (token is NameToken nameToken) {
				if (variablesSeen.Contains(nameToken.Name)) {
					throw new PatchRealizationException($"\"{nameToken.Name}\" has already been set; it cannot be set more than once");
				}
				variablesSeen.Add(nameToken.Name);
				switch (nameToken.Name) {
					case "last":
					case "critical": {
						Token equalsToken = Expect(tokens, pos + 1, typeof(EqualsToken), nameToken.LineNumber);
						pos++;
						NameToken valueToken =
							(NameToken)Expect(tokens, pos + 1, typeof(NameToken), equalsToken.LineNumber);
						pos++;

						if (valueToken.Name != "true" && valueToken.Name != "false") {
							throw new PatchRealizationException(
								$"At line {valueToken.LineNumber}: Expected \"true\" or \"false\"");
						}
						
						if (nameToken.Name == "critical")
							critical = valueToken.Name == "true";
						else
							last = valueToken.Name == "true";
						break;
					}
					case "targets": {
						Token equalsToken = Expect(tokens, pos + 1, typeof(EqualsToken), nameToken.LineNumber);
						pos++;
						int lastLineNumber = equalsToken.LineNumber;
						Token nextToken = Expect(tokens, pos + 1, typeof(Token), equalsToken.LineNumber);
						pos++;
						if (nextToken.GetType() == typeof(BraceStartToken)) {
							while (true) {
								StringToken targetToken =
									(StringToken)Expect(tokens, pos + 1, typeof(StringToken), lastLineNumber);
								targets.Add(targetToken.Text);
								pos++;
								
								Token commaOrBraceToken =
									(Token)Expect(tokens, pos + 1, typeof(Token), lastLineNumber);
								pos++;
								lastLineNumber = commaOrBraceToken.LineNumber;
								if (commaOrBraceToken is not CommaToken) {
									TokenTypeAssert(commaOrBraceToken, typeof(BraceEndToken));
									break;
								}
								TokenTypeAssert(commaOrBraceToken, typeof(CommaToken));
							}
							
						}
						else if (nextToken.GetType() == typeof(StringToken)) {
							targets.Add(((StringToken)nextToken).Text);
						}
						else {
							TokenTypeAssert(nextToken, typeof(StringToken));
						}

						break;
					}
					default:
						throw new PatchRealizationException(
							$"At line {nameToken.LineNumber}: invalid metadata variable {nameToken.Name}");
				}
			}
			else {
				break; // leave as soon as we stop seeing name tokens
			}

			pos++;
		}

		if (targets.Count == 0)
			throw new PatchRealizationException($"Meta section must contain at least one target in \"targets\"");

		return (targets.ToArray(), critical, last, pos);
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

	public static int ExecutePatchSection(Token[] tokens, int pos, string code, bool critical, UnitOperations unitOperations, bool bailOnSection, ref int patchIncrement) {
		// TODO make sure code has \n line endings only
		
		// we prepend a "\n" for line 0
		code = "\n" + code;
		string[] lines = code.Split('\n');

		List<Caret> carets = [new Caret(0, 0, lines.Length - 1)];
		string lastRemovalReason = "";
		while (pos < tokens.Length) {
			if (carets.Count == 0) {
				break;
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
						for (int j = caret.Line; j >= 0; j--) {
							if (lines[j].EndsWith('}'))
								stack++;
							else if (lines[j].EndsWith('{')) {
								if (stack == 0) {
									caret.StartLine = j;
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
				case "skip_scope": {
					(_, pos) = ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, []);
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						int stack = 0;
						for (int j = caret.Line; j <= caret.EndLine; j++) {
							if (lines[j].EndsWith('{'))
								stack++;
							else if (lines[j].EndsWith('}')) {
								stack--;
								if (stack <= 0) {
									caret.Line = j;
									goto Found;
								}
							}
						}
						lastRemovalReason = $"Removed because tried running skip_scope() while on line {caret.Line},  " 
						                    + $"but found no scope to skip within my scope (lines {caret.StartLine}-{caret.EndLine})";
						carets.RemoveAt(i);
						i--;
						continue;
						
						Found:
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
						int line = numberToken.Number;
						for (int i = 0; i < carets.Count; i++) {
							Caret caret = carets[i];
							int lineInFile = line + caret.StartLine;
							if (lineInFile > caret.EndLine || lineInFile < caret.StartLine) {
								lastRemovalReason = $"Removed because tried running move_to({numberToken.Number}), " 
									+ $"which lead me to line {lineInFile} in the file, which is out of my scope (lines {caret.StartLine}-{caret.EndLine})";
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
								lastRemovalReason = $"Removed because tried running move({numberToken.Number}) while on line {caret.Line}, " 
									+ $"which pushed me out of my scope (lines {caret.StartLine}-{caret.EndLine})";
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
								lastRemovalReason = $"Removed because tried running find_line_with('{stringToken.Text}') while on line {caret.Line} " +
									$"in the scope from lines {caret.StartLine}-{caret.EndLine}, but no line was found";
							}
							else {
								lastRemovalReason = $"Removed because tried running find_line_with('{stringToken.Text}') while on line {caret.Line}, " 
								+ $"but the line was found outside my scope (found at {newLinePos}, scope is from lines {caret.StartLine}-{caret.EndLine})";
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
								lastRemovalReason = $"Removed because tried running reverse_find_line_with('{stringToken.Text}') while on line {caret.Line} " +
									$"in the scope from lines {caret.StartLine}-{caret.EndLine}, but no line was found";
							}
							else {
								lastRemovalReason = $"Removed because tried running reverse_find_line_with('{stringToken.Text}') while on line {caret.Line}, " 
									+ $"but the line was found outside my scope (found at {newLinePos}, scope is from lines {caret.StartLine}-{caret.EndLine})";
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
				case "remove_caret_if_line_contains": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken));
					StringToken stringToken = (StringToken)parameters[0];
					for (int i = 0; i < carets.Count; i++) {
						Caret caret = carets[i];
						bool contains;
						if (stringToken.Regex) {
							Regex regex = new Regex(stringToken.Text, RegexOptions.CultureInvariant);
							Match match = regex.Match(lines[caret.Line]);
							contains = match.Success;
						}
						else {
							contains = lines[caret.Line].Contains(stringToken.Text);
						}

						if (contains) {
							lastRemovalReason = $"Removed because ran {nameToken.Name}('{stringToken.Text}'), and my line ({caret.Line}) did contain the text";
							carets.RemoveAt(i);
							i--;	
						}
					}

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
				
				case "write_replace":
					
				case "write_before":
				case "write_before_last":
					
				case "write":
				case "write_last":
					
				case "write_else":
				case "write_else_if": 
				
				case "write_and_condition":
				case "write_or_condition": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken));
					StringToken stringToken = (StringToken)parameters[0];

					for (int i = 0; i < carets.Count; i++) {
						int filePos = carets[i].Line;
						List<PatchOperation> linePatches = unitOperations.GetPatchOperationsOrCreate(filePos);
						OperationType type = PatchOperation.WriteOperationTypes[nameToken.Name];
						if (type == OperationType.WriteBefore) {
							if (filePos == carets[i].StartLine) {
								if (filePos == 0) {
									// seems like an easy mistake to make so a more precise error message is sent here
									throw new PatchRealizationException($"Invalid use of \"write_before\" on line 0, "
									                                + "which is the very start of the file; Carets cannot write there. "
									                                + "(Notably, you can still \"write\" on the last line of the file, it doesn't count outside the file.)");
								}

								throw new PatchRealizationException(
									$"Invalid use of \"write_before\" on line {filePos}, which is "
									+ "the starting line of this carets' scope. Carets cannot write outside their scope. "
									+ "(You can still \"write\" on the last line of your scope, as it is before the closing brace.)");
							}
							if (IsEndOfScope(lines[filePos])) {
								throw new PatchRealizationException(
									$"Invalid use of \"write_before\" on line {filePos}, which is "
									+ "the line of the closing brace of a scope; you must use \"write\" on the previous line "
									+ "if you wish to write inside that scope.");
							}
						}
						
							
						linePatches.Add(new PatchOperation(stringToken.Text, critical, type, patchIncrement));
						patchIncrement++;
					}

					break;
				}

				case "write_replace_substring": {
					(Token[] parameters, pos) =
						ExpectFunctionSignature(tokens, pos, nameToken.LineNumber, typeof(StringToken), typeof(StringToken));
					StringToken oldStringToken = (StringToken)parameters[0];
					StringToken newStringToken = (StringToken)parameters[1];

					for (int i = 0; i < carets.Count; i++) {
						int filePos = carets[i].Line;
						List<PatchOperation> linePatches = unitOperations.GetPatchOperationsOrCreate(filePos);
						linePatches.Add(new ReplaceSubstringPatchOperation(oldStringToken.Text, newStringToken.Text, oldStringToken.Regex, critical, patchIncrement));
						patchIncrement++;
					}

					break;
				}

				default:
					throw new PatchRealizationException(
						$"At line {nameToken.LineNumber}: unknown operation {nameToken.Name}");
			}


			pos++;
		}

		if (carets.Count == 0) {
			throw new PatchRealizationException($"All carets have been removed (have been sent out of their scope, or have searched for nonexistent lines). Log from last caret:\n{lastRemovalReason}");
		}

		return pos;
	}
	
	// TODO: Doesn't always work in gmlpweb because user can add a /* */ comment before the line, and a // after it for example
	private static bool IsEndOfScope(string line) {
		line = line.Trim();
		return line.StartsWith('}') || line.EndsWith('}');
	}
	

	public static PatchResults Apply(RecordAggregate<UnitOperations> record, CodeSource source) {
		PatchResults results = new PatchResults();
		foreach (KeyValuePair<string, UnitOperations> pair in record.GetChanges()) {
			string file = pair.Key;
			string[] lines = source.GetCodeFile(file)!.GetAsLines().Prepend("").ToArray();
			foreach (KeyValuePair<int, List<PatchOperation>> unitPatchPair in pair.Value.GetData()) {
				int line = unitPatchPair.Key;
				List<PatchOperation> operations = unitPatchPair.Value;
				operations.Sort((a, b) => a.IsHigherPriorityThan(b));
				
				StringBuilder after = new StringBuilder();
				StringBuilder before = new StringBuilder();
				StringBuilder afterElseIf = new StringBuilder();
				StringBuilder afterElse = new StringBuilder();
				StringBuilder conditions = new StringBuilder();
				int conditionsCount = 0;
	
				string lineToReinsert = lines[line];
				foreach (PatchOperation op in operations) {
					switch (op.Type) {
						case OperationType.WriteReplace:
							lineToReinsert = op.Text;
							break;
						case OperationType.WriteBefore:
							before.Insert(0, op.Text + "\n");
							break;
						case OperationType.Write:
							after.Insert(0, "\n" + op.Text);
							break;
						case OperationType.WriteAndCondition:
						case OperationType.WriteOrCondition:
							string operand = op.Type == OperationType.WriteAndCondition ? "&&" : "||";
							conditions.Append($"\n{operand} {op.Text})");
							conditionsCount++;
							break;
						case OperationType.WriteElseIf:
							afterElseIf.Insert(0, "\nelse if " + op.Text);
							break;
						case OperationType.WriteElse:
							afterElse.Insert(0, "\n" + op.Text);
							break;
						case OperationType.WriteReplaceSubstring:
							ReplaceSubstringPatchOperation rsop = (ReplaceSubstringPatchOperation)op;
							if (!rsop.Regex) {
								lineToReinsert = lineToReinsert.Replace(rsop.OldText, rsop.Text);
							}
							else {
								Regex regex = new Regex(rsop.OldText, RegexOptions.CultureInvariant);
								lineToReinsert = regex.Replace(lineToReinsert, rsop.Text);
							}
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


				if (conditionsCount > 0) {
					// we have to add a conditionsCount amount of parentheses before the expression. Finding it could be a little hard
					if (lineToReinsert.TrimStart().StartsWith("if")) {
						int index = lineToReinsert.IndexOf("if", StringComparison.Ordinal);
						lineToReinsert = lineToReinsert.Insert(index + "if".Length + 1,
							new string('(', conditionsCount));
					}
					else if (lineToReinsert.TrimStart().StartsWith("else if")) {
						int index = lineToReinsert.IndexOf("else if", StringComparison.Ordinal);
						lineToReinsert = lineToReinsert.Insert(index + "else if".Length + 1,
							new string('(', conditionsCount));
					}
					else if (lineToReinsert.TrimStart().StartsWith("while")) {
						int index = lineToReinsert.IndexOf("while", StringComparison.Ordinal);
						lineToReinsert = lineToReinsert.Insert(index + "while".Length + 1,
							new string('(', conditionsCount));
					}
					else {
						results.AddError(file,($"In {file}: Attempted to add a condition to an invalid line ({line})\nYou can only add conditions to if and while statements."));
					}
				}

				lines[line] = $"{before}{lineToReinsert}{conditions}{afterElseIf}{afterElseResult}{after}";

			}

			// remove starting newline
			string finalResult = string.Join("\n", lines).Remove(0, 1);
			results.AddResult(file, finalResult);
		}

		return results;
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
	
	public class CommaToken(int lineNumber) : Token(lineNumber);
	public class StringToken(string text, bool regex, int lineNumber) : Token(lineNumber) {
		public readonly string Text = text;
		public readonly bool Regex = regex;
	}

	public class BraceStartToken(int lineNumber) : Token(lineNumber);
	
	public class BraceEndToken(int lineNumber) : Token(lineNumber);
	
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
					throw new PatchRealizationException($"At line {lineNumber}: Expected a number after the sign");
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
			
			if (c == '{') {
				if (!string.IsNullOrWhiteSpace(build)) {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}

				tokens.Add(new BraceStartToken(lineNumber));
				continue;
			}
			if (c == '}') {
				if (!string.IsNullOrWhiteSpace(build)) {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}

				tokens.Add(new BraceEndToken(lineNumber));
				continue;
			}

			if (c == ',') {
				if (!string.IsNullOrWhiteSpace(build)) {
					tokens.Add(new NameToken(build, lineNumber));
					build = "";
				}
				
				tokens.Add(new CommaToken(lineNumber));
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
						throw new PatchRealizationException(
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

				bool seenBackslash = false;
				
				
				while (i + 1 < patch.Length) {
					char character = patch[i + 1];
					if (character == '\'') {
						if (!seenBackslash)
							break;
					}
					else if (character == '\n')
						lineNumber++;
					else if (character == '\\') {
						if (!seenBackslash && !regex) {
							seenBackslash = true;
							i++;
							continue;
						}
					}
					text += character;
					seenBackslash = false;
					i++;
				}
				
				while (stripNewlines && text.Length > 0 && text[text.Length - 1] == '\n') {
					text = text.Substring(0, text.Length - 1);
				}

				if (i >= patch.Length) {
					throw new PatchRealizationException(
						$"At line {lineNumber}: Reached end of file before string terminated");
				}

				// go over the ' we're currently on
				i++;


				tokens.Add(new StringToken(text, regex, lineNumberStart));

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
			if (i != types.Length - 1) {
				CommaToken comma = (CommaToken)Expect(tokens, pos, typeof(CommaToken), lastLineNumber);
				pos++;
				lastLineNumber = comma.LineNumber;
			}
		}

		Token parenthesisEnd = Expect(tokens, pos, typeof(ParensEndToken), lastLineNumber);
		return (ret, pos);
	}
	
	private static Token Expect(Token[] tokens, int pos, Type type, int lastLineNumber) {
		if (pos >= tokens.Length)
			throw new PatchRealizationException(
				$"At line {lastLineNumber}: Expected {GetHumanTypeName(type)}, found end of file");
		Token token = tokens[pos];
		if (!type.IsInstanceOfType(token))
			throw new PatchRealizationException(
				$"At line {token.LineNumber}: Expected {GetHumanTypeName(type)}, but found {GetHumanTypeName(token.GetType())}");
		return token;
	}
	private static void TokenTypeAssert(Token token, Type expected) {
		if (!expected.IsInstanceOfType(token))
			throw new PatchRealizationException(
				$"At line {token.LineNumber}: Expected {GetHumanTypeName(expected)}, but found {GetHumanTypeName(token.GetType())}");
	}
	
	private static string GetHumanTypeName(Type type) {
		switch (type.Name) {
			case "Token":
				return "any token";
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
			case "BraceStartToken":
				return "an opening brace";
			case "BraceEndToken":
				return "a closing brace";
			case "CommaToken":
				return "a comma";
			default:
				throw new UnreachableException();
		}
	}
}

