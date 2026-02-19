using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G.Assistant
{
    public class CmdTranslatorBase
    {
        public virtual (bool succeeded, List<Instruction> instructions) Translate((string name, string value)[] args)
        {
            return (false, null);
        }
    }
}