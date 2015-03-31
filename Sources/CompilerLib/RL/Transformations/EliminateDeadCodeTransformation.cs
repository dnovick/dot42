﻿using System.Linq;
using Dot42.CompilerLib.RL.Extensions;
using Dot42.DexLib;

namespace Dot42.CompilerLib.RL.Transformations
{
    /// <summary>
    /// Remove all nop's
    /// </summary>
    internal sealed class EliminateDeadCodeTransformation : IRLTransformation
    {
        public void Transform(Dex target, MethodBody body)
        {
#if DEBUG
            //return;
#endif
            var instructions = body.Instructions;

            bool[] reachable = new bool[instructions.Count];

            MarkReachable(0, instructions, reachable, body);

            bool removeNops = false, atEndOfMethod = true;
            for (int i = instructions.Count - 1; i > 0 ; --i)
            {
                if (!reachable[i] && atEndOfMethod)
                {
                    instructions.RemoveAt(i);
                }
                else if (!reachable[i])
                {
                    instructions[i].ConvertToNop();
                    removeNops = true;
                }
                else
                {
                    atEndOfMethod = false;
                }

            }
           
            if(removeNops)
                new NopRemoveTransformation().Transform(target, body);
        }

        private void MarkReachable(int index, InstructionList instructions, bool[] reachable, MethodBody body)
        {
            for (;index < instructions.Count; ++index)
            {
                if (reachable[index]) return; // marked before.

                var ins = instructions[index];
                reachable[index] = true;

                foreach (var ex in body.Exceptions.Where(p => p.TryStart.Index <= index && p.TryEnd.Index >= index))
                {
                    ex.Catches.ForEach(c => MarkReachable(c.Instruction.Index, instructions, reachable, body));
                    if (ex.CatchAll != null)
                        MarkReachable(ex.CatchAll.Index, instructions, reachable, body);
                }

                if (ins.Code.IsReturn() || ins.Code == RCode.Throw)
                    return;

                if (ins.Code == RCode.Goto)
                {
                    MarkReachable(((Instruction) ins.Operand).Index, instructions, reachable, body);
                    return;
                }

                if (ins.Code == RCode.Packed_switch || ins.Code == RCode.Sparse_switch)
                {
                    foreach (var target in (Instruction[]) ins.Operand)
                        MarkReachable(target.Index, instructions, reachable, body);
                }
                else if (ins.Code.IsBranch())
                {
                    MarkReachable(((Instruction) ins.Operand).Index, instructions, reachable, body);
                }
            }
        }
    }
}
