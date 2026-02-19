
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace T2G
{
    public class AssetSearchClient
    {
        public async static Awaitable<(bool succeeded, string[] assetPaths)> SearchAssets(string desc)
        {
            string tokens = "default";
            string assetType = string.Empty;        //TODO: search with asset type

            string url = $"http://localhost:5000/search?q={tokens}&type={assetType}";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                await request.SendWebRequest();

                while (request.result == UnityWebRequest.Result.InProgress)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string[] seperator = { "',", ", " };
                    string[] assetPaths = request.downloadHandler.text.Split(seperator, StringSplitOptions.RemoveEmptyEntries);

                    if (assetPaths.Length > 0)
                    {
                        return (true, assetPaths);
                    }
                }
            }
            return (false, null);
        }
    }
}