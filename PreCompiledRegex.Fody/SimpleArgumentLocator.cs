using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrecompiledRegex.Fody
{
    // code inspired by http://cecil.googlecode.com/svn/trunk/decompiler/Cecil.Decompiler/Cecil.Decompiler.Cil/ControlFlowGraphBuilder.cs
    internal static class SimpleArgumentLocator
    {
        public static bool TryFindArgumentInstructions(Instruction call, out Instruction[] arguments, out string errorMessage)
        {
            var calledMethod = ((MethodReference)call.Operand).Resolve();
            var argumentCount = calledMethod.Parameters.Count + (calledMethod.IsStatic || calledMethod.IsConstructor ? 0 : 1);
            var argumentList = new List<Instruction>();

            var nextInstructionToAnalyze = call.Previous;
            var stackDelta = 0;
            while (argumentList.Count < argumentCount)
            {
                if (nextInstructionToAnalyze == null)
                {
                    arguments = null;
                    errorMessage = "Unexpected control flow: could not find enough arguments";
                    return false;
                }

                if (IsBlockDelimiter(nextInstructionToAnalyze))
                {
                    arguments = null;
                    errorMessage = "Control flow too complex: encountered conditional logic when determining arguments";
                    return false;
                }

                var popDelta = GetPopDelta(nextInstructionToAnalyze);
                if (popDelta == null)
                {
                    arguments = null;
                    errorMessage = $"Control flow too complex: encountered {nextInstructionToAnalyze.OpCode} instruction";
                    return false;
                }
                stackDelta -= popDelta.Value;

                var pushDelta = GetPushDelta(nextInstructionToAnalyze);
                for (var i = 0; i < pushDelta && argumentList.Count < argumentCount; ++i)
                {
                    ++stackDelta;
                    if (stackDelta == 1)
                    {
                        argumentList.Add(nextInstructionToAnalyze);
                        stackDelta = 0; // reset
                    }
                }

                nextInstructionToAnalyze = nextInstructionToAnalyze.Previous;
            }

            argumentList.Reverse(); // we built the list backwards

            // check for incoming jumps
            // todo not quite right... offset/getsize
            var minOffset = argumentList?[0].Offset ?? call.Offset;
            var maxOffset = call.Offset;
            foreach (var instruction in AllInstructions(call))
            {
                var operand = instruction.Operand;
                Instruction target;
                IEnumerable<Instruction> targets;
                if (
                    ((target = (operand as Instruction)) != null && target.Offset >= minOffset && target.Offset <= maxOffset)
                    || ((targets = (operand as IEnumerable<Instruction>)) != null && targets.Any(t => t.Offset >= minOffset && t.Offset <= maxOffset)))
                {
                    arguments = null;
                    errorMessage = "Control flow too complex: incoming jump found";
                    return false;
                }
            }

            arguments = argumentList.ToArray();
            errorMessage = null;
            return true;
        }

        private static IEnumerable<Instruction> AllInstructions(Instruction instruction)
        {
            var start = instruction;
            while (start.Previous != null) { start = start.Previous; }

            var next = start;
            do
            {
                yield return next;
                next = next.Next;
            }
            while (next != null);
        }

        private static int GetPushDelta(Instruction instruction)
        {
            var opCode = instruction.OpCode;
            switch (opCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    return 0;

                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;

                case StackBehaviour.Push1_push1:
                    return 2;

                case StackBehaviour.Varpush:
                    if (opCode.FlowControl == FlowControl.Call)
                    {
                        var method = (IMethodSignature)instruction.Operand;
                        return method.ReturnType.FullName == "System.Void" ? 0 : 1;
                    }
                    throw new NotSupportedException($"Unexpected flow control {opCode.FlowControl} for {instruction}");

                default:
                    throw new NotSupportedException($"Unexpected push behavior {opCode.StackBehaviourPush} for {instruction}");
            }
        }

        private static int? GetPopDelta(Instruction instruction)
        {
            var opCode = instruction.OpCode;
            switch (opCode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    return 0;
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                case StackBehaviour.Pop1:
                    return 1;

                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return 2;

                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;

                case StackBehaviour.PopAll:
                    return null;

                case StackBehaviour.Varpop:
                    if (opCode.FlowControl == FlowControl.Call)
                    {
                        var method = (IMethodSignature)instruction.Operand;
                        int count = method.Parameters.Count;
                        if (method.HasThis && OpCodes.Newobj.Value != opCode.Value)
                            ++count;

                        return count;
                    }

                    if (opCode.Code == Code.Ret)
                    {
                        return null;
                        //return signature.ReturnType.FullName == "System.Void" ? 0 : 1;
                    }

                    throw new NotSupportedException($"Unexpected varpop instruction {instruction}");

                default:
                    throw new NotSupportedException($"Unexpected pop behavior {opCode.StackBehaviourPop} for {instruction}");
            }
        }

        private static bool IsBlockDelimiter(Instruction instruction)
        {
            switch (instruction.OpCode.FlowControl)
            {
                case FlowControl.Break:
                case FlowControl.Branch:
                case FlowControl.Return:
                case FlowControl.Cond_Branch:
                    return true;
                default:
                    return false;
            }
        }
    }
}
