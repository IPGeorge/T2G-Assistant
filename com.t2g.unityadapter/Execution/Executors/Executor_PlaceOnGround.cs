#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.place_on_ground)]
    public class Executor_PlaceOnGround : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("objectName");

            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var gameObject = rootObjects.Where(obj => obj.name == objName).FirstOrDefault();
            if (gameObject == null)
            {
                return (false, $"Couldn't find {objName}!", null);
            }
            Utils.PlaceOnGround(gameObject);
            Utils.UpdateEditorViews();
            await Task.Delay(100);
            return (true, $"{objName} is put down.", null);
        }
    }
}

#endif