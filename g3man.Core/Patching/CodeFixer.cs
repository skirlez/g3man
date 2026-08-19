using g3man.Core.Util;
using Underanalyzer;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace g3man.Core.Patching;



public class CodeFixer {
	
	// This seems to find references quite well. Using this, it should be possible to fix up references
	// for projects above 2023.8.
	public static void FixReferences(UndertaleData data) {
		foreach (UndertaleCode code in data.Code) {
			foreach (IGMInstruction instruction in code.Instructions) {
				if (instruction.Kind == IGMInstruction.Opcode.Extended
						&& instruction.ExtKind == IGMInstruction.ExtendedOpcode.PushReference
						&& instruction.ResolvedFunction == null) 
				{
					//Console.WriteLine(instruction.ValueLong);
				}
			}
		}
	}
	

	public static void RenameVariableOrCreateNew(UndertaleData data, UndertaleScript script, UndertaleVariable variable, string newName) {
		string name = variable.Name.Content;
		
		// must be a new string, it is plausible that other things in the project might share the name of a variable (in which case, they'll reuse a string instance)
		UndertaleString newString = data.Strings.MakeString(newName);
		
		if (variable.Occurrences == 1) {
			// no shenanigans possible unless occurances is inaccurate
			// TODO verify accuracy of Occurences
			variable.Name = newString;
			return;
		}

		UndertaleCode code = script.Code.ParentEntry ?? script.Code;
		// it's probably possible to trick this following code, but the important thing is that you have to do it deliberately.
		// we're trying to detect a snippet like:
/*
push.i [function]gml_Script_(OUR FUNCTION)
... some instructions here
pop.v.v [stacktop]self.(OUR FUNCTION)
*/

		bool seenFunctionPush = false;
		foreach (IGMInstruction instruction in code.Instructions) {
			continue;
			if (instruction.Kind == IGMInstruction.Opcode.Push) {
				IGMFunction? function = instruction.ResolvedFunction;
				if (function is null)
					continue;
				if (!function.Name.Content.StartsWith(DatafilePatcher.SCRIPT_PREFIX))
					continue;
				string functionName = function.Name.Content.Substring(DatafilePatcher.SCRIPT_PREFIX.Length);
				if (functionName != name)
					continue;
				seenFunctionPush = true;
				continue;
			}
			if (!seenFunctionPush)
				continue;
			if (instruction.Kind == IGMInstruction.Opcode.Pop && instruction.ReferenceVarType == IGMInstruction.VariableType.StackTop) {
				if (instruction.ResolvedVariable?.Name.Content != name)
					continue;
				
				UndertaleVariable newVariable = data.Variables.Define(newString, data.Strings.Count - 1,
					UndertaleInstruction.InstanceType.Self, false, data);
				
				((UndertaleInstruction)instruction).ValueVariable = newVariable;
				return;
			}
		}
		throw new Gexception($"Failed to auto-namespace function {name}. Please report this error!");
	}
}