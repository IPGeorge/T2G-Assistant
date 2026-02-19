using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace T2G.Assistant
{
    public class Resolution
    {
        public bool Resolve(ref Instruction instruction)
        {
            switch(instruction.state)
            {
                case Instruction.eState.Resolved:
                    return true;
                case Instruction.eState.Raw:
                    var assetList = Resolver.Resolve(instruction.desc);
                    if (assetList != null)
                    {
                        instruction.assets = new List<string>(assetList.Result);
                        instruction.state = Instruction.eState.Resolved;
                        return true;
                    }
                    break;
                default:
                    break;

            }
            return false;
        }
    }

    public static class Resolver
    {
        public static async Task<string[]> Resolve(string desc)
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