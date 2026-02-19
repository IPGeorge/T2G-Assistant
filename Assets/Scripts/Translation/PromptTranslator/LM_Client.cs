using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using T2G;

namespace T2G.Assistant
{
    public sealed class LM_Client
    {
        private static readonly Lazy<LM_Client> _instance = new Lazy<LM_Client>(() => new LM_Client());
        public static LM_Client Instance => _instance.Value;

        private readonly JsonSerializerSettings _jsonSettings;

        private LM_Client()
        {
            _jsonSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new InstructionConverter() },
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
        }

        // -------------------------
        // Config
        // -------------------------
        public string Endpoint { get; set; } = "http://localhost:1234/v1/chat/completions";
        public string ModelName { get; set; } = "local-model";
        public float Temperature { get; set; } = 0.0f;

        /// <summary>
        /// If parsing fails, how many repair attempts to run (recommended: 1).
        /// </summary>
        public int MaxRepairAttempts { get; set; } = 1;

        // -------------------------
        // Public API
        // -------------------------
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

        // -------------------------
        // Main Flow
        // -------------------------
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

            // 1) Normal request
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

                        // Validation failed: return immediately (or you can also repair here if you want)
                        onError?.Invoke("Validation failed:\n" + validationError + "\nRaw output:\n" + content);
                        return;
                    }

                    // Parsing failed -> attempt repair if enabled
                    if (MaxRepairAttempts <= 0)
                    {
                        onError?.Invoke(parseError + "\nRaw output:\n" + content);
                        return;
                    }

                    // Start a repair coroutine chain
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

                // If you ever want >1 repair attempt:
                // runner.StartCoroutine(RepairThenParseCoroutine(repairedContent, attemptLeft, onSuccess, onError));
                onError?.Invoke("Parse failed after repair attempt(s).\n" + parseError + "\nRepaired output:\n" + repairedContent);
                },
                onError: onError
            );
        }

        // -------------------------
        // Network: OpenAI-compatible chat call
        // -------------------------
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

        // -------------------------
        // Prompts
        // -------------------------
        private string BuildSystemPrompt()
        {
            return
    @"You translate user requests into JSON instructions for Unity tooling.

CRITICAL OUTPUT RULES (STRICT):
- Output ONLY the JSON payload.
- Do NOT include explanations, comments, or prose.
- Do NOT wrap JSON in ``` code fences.
- Do NOT prepend text like 'Here is the JSON'.
- If you output anything other than raw JSON, the system will fail.

ACTION SELECTION RULES (very important):
- Use create_project ONLY when the user explicitly asks for a NEW project (keywords: 'new', 'create').
- Use init_project when the user asks to initialize / setup / configure an existing project(keywords: 'init', 'initialize', 'configure').
- If the user says 'initialize' but also says 'new project', choose create_project.

Return either:
(A) InstructionList:
{ ""type"": ""InstructionList"", ""instructions"": [ Instruction, ... ] }

OR

(B) Instruction:
{
  ""type"": ""Instruction"",
  ""action"": ""..."",
  ""state"": ""init|Local|raw|resolved"",
  ""desc"": ""..."",
  ""parameters"": [ { ""name"": """", ""value"": ... } ],
  ""assets"": [ { ""name"": """", ""value"": ... } ],
  ""instructions"": [ ... ]
}

SCHEMA RULES:
- 'parameters' MUST be an array of { name, value }.
- 'assets' MUST be an array of { name, value } (use [] if none).
- value may be string, number, boolean, array, or object.

NORMALIZATION:
- Treat '|' as '\' in Windows paths (C:|UnityGames → C:\UnityGames).
- Escape backslashes in JSON strings (C:\\UnityGames).

ACTION MUST be one of the allowed actions:
" + ActionCatalog.BuildActionSpecForSystemPrompt() + @"

If the request cannot be mapped, output:
{
  ""type"": ""Instruction"",
  ""action"": ""unknown"",
  ""state"": ""Invalid"",
  ""desc"": ""Unsupported request"",
  ""parameters"": [ { ""name"": ""reason"", ""value"": ""..."" } ],
  ""assets"": [],
  ""instructions"": []
}";
        }

        private string BuildRepairSystemPrompt()
        {
            // Keep this short and very strict; repair prompts work best when minimal.
            return
@"You are a JSON repair tool.

Return ONLY valid JSON.
No prose, no explanations, no markdown, no ``` fences.
Output must be either an InstructionList or Instruction, using keys:
type, action, state, desc, parameters, assets, instructions.

parameters/assets must be arrays of { name, value }.
value may be string, number, boolean, array, or object.";
        }

        private string BuildRepairUserPrompt(string originalOutput)
        {
            return
@"Fix the following content so that the final output is ONLY a valid JSON payload (InstructionList or Instruction).
Remove any prose, remove ``` fences, and ensure JSON is valid.

CONTENT TO FIX:
" + originalOutput;
        }

        // -------------------------
        // Parsing + Validation
        // -------------------------
        private bool TryParsePayload(string text, out InstructionList list, out string error)
        {
            list = null;
            error = null;

            // Robust: strip prose/fences and extract first JSON
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

                if (!ActionCatalog.AllowedActions.Contains(ins.action))
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