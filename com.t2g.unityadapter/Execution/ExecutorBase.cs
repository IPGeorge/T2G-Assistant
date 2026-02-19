using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G
{
    public class ExecutorBase
    {
        protected TaskCompletionSource<(bool, string, List<Instruction>)> _tcs;
        public virtual Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            _tcs = new TaskCompletionSource<(bool, string, List<Instruction>)>();
            _tcs.SetResult((false, "Done!", null));
            return _tcs.Task;
        }
    }
}
