using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using T2G;
using UnityEngine;

namespace T2G.Assistant
{
    public class LMStudioTranslator : TranslatorBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpointUrl;
        private readonly string _modelName;
        private readonly JsonSerializerSettings _jsonSettings;

        public LMStudioTranslator(string baseUrl = "http://localhost:1234", string modelName = "qwen2.5-7b-instruct")
        {
            _httpClient = new HttpClient();
            _endpointUrl = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";
            _modelName = modelName;

            _jsonSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new InstructionConverter() },
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
        }

        public override async Task<(bool succeeded, List<Instruction> instructions)> Translate(string text)
        {
            Instruction instruction = await TranslatePromptAsync(text);
            if (instruction == null)
                return (false, null);

            return (true, new List<Instruction> { instruction });
        }

        public async Task<Instruction> TranslatePromptAsync(string userPrompt, List<object> conversationHistory = null)
        {
            string projectName = Assistant.Instance?.GameProject?.ProjectName ?? "";
            string projectPath = Assistant.Instance?.GameProject?.ProjectPath ?? "";
            string systemPrompt = $@"You are a natural language to structured JSON command compiler for Unity game development.

CRITICAL RULES:
1. Output ONLY the JSON payload. No explanations, no markdown code fences, no prefix text.
2. Single action -> type=0, multiple actions -> type=1 with 'instructions' array.
3. If all parameters are known, set state=1 (Local). If missing crucial info, set state=2 (Raw) and ask in 'desc'.
4. The 'parameters' array must contain objects with 'name' and 'value' fields.

CURRENT PROJECT:
- Name: {projectName}
- Path: {projectPath}
When the user says ""current project"", ""this project"", or ""my project"", use these values.

AVAILABLE ACTIONS (use exact action name and parameter names):
- open_project: params={{ path (string), projectName (string) }}, state=Local
- create_project: params={{ path (string), projectName (string) }}, state=Local
- create_object: params={{ Name (string), desc? (string) }}, state=Raw
- add_component: params={{ objName (string), component (string) }}, state=Raw
- remove_component: params={{ objName (string), componentType (string) }}, state=Resolved
- set_property_value: params={{ name (string), propertyPath (string), value (string) }}, state=Resolved
- create_space: params={{ spaceName (string) }}, state=Resolved
- goto_space: params={{ spaceName (string) }}, state=Resolved
- destroy_object: params={{ name (string) }}, state=Resolved
- attach_object_to: params={{ childName (string), parentName (string) }}, state=Resolved
- detach_object_from: params={{ childName (string) }}, state=Resolved
- select_object: params={{ name (string) }}, state=Resolved

EXAMPLES:
User: open current project
Output: {{""type"":0,""action"":""open_project"",""state"":1,""parameters"":[{{""name"":""path"",""value"":""{projectPath}""}},{{""name"":""projectName"",""value"":""{projectName}""}}],""assets"":[],""instructions"":[]}}

User: Create a cube named Box
Output: {{""type"":0,""action"":""create_object"",""state"":2,""desc"":""cube"",""parameters"":[{{""name"":""Name"",""value"":""Box""}}],""assets"":[],""instructions"":[]}}

User: Create a new space called MainLevel
Output: {{""type"":0,""action"":""create_space"",""state"":3,""parameters"":[{{""name"":""spaceName"",""value"":""MainLevel""}}],""assets"":[],""instructions"":[]}}

User: Go to space GameScene
Output: {{""type"":0,""action"":""goto_space"",""state"":3,""parameters"":[{{""name"":""spaceName"",""value"":""GameScene""}}],""assets"":[],""instructions"":[]}}

If not mappable: {{""type"":0,""action"":""unknown"",""state"":-1,""desc"":""Unsupported request"",""parameters"":[],""assets"":[],""instructions"":[]}}";

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            if (conversationHistory != null)
            {
                messages.AddRange(conversationHistory);
            }
            else
            {
                messages.Add(new { role = "user", content = userPrompt });
            }

            var payload = new
            {
                model = _modelName,
                messages = messages,
                temperature = 0.0,
                response_format = new
                {
                    type = "json_object",
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            type = new { type = "integer", @enum = new[] { 0, 1 } },
                            action = new { type = "string" },
                            state = new { type = "integer" },
                            desc = new { type = "string" },
                            parameters = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string" },
                                        value = new { type = "string" }
                                    },
                                    required = new[] { "name", "value" }
                                }
                            },
                            assets = new
                            {
                                type = "array",
                                items = new { type = "string" }
                            },
                            instructions = new
                            {
                                type = "array",
                                items = new { type = "object" }
                            }
                        },
                        required = new[] { "type", "state" }
                    }
                }
            };

            try
            {
                string jsonBody = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(_endpointUrl, content);
                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();
                var responseDoc = JObject.Parse(responseString);

                string innerJson = responseDoc["choices"]?[0]?["message"]?["content"]?.ToString();
                if (string.IsNullOrEmpty(innerJson))
                {
                    Debug.LogError("[LMStudioTranslator] Model returned empty content.");
                    return null;
                }

                string extracted = JsonExtraction.ExtractFirstJson(innerJson);
                if (string.IsNullOrEmpty(extracted))
                {
                    Debug.LogError("[LMStudioTranslator] Could not extract JSON from model output.");
                    return null;
                }

                var instruction = JsonConvert.DeserializeObject<Instruction>(extracted, _jsonSettings);
                if (instruction == null)
                {
                    Debug.LogError("[LMStudioTranslator] Deserialized instruction is null.");
                    return null;
                }

                return instruction;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LMStudioTranslator] {ex.Message}");
                return null;
            }
        }
    }
}