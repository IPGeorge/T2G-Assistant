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
            var messages = conversationHistory ?? new List<object>
            {
                new { role = "user", content = userPrompt }
            };

            var payload = new
            {
                model = _modelName,
                messages = messages,
                temperature = 0.0
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