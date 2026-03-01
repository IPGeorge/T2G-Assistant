#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace T2G
{
    [Executor(Actions.delete_object)]
    public class Executor_DeleteObject : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("name");

            var gameObject = SceneManager.GetActiveScene()
                    .GetRootGameObjects()
                    .Where(obj => obj.name == objName)
                    .FirstOrDefault();

            if (gameObject == null)
            {
                return (true, $"Couldn't find {objName}!", null);
            }

            GameObject.Destroy(gameObject);
            return (true, $"{objName} was destroyed.", null);
        }
    }
}

#endif