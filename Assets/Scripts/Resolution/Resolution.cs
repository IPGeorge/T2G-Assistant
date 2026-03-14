using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G.Assistant
{
    public class Resolution
    {
        public async Awaitable<Instruction> Resolve(Instruction instruction)
        {
            switch (instruction.state)
            {
                case Instruction.eState.Resolved:
                    break;
                case Instruction.eState.Raw:
                    if (T2G.Utils.IsObjectDesc(instruction.desc) ||
                        T2G.Utils.IsPrimitiveDesc(instruction.desc, out var primitiveType))
                    {
                        instruction.state = Instruction.eState.Resolved;
                    }
                    else
                    {
                        var assetsArray = await Resolver.Resolve(instruction.desc);
                        if (assetsArray != null && assetsArray.Length > 0)
                        {
                            string[] seperator = { ",", ", " };
                            string[] assets = assetsArray[0].Split(seperator, StringSplitOptions.None);
                            instruction.assets = new List<string>(assets);
                            instruction.state = Instruction.eState.Resolved;
                        }
                    }
                    break;
                default:
                    break;
            }
            return instruction;
        }
    }

    public static class Resolver
    {
        public static async Awaitable<string[]> Resolve(string desc)
        {
            var response = await AssetSearchClient.SearchAssets(desc);
            if(response.succeeded)
            {
                return response.assetPaths;  
            }
            return null;
        }
    }
}