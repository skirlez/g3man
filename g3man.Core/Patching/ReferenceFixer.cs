using Underanalyzer;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace g3man.Core.Patching;


// This seems to find references quite well. Using this, it should be possible to fix up references
// for projects above 2023.8.
public class ReferenceFixer {
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
}