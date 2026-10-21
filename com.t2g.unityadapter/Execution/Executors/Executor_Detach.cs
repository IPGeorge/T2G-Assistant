#if UNITY_EDITOR

using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace T2G
{
    [Executor(Actions.detach_from)]

    public class Executor_Detach : ExecutorBase
    {
        public override async Task<(bool succeeded, 
            string message,
            List<Response.Result> results, 
            List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("Name");

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find {objName}!", null, null);
            }
            obj.transform.parent = null;
            Utils.UpdateEditorViews();
            await Task.Yield();

            string message = $"{objName} was dettached.";
            message += $"\nposition={obj.transform.localPosition};" +
                $"rotation={obj.transform.localRotation.eulerAngles};" +
                $"scale={obj.transform.localScale}";

            List<Response.Result> results = new List<Response.Result>();
            Transform objTransform = obj.transform;
            results.Add(new Response.Result()
            {
                name = "Position",
                type = "string",
                value = $"({objTransform.position.x}, {objTransform.position.y}, {objTransform.position.z})"
            });
            results.Add(new Response.Result()
            {
                name = "Rotation",
                type = "string",
                value = $"({objTransform.rotation.eulerAngles.x}, {objTransform.rotation.eulerAngles.y}, {objTransform.rotation.eulerAngles.z})"
            });
            results.Add(new Response.Result()
            {
                name = "Scale",
                type = "string",
                value = $"({objTransform.localScale.x}, {objTransform.localScale.y}, {objTransform.localScale.z})"
            });

            return (true, message, results, null);
        }
    }
}

#endif