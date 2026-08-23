/*
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at https://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using Underanalyzer.Compiler.Nodes;
using Underanalyzer.Compiler.Parser;

namespace Underanalyzer.Compiler;

internal static class PrefixPostfixRewriteHelper
{
    /// <summary>
    /// Recursively removes prefix/postfix side effects from the supplied node, following official GM rules (which stops at certain points).
    /// </summary>
    /// <remarks>
    /// Can also provide an output list for any side effect statements that are found (which will be duplicated).
    /// </remarks>
    private static IASTNode RemoveSideEffects(ParseContext context, IASTNode node, List<IASTNode>? sideEffectOutput)
    {
        switch (node)
        {
            case PrefixNode prefix:
                // This is a side effect
                if (sideEffectOutput is not null)
                {
                    PrefixNode sideEffectPrefix = prefix.Duplicate(context) as PrefixNode ?? throw new Exception("Duplicated prefix is no longer a prefix");
                    sideEffectPrefix.IsStatement = true;
                    sideEffectOutput.Add(sideEffectPrefix);
                }

                // Replace with simple + or - to get rid of side effect
                return new BinaryChainNode(
                    prefix.NearbyToken,
                    [prefix.Expression, new NumberNode(1, prefix.NearbyToken)],
                    [prefix.IsIncrement ? BinaryChainNode.BinaryOperation.Add : BinaryChainNode.BinaryOperation.Subtract]);
            case PostfixNode postfix:
                // This is a side effect
                if (sideEffectOutput is not null)
                {
                    PostfixNode sideEffectPostfix = postfix.Duplicate(context) as PostfixNode ?? throw new Exception("Duplicated postfix is no longer a postfix");
                    sideEffectPostfix.IsStatement = true;
                    sideEffectOutput.Add(sideEffectPostfix);
                }

                // Completely get rid of ++/-- to get rid of side effect
                return postfix.Expression;

            case SimpleFunctionCallNode funcCall:
                for (int i = 0; i < funcCall.Arguments.Count; i++)
                {
                    funcCall.Arguments[i] = RemoveSideEffects(context, funcCall.Arguments[i], sideEffectOutput);
                }
                return funcCall;
            case FunctionCallNode funcCall:
                funcCall.Expression = RemoveSideEffects(context, funcCall.Expression, sideEffectOutput);
                for (int i = 0; i < funcCall.Arguments.Count; i++)
                {
                    funcCall.Arguments[i] = RemoveSideEffects(context, funcCall.Arguments[i], sideEffectOutput);
                }
                return funcCall;
            case DotVariableNode dotVariable:
                dotVariable.LeftExpression = RemoveSideEffects(context, dotVariable.LeftExpression, sideEffectOutput);
                return dotVariable;
            case AccessorNode accessor:
                accessor.AccessorExpression = RemoveSideEffects(context, accessor.AccessorExpression, sideEffectOutput);
                if (accessor.AccessorExpression2 is not null)
                {
                    accessor.AccessorExpression2 = RemoveSideEffects(context, accessor.AccessorExpression2, sideEffectOutput);
                }
                return accessor;
            case UnaryNode unary:
                unary.Expression = RemoveSideEffects(context, unary.Expression, sideEffectOutput);
                return unary;
            case BinaryChainNode binary:
                for (int i = 0; i < binary.Arguments.Count; i++)
                {
                    binary.Arguments[i] = RemoveSideEffects(context, binary.Arguments[i], sideEffectOutput);
                }
                return binary;
            case ConditionalNode conditional:
                conditional.Condition = RemoveSideEffects(context, conditional.Condition, sideEffectOutput);
                conditional.TrueExpression = RemoveSideEffects(context, conditional.TrueExpression, sideEffectOutput);
                conditional.FalseExpression = RemoveSideEffects(context, conditional.FalseExpression, sideEffectOutput);
                return conditional;
            case NullishCoalesceNode nullish:
                nullish.Left = RemoveSideEffects(context, nullish.Left, sideEffectOutput);
                nullish.Right = RemoveSideEffects(context, nullish.Right, sideEffectOutput);
                return nullish;
            case NewObjectNode newObj:
                newObj.Expression = RemoveSideEffects(context, newObj.Expression, sideEffectOutput);
                for (int i = 0; i < newObj.Arguments.Count; i++)
                {
                    newObj.Arguments[i] = RemoveSideEffects(context, newObj.Arguments[i], sideEffectOutput);
                }
                return newObj;

            default:
                return node;
        }
    }

    /// <summary>
    /// Duplicates the provided expression node, returning a version with prefix/postfix side effects removed (as per offical GM rules, which are not exhaustive).
    /// </summary>
    /// <remarks>
    /// Can optionally provide an output list for all side effect nodes removed in this process, which will be duplicated.
    /// </remarks>
    public static IASTNode DuplicateAndRemoveSideEffects(ParseContext context, IASTNode node, List<IASTNode>? sideEffectOutput = null)
    {
        IASTNode duplicated = node.Duplicate(context);
        duplicated = RemoveSideEffects(context, duplicated, sideEffectOutput);
        return duplicated;
    }
}
