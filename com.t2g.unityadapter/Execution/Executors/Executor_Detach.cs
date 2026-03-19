#if UNITY_EDITOR

using System.Collections.Generic;
using System.Threading.Tasks;

namespace T2G
{
    [Executor(Actions.detach)]

    public class Executor_Dettach : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("name");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }
            obj.transform.parent = null;
            Utils.UpdateEditorViews();
            await Task.Delay(100);
            return (true, $"{objName} was dettached.", null);
        }
    }
}

#endif