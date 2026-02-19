using UnityEngine;
using T2G;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace T2G.Assistant
{
    public abstract class TranslatorBase
    {
        public abstract Task<(bool succeeded, List<Instruction> instructions)> Translate(string text);
    }
}
