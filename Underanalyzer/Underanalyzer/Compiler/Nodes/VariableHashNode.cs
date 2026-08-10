/*
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at https://mozilla.org/MPL/2.0/.
*/

using System.Collections.Generic;
using Underanalyzer.Compiler.Bytecode;
using Underanalyzer.Compiler.Lexer;
using Underanalyzer.Compiler.Parser;
using static Underanalyzer.IGMInstruction;

namespace Underanalyzer.Compiler.Nodes;

/// <summary>
/// Represents a variable hash (a reference to a variable by integer ID, as a compiler optimization).
/// </summary>
internal sealed class VariableHashNode(string name, bool isBuiltin, IToken? nearbyToken) : IASTNode
{
    /// <summary>
    /// Variable name being referenced by this hash.
    /// </summary>
    public string VariableName { get; } = name;

    /// <summary>
    /// Whether the variable name being referenced was found to be a built-in variable.
    /// </summary>
    public bool IsBuiltin { get; } = isBuiltin;

    /// <inheritdoc/>
    public IToken? NearbyToken { get; } = nearbyToken;

    /// <inheritdoc/>
    public IASTNode PostProcess(ParseContext context)
    {
        return this;
    }

    /// <inheritdoc/>
    public IASTNode Duplicate(ParseContext context)
    {
        return this;
    }

    /// <inheritdoc/>
    public void GenerateCode(BytecodeContext context)
    {
        context.EmitPushVariableHash(new VariableHashPatch(VariableName, IsBuiltin));
        context.PushDataType(DataType.Int32);
    }

    /// <inheritdoc/>
    public IEnumerable<IASTNode> EnumerateChildren()
    {
        return [];
    }   
}
