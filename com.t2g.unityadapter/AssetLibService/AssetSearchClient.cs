
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SimpleJSON;


namespace T2G
{
    public class AssetSearchClient
    {
        public static async Awaitable<(bool succeeded, string[] assetPaths)> SearchAssets(string desc, int timeoutSeconds = 30)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {

                string tokens = string.IsNullOrEmpty(desc.Trim()) ? "default" : desc;
                string assetType = string.Empty;        //TODO: search with asset type

                string url = $"http://localhost:5000/search?q={tokens}&type={assetType}";
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    try
                    {
                        await request.SendWebRequest();

                        while (!request.isDone && !cts.Token.IsCancellationRequested)
                        {
                            await Task.Yield();
                        }

                        if (cts.Token.IsCancellationRequested)
                        {
                            request.Abort();
                            Debug.LogWarning("Request time out! No response was received from the Content Library service.");
                            return (false, null);
                        }

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            string[] seperator = { "',", ", " };
                            string assetsJson = request.downloadHandler.text;
                            JSONObject jsonObj = JSON.Parse(assetsJson).AsObject;
                            if (jsonObj == null)
                            {
                                return (false, null);
                            }
                            JSONArray assetsArray = jsonObj["results"].AsArray;

                            if (assetsArray == null || assetsArray.Count == 0)
                            {
                                return (false, null);
                            }

                            string[] assets = new string[assetsArray.Count];
                            for(int i = 0; i < assetsArray.Count; ++i)
                            {
                                assets[i] = assetsArray[i];
                            }
                            return (true, assets);
                        }
                    }
                    catch(OperationCanceledException)
                    {
                        Debug.LogWarning("Request was canceled! No response was received from the Content Library service.");
                    }
                }
            }

            return (false, null);
        }
    }
}