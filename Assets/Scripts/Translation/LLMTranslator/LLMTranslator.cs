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
    public class LLMTranslator : TranslatorBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpointUrl;
        private readonly string _modelName;
        private readonly JsonSerializerSettings _jsonSettings;

        public LLMTranslator(string baseUrl = "http://localhost:1234", string modelName = "qwen2.5-7b-instruct")
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
2. Single action -> type=0 with action and parameters filled directly.
3. For a multi-step scene that requires multiple actions in sequence -> type=0, state=4 (Batch), and put all sub-instructions in the 'instructions' array. Each sub-instruction is a regular instruction with its own type, action, state, and parameters.
4. Sub-instructions inside a Batch are executed in array order.
5. If all parameters are known, set state=3 (Resolved). If missing crucial info, set state=2 (Raw) and ask in 'desc'.
6. The 'parameters' array must contain objects with 'name' and 'value' fields.
7. Position and scale values are strings in the format ""(x,y,z)"".

STATE VALUES:
- -1 = Invalid (unknown/unmappable request)
- 0 = Init (not yet processed)
- 1 = Local (executed locally, e.g. open_project)
- 2 = Raw (needs resolution, e.g. create_object with a desc like ""cube"")
- 3 = Resolved (ready to send to Unity, e.g. set_property, remove_component)
- 4 = Batch (contains sub-instructions in 'instructions' array)

CURRENT PROJECT:
- Name: {projectName}
- Path: {projectPath}
When the user says ""current project"", ""this project"", or ""my project"", use these values.

AVAILABLE ACTIONS (use exact action name and parameter names):
- open_project: params={{ path, projectName }}, state=1
- create_project: params={{ path, projectName }}, state=1
- init_project: params={{ path, projectName }}, state=1
- create_object: params={{ Name, desc }}, use desc for primitive type (cube, sphere, capsule, cylinder, plane, camera, light), state=2
- set_property: params={{ objName, property, value }}, use property=""position"" for position, property=""Transform.localScale"" for scale, property=""Transform.eulerAngles"" for rotation, state=3
- add_component: params={{ objName, type }}, state=3
- remove_component: params={{ objName, componentType }}, state=3
- delete_object: params={{ name }}, state=3
- select_object: params={{ name }}, state=3
- attach_to: params={{ childName, parentName }}, state=3
- detach_from: params={{ childName }}, state=3
- create_space: params={{ spaceName }}, state=3
- goto_space: params={{ spaceName }}, state=3
- save_space: params={{ spaceName }}, state=3

EXAMPLES:

User: open current project
Output: {{""type"":0,""action"":""open_project"",""state"":1,""parameters"":[{{""name"":""path"",""value"":""{projectPath}""}},{{""name"":""projectName"",""value"":""{projectName}""}}],""assets"":[],""instructions"":[]}}

User: Create a cube named Box
Output: {{""type"":0,""action"":""create_object"",""state"":2,""desc"":""cube"",""parameters"":[{{""name"":""Name"",""value"":""Box""}}],""assets"":[],""instructions"":[]}}

User: In this game scene, place a box on the ground in front of player view
Output: {{""type"":0,""action"":"""",""state"":4,""instructions"":[
{{""type"":0,""action"":""create_object"",""state"":2,""desc"":""cube"",""parameters"":[{{""name"":""Name"",""value"":""Ground""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""set_property"",""state"":3,""parameters"":[{{""name"":""objName"",""value"":""Ground""}},{{""name"":""property"",""value"":""position""}},{{""name"":""value"",""value"":""(0,0,0)""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""set_property"",""state"":3,""parameters"":[{{""name"":""objName"",""value"":""Ground""}},{{""name"":""property"",""value"":""Transform.localScale""}},{{""name"":""value"",""value"":""(10,1,10)""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""create_object"",""state"":2,""desc"":""cube"",""parameters"":[{{""name"":""Name"",""value"":""Box""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""set_property"",""state"":3,""parameters"":[{{""name"":""objName"",""value"":""Box""}},{{""name"":""property"",""value"":""position""}},{{""name"":""value"",""value"":""(0,0.5,3)""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""create_object"",""state"":2,""desc"":""camera"",""parameters"":[{{""name"":""Name"",""value"":""MainCamera""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""set_property"",""state"":3,""parameters"":[{{""name"":""objName"",""value"":""MainCamera""}},{{""name"":""property"",""value"":""position""}},{{""name"":""value"",""value"":""(0,1,-2)""}}],""assets"":[],""instructions"":[]}}
]}}

User: Replace the soldier's M4 rifle with an AK rifle
Output: {{""type"":0,""action"":"""",""state"":4,""instructions"":[
{{""type"":0,""action"":""remove_component"",""state"":3,""parameters"":[{{""name"":""objName"",""value"":""Soldier""}},{{""name"":""componentType"",""value"":""M4Rifle""}}],""assets"":[],""instructions"":[]}},
{{""type"":0,""action"":""add_component"",""state"":3,""parameters"":[{{""name"":""objName"",""value"":""Soldier""}},{{""name"":""type"",""value"":""AKRifle""}}],""assets"":[],""instructions"":[]}}
]}}

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