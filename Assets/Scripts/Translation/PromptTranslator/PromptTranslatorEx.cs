using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using T2G;
using System.Threading.Tasks;

namespace T2G.Assistant
{
    public class PromptTranslatorEx : TranslatorBase
    {
        const string url = "http://localhost:1234/v1/chat/completions";

        public override Task<(bool succeeded, List<Instruction> instructions)> Translate(string text)
        {
            var tcs = new TaskCompletionSource<(bool, List<Instruction>)>();

            LM_ClientEx.Instance.Translate(Assistant.Instance, text, 
                (instructionList) => 
                {
                    tcs.SetResult((true, instructionList.instructions));
                }, 
                (error) => 
                {
                    Debug.LogError("[PromptTranslatorEx] " + error);
                    tcs.SetResult((false, null));
                });           

            return tcs.Task;
        }
    }

    public sealed class LM_ClientEx
    {
        private static readonly Lazy<LM_ClientEx> _instance = new Lazy<LM_ClientEx>(() => new LM_ClientEx());
        public static LM_ClientEx Instance => _instance.Value;

        private readonly JsonSerializerSettings _jsonSettings;

        private LM_ClientEx()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new InstructionConverter() },
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
        }

        public string Endpoint { get; set; } = "http://localhost:1234/v1/chat/completions";
        public string ModelName { get; set; } = "local-model";
        public float Temperature { get; set; } = 0.0f;
        public int MaxRepairAttempts { get; set; } = 1;

        public void Translate(
            MonoBehaviour runner,
            string prompt,
            Action<InstructionList> onSuccess,
            Action<string> onError)
        {
            if (runner == null) 
            { 
                onError?.Invoke("Runner is null."); 
                return; 
            }
            runner.StartCoroutine(TranslateCoroutine(runner, prompt, onSuccess, onError));
        }

        private IEnumerator TranslateCoroutine(
            MonoBehaviour runner,
            string prompt,
            Action<InstructionList> onSuccess,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                onError?.Invoke("Prompt is empty.");
                yield break;
            }

            yield return SendChatCompletion(
                systemPrompt: BuildSystemPrompt(),
                userPrompt: prompt,
                onResult: content =>
                {
                    if (TryParsePayload(content, out var list, out var parseError))
                    {
                        Normalize(list);
                        if (Validate(list, out var validationError))
                        {
                            onSuccess?.Invoke(list);
                            return;
                        }
                        onError?.Invoke("Validation failed:\n" + validationError + "\nRaw output:\n" + content);
                        return;
                    }

                    if (MaxRepairAttempts <= 0)
                    {
                        onError?.Invoke(parseError + "\nRaw output:\n" + content);
                        return;
                    }

                    runner.StartCoroutine(RepairThenParseCoroutine(
                        originalOutput: content,
                        attemptLeft: MaxRepairAttempts,
                        onSuccess: onSuccess,
                        onError: onError));
                },
                onError: onError
            );
        }

        private IEnumerator RepairThenParseCoroutine(
            string originalOutput,
            int attemptLeft,
            Action<InstructionList> onSuccess,
            Action<string> onError)
        {
            string repairSystem = BuildRepairSystemPrompt();
            string repairUser = BuildRepairUserPrompt(originalOutput);

            yield return SendChatCompletion(
                systemPrompt: repairSystem,
                userPrompt: repairUser,
                onResult: repairedContent =>
                {
                    if (TryParsePayload(repairedContent, out var list, out var parseError))
                    {
                        Normalize(list);
                        if (Validate(list, out var validationError))
                        {
                            onSuccess?.Invoke(list);
                            return;
                        }
                        onError?.Invoke("Validation failed after repair:\n" + validationError + "\nRepaired output:\n" + repairedContent);
                        return;
                    }

                    attemptLeft--;
                    if (attemptLeft <= 0)
                    {
                        onError?.Invoke("Parse failed after repair.\n" + parseError + "\nRepaired output:\n" + repairedContent);
                        return;
                    }

                    onError?.Invoke("Parse failed after repair attempt(s).\n" + parseError + "\nRepaired output:\n" + repairedContent);
                },
                onError: onError
            );
        }

        private IEnumerator SendChatCompletion(
            string systemPrompt,
            string userPrompt,
            Action<string> onResult,
            Action<string> onError)
        {
            var requestBody = new OA_ChatCompletionRequest
            {
                model = ModelName,
                temperature = Temperature,
                messages = new List<OA_Message>
                {
                    new OA_Message { role = "system", content = systemPrompt },
                    new OA_Message { role = "user", content = userPrompt }
                }
            };

            string bodyJson = JsonConvert.SerializeObject(requestBody, _jsonSettings);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);

            using var req = new UnityWebRequest(Endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"HTTP error: {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            OA_ChatCompletionResponse completion;
            try
            {
                completion = JsonConvert.DeserializeObject<OA_ChatCompletionResponse>(req.downloadHandler.text, _jsonSettings);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse chat response wrapper: {e.Message}\nRaw:\n{req.downloadHandler.text}");
                yield break;
            }

            if (completion?.choices == null || completion.choices.Count == 0 || completion.choices[0]?.message == null)
            {
                onError?.Invoke("No model choices returned.");
                yield break;
            }

            string content = completion.choices[0].message.content?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                onError?.Invoke("Model returned empty content.");
                yield break;
            }

            onResult?.Invoke(content);
        }

        private string BuildSystemPrompt()
        {
            return @"You translate user requests into JSON instructions for Unity game development.

OUTPUT RULES:
- Output ONLY the JSON payload
- No explanations, comments, or prose
- No markdown code fences (```)
- No prefix text like 'Here is the JSON'

Return either:
(A) InstructionList: { ""type"": ""InstructionList"", ""instructions"": [ Instruction, ... ] }
(B) Instruction: { ""type"": ""Instruction"", ""action"": ""..."", ""state"": ""init|Local|raw|resolved"", ""desc"": ""..."", ""parameters"": [ { ""name"": """", ""value"": ... } ], ""assets"": [], ""instructions"": [] }

SCHEMA:
- 'parameters' MUST be an array of { name, value }
- 'assets' MUST be an array of strings (use [] if none)
- value may be string, number, boolean, array, or object
- state: init=local command, Local=local execution, raw=needs resolution, resolved=ready to execute

FEW-SHOT EXAMPLES:

User: Create a cube named Box
Output: {""type"":""Instruction"",""action"":""create_object"",""state"":""Raw"",""parameters"":[{""name"":""Name"",""value"":""Box""},{""name"":""desc"",""value"":""cube""}],""assets"":[],""instructions"":[]}

User: Add a Camera component to Player
Output: {""type"":""Instruction"",""action"":""add_component"",""state"":""Resolved"",""parameters"":[{""name"":""objName"",""value"":""Player""},{""name"":""component"",""value"":""Camera""}],""assets"":[],""instructions"":[]}

User: Create a new space called MainLevel
Output: {""type"":""Instruction"",""action"":""create_space"",""state"":""Resolved"",""parameters"":[{""name"":""spaceName"",""value"":""MainLevel""}],""assets"":[],""instructions"":[]}

User: Go to space GameScene
Output: {""type"":""Instruction"",""action"":""goto_space"",""state"":""Resolved"",""parameters"":[{""name"":""spaceName"",""value"":""GameScene""}],""assets"":[],""instructions"":[]}

User: Delete the object Enemy
Output: {""type"":""Instruction"",""action"":""delete_object"",""state"":""Resolved"",""parameters"":[{""name"":""Name"",""value"":""Enemy""}],""assets"":[],""instructions"":[]}

User: Set position to (1,2,3) for Player
Output: {""type"":""Instruction"",""action"":""set_property"",""state"":""Resolved"",""parameters"":[{""name"":""Name"",""value"":""Player""},{""name"":""Component"",""value"":""Transform""},{""name"":""Property"",""value"":""position""},{""name"":""Type"",""value"":""Vector3""},{""name"":""Value"",""value"":[1,2,3]}],""assets"":[],""instructions"":[]}

User: Remove the Rigidbody component from Ball
Output: {""type"":""Instruction"",""action"":""remove_component"",""state"":""Resolved"",""parameters"":[{""name"":""objName"",""value"":""Ball""},{""name"":""componentType"",""value"":""Rigidbody""}],""assets"":[],""instructions"":[]}

User: Attach Player to Platform
Output: {""type"":""Instruction"",""action"":""attach_to"",""state"":""Resolved"",""parameters"":[{""name"":""childName"",""value"":""Player""},{""name"":""parentName"",""value"":""Platform""}],""assets"":[],""instructions"":[]}

If request cannot be mapped, output:
{ ""type"": ""Instruction"", ""action"": ""unknown"", ""state"": ""Invalid"", ""desc"": ""Unsupported request"", ""parameters"": [{ ""name"": ""reason"", ""value"": ""..."" }], ""assets"": [], ""instructions"": [] }";
        }

        private string BuildRepairSystemPrompt()
        {
            return @"You are a JSON repair tool.

Return ONLY valid JSON.
No prose, no explanations, no markdown, no ``` fences.
Output must be either an InstructionList or Instruction, using keys:
type, action, state, desc, parameters, assets, instructions.

parameters/assets must be arrays of { name, value }.
value may be string, number, boolean, array, or object.";
        }

        private string BuildRepairUserPrompt(string originalOutput)
        {
            return @"Fix the following content so that the final output is ONLY a valid JSON payload (InstructionList or Instruction).
Remove any prose, remove ``` fences, and ensure JSON is valid.

CONTENT TO FIX:
" + originalOutput;
        }

        private bool TryParsePayload(string text, out InstructionList list, out string error)
        {
            list = null;
            error = null;

            string json = JsonExtraction.ExtractFirstJson(text);
            if (string.IsNullOrEmpty(json))
            {
                error = "Could not find JSON in model output.";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                error = $"Invalid JSON after extraction: {e.Message}\nExtracted:\n{json}";
                return false;
            }

            string type = (string)root["type"];

            if (string.Equals(type, "InstructionList", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    list = root.ToObject<InstructionList>(JsonSerializer.Create(_jsonSettings));
                    return true;
                }
                catch (Exception e)
                {
                    error = $"Failed to parse InstructionList: {e.Message}";
                    return false;
                }
            }

            if (string.Equals(type, "Instruction", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var inst = root.ToObject<Instruction>(JsonSerializer.Create(_jsonSettings));
                    list = new InstructionList
                    {
                        type = "InstructionList",
                        instructions = new List<Instruction> { inst }
                    };
                    return true;
                }
                catch (Exception e)
                {
                    error = $"Failed to parse Instruction: {e.Message}";
                    return false;
                }
            }

            error = $"Unknown payload type '{type ?? "(null)"}'.";
            return false;
        }

        private static void Normalize(InstructionList list)
        {
            list.instructions ??= new List<Instruction>();

            foreach (var ins in list.instructions)
            {
                if (ins == null) continue;

                ins.type ??= "Instruction";
                if (ins.parameters == null) ins.parameters = new List<ValuePair>();
                if (ins.assets == null) ins.assets = new List<string>();

                if (ins.instructions != null)
                {
                    foreach (var child in ins.instructions)
                    {
                        if (child == null) continue;
                        child.type ??= "Instruction";
                        child.parameters ??= new List<ValuePair>();
                        child.assets ??= new List<string>();
                    }
                }
            }
        }

        private static bool Validate(InstructionList list, out string error)
        {
            error = null;

            if (list?.instructions == null)
            {
                error = "InstructionList.instructions is null.";
                return false;
            }

            for (int i = 0; i < list.instructions.Count; i++)
            {
                var ins = list.instructions[i];
                if (ins == null)
                {
                    error = $"Instruction[{i}] is null.";
                    return false;
                }

                if (!string.Equals(ins.type, "Instruction", StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Instruction[{i}] has invalid type '{ins.type}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ins.action))
                {
                    error = $"Instruction[{i}] missing action.";
                    return false;
                }

                if (string.Equals(ins.action, "unknown", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ActionCatalog.IsValidAction(ins.action))
                {
                    error = $"Instruction[{i}] action '{ins.action}' is not allowed.";
                    return false;
                }

                ins.parameters ??= new List<ValuePair>();
                ins.assets ??= new List<string>();
            }

            return true;
        }
    }
}
