using UnityEngine;
using T2G;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace T2G.Assistant
{
    public class PromptTranslator : TranslatorBase
    {
        const string url = "http://localhost:1234/v1/chat/completions";

        public override Task<(bool succeeded, List<Instruction> instructions)> Translate(string text)
        {
            var tcs = new TaskCompletionSource<(bool, List<Instruction>)>();

            LM_Client.Instance.Translate(Assistant.Instance, text, 
                (instructionList) => 
                {
                    tcs.SetResult((true, instructionList.instructions));
                }, 
                (error) => 
                {
                    Debug.LogError("[PromptTranslator] " + error);
                    tcs.SetResult((false, null));
                });           

            return tcs.Task;
        }
    }

    [System.Serializable]
    public class ChatRequest
    {
        public string model = "local-model";
        public Message[] messages;
        public float temperature = 0;
    }

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

}