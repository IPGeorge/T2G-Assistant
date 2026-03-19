
#if UNITY_EDITOR

using System.Threading.Tasks;
using System.Collections.Generic;


namespace T2G
{
    [Executor(Actions.set_property)]

    public class Executor_Set : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("objName");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }

            string propertyName = instruction.parameters.GetString("property");
            string value = instruction.parameters.GetString("value");

            bool succeeded = GameObjectPropertySetter.SetProperty(obj, propertyName, value, out var resultMessage);
            if (succeeded)
            {
                Utils.UpdateEditorViews();
                await Task.Delay(100);
            }
            return (succeeded, resultMessage, null);
        }
    }
}

#endif
