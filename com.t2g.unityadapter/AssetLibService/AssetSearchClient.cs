
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace T2G
{
    public class AssetSearchClient
    {
        public static async Awaitable<(bool succeeded, string[] assetPaths)> SearchAssets(string desc, int timeoutSeconds = 30)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {

                string tokens = "default";
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
                            string[] assetPaths = request.downloadHandler.text.Split(seperator, StringSplitOptions.RemoveEmptyEntries);

                            if (assetPaths.Length > 0)
                            {
                                return (true, assetPaths);
                            }
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