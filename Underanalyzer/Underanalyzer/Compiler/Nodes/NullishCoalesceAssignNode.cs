/*
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at https://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Underanalyzer.Compiler.Bytecode;
using Underanalyzer.Compiler.Lexer;
using Underanalyzer.Compiler.Parser;
using static Underanalyzer.IGMInstruction;

namespace Underanalyzer.Compiler.Nodes;

/// <summary>
/// Represents a nullish coalesce assignment statement in the AST.
/// </summary>
internal sealed class NullishCoalesceAssignNode : IASTNode
{
    /// <summary>
    /// Expression being assigned to.
    /// </summary>
    public IAssignableASTNode Destination { get; private set; }

    /// <summary>
    /// Expression being assigned to, but with less side effects.
    /// </summary>
    public IASTNode? DestinationLessSideEffects { get; private set; } = null;

    /// <summary>
    /// Side effects that were removed from <see cref="DestinationLessSideEffects"/>.
    /// </summary>
    public List<IASTNode>? SideEffects { get; private set; } = null;

    /// <summary>
    /// The expression being evaluted and assigned to the destination.
    /// </summary>
    public IASTNode Expression { get; private set; }

    /// <inheritdoc/>
    public IToken? NearbyToken => Destination.NearbyToken;

    /// <summary>
    /// Creates a nullish coalesce assignment node from the given destination and expression.
    /// </summary>
    public NullishCoalesceAssignNode(IAssignableASTNode destination, IASTNode expression)
    {
        Destination = destination;
        Expression = expression;
    }

    private NullishCoalesceAssignNode(IAssignableASTNode destination, IASTNode? destinationLessSideEffects, List<IASTNode>? sideEffects, IASTNode expression)
    {
        Destination = destination;
        DestinationLessSideEffects = destinationLessSideEffects;
        SideEffects = sideEffects;
        Expression = expression;
    }

    /// <inheritdoc/>
    public IASTNode PostProcess(ParseContext context)
    {
        Destination = Destination.PostProcess(context) as IAssignableASTNode ?? throw new Exception("Destination no longer assignable");
        Expression = Expression.PostProcess(context);
        if (context.CompileContext.GameContext.UsingNewNullishAssignSideEffects)
        {
            SideEffects = new(4);
            DestinationLessSideEffects = PrefixPostfixRewriteHelper.DuplicateAndRemoveSideEffects(context, Destination, SideEffects);
        }

        return this;
    }

    /// <inheritdoc/>
    public IASTNode Duplicate(ParseContext context)
    {
        return new NullishCoalesceAssignNode(
            Destination.Duplicate(context) as IAssignableASTNode ?? throw new Exception("Destination no longer assignable"),
            DestinationLessSideEffects?.Duplicate(context),
            SideEffects = SideEffects is null ? null : [.. SideEffects.Select(sideEffect => sideEffect.Duplicate(context))],
            Expression.Duplicate(context)
        );
    }

    /// <inheritdoc/>
    public void GenerateCode(BytecodeContext context)
    {
        // Handle array copy-on-write
        bool canGenerateArrayOwners = context.CanGenerateArrayOwners;
        if (canGenerateArrayOwners)
        {
            if (ArrayOwners.ContainsArrayAccessor(Destination) || ArrayOwners.ContainsNewArrayLiteral(Expression) ||
                ArrayOwners.IsArraySetFunctionOrContainsSubLiteral(Destination))
            {
                context.CanGenerateArrayOwners = false;
                ArrayOwners.GenerateSetArrayOwner(context, Destination);
            }
        }

        // Push destination value first
        if (DestinationLessSideEffects is not null)
        {
            DestinationLessSideEffects.GenerateCode(context);
        }
        else
        {
            Destination.GenerateCode(context);
        }
        context.ConvertDataType(DataType.Variable);

        // Check if nullish; branch around right side (and assignment) if not
        context.Emit(ExtendedOpcode.IsNullishValue);
        SingleForwardBranchPatch skipRightSidePatch = new(context, context.Emit(Opcode.BranchFalse));

        // Right side (but remove nullish result from left side first)
        context.Emit(Opcode.PopDelete, DataType.Variable);
        Expression.GenerateCode(context);
        context.ConvertDataType(DataType.Variable);

        // Assign right side, then branch around removal of non-nullish destination value
        context.PushDataType(DataType.Variable);
        Destination.GenerateAssignCode(context);
        SingleForwardBranchPatch skipDestinationPopPatch = new(context, context.Emit(Opcode.Branch));

        // Remove non-nullish destination value from stack
        skipRightSidePatch.Patch(context);
        context.Emit(Opcode.PopDelete, DataType.Variable);
        if (SideEffects is not null)
        {
            // If destination with side effects was not evaluated, evaluate its side effects now
            foreach (IASTNode sideEffect in SideEffects)
            {
                sideEffect.GenerateCode(context);
            }
        }
        skipDestinationPopPatch.Patch(context);

        // Restore array owner state
        context.CanGenerateArrayOwners = canGenerateArrayOwners;
    }

    /// <inheritdoc/>
    public IEnumerable<IASTNode> EnumerateChildren()
    {
        if (DestinationLessSideEffects is not null)
        {
            yield return DestinationLessSideEffects;
        }
        yield return Destination;
        yield return Expression;
        if (SideEffects is not null)
        {
            foreach (IASTNode sideEffect in SideEffects)
            {
                yield return sideEffect;
            }
        }
    }
}
