#if UNITY_EDITOR

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G
{
    [Executor(Actions.create_object)]
    public class Executor_CreateObject : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("name");
            string desc = instruction.desc;
            
            
            string asset = instruction.assets[0];

            GameObject newObj;

            newObj = GameObject.CreatePrimitive(PrimitiveType.Cube);

            newObj = GameObject.Instantiate<GameObject>(newObj);


            newObj = new GameObject(objName);


            return (true, $"{objName} was created.", null);
        }
    }
}

#endif