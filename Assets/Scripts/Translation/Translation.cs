using System;
using System.Collections.Generic;
using UnityEngine;
using T2G;
using System.Threading.Tasks;

namespace T2G.Assistant
{
    public class Translation
    {
        static Translation _instance = null;
        public static Translation Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Translation();
                }
                return _instance;
            }
        }

        List<TranslatorBase> _translators = new List<TranslatorBase>();
        
        private LLMTranslator _llmTranslator;
        public bool AwaitingClarification => _llmTranslator != null && _llmTranslator.AwaitingClarification;


        public Translation()
        {
            RegisterTranslators();
        }

        public void RegisterTranslators()
        {
            _translators.Clear();
            _translators.Add(new CommandTranslator());
            _llmTranslator = new LLMTranslator();
            _translators.Add(_llmTranslator);
        }

        public async Awaitable<(TranslationResponseType responsType, List<InstructionBase> instructions, string message)> Translate(string text)
        {
            (bool succeeded, List<InstructionBase> instructions) output;
            for (int i = 0; i < _translators.Count; ++i)
            {
                Debug.Log($"[Translation] Trying translator {i}: {_translators[i].GetType().Name}");

                if (i == 0)
                {
                    output = await _translators[i].Translate(text);

                    if (output.succeeded && output.instructions != null)
                    {
                        Debug.Log($"[Translation] Translator {i} succeeded with {output.instructions.Count} instructions");
                        return (TranslationResponseType.Instruction, output.instructions, null);
                    }
                    else
                    {
                        Debug.Log($"[Translation] Translator {i} failed: succeeded={output.succeeded}, instructions={(output.instructions != null ? output.instructions.Count.ToString() : "null")}");
                    }
                }
                else
                {
                    TranslationResponse result = await TranslateWithDialogueAsync(_translators[i], text);

                    if (result == null)
                    {
                        return (TranslationResponseType.Error, null, null);
                    }

                    switch (result.Type)
                    {
                        case TranslationResponseType.Instruction:
                            if(result.HasInstructions && result.Instructions.Count > 0)
                            {
                                return (TranslationResponseType.Instruction, result.Instructions, null);
                            }
                            break;
                        case TranslationResponseType.Clarification:
                        case TranslationResponseType.Conversation:
                            return (TranslationResponseType.Conversation, null, result.Message);
                        default:
                            break;
                    }
                }
            }
            return (TranslationResponseType.Error, null, null);
        }

        // New API for the Chat UI: supports instructions, clarification, and conversation.
        public async Task<TranslationResponse> TranslateWithDialogueAsync(TranslatorBase translator, string text, string gameDescContext = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return TranslationResponse.Error("Empty prompt.");

            if (translator == null)
                return TranslationResponse.Error("LLMTranslator is not registered.");

            var llmTranslator = translator as LLMTranslator;

            // A follow-up answer belongs to the pending LLM dialogue. Do not let CommandTranslator intercept it.
            if (llmTranslator != null && AwaitingClarification)
            {
                Debug.Log("[Translation] Continuing pending clarification with LLMTranslator");
                return await llmTranslator.TranslateWithDialogueAsync(text, gameDescContext);
            }

            Debug.Log($"[Translation] Using translator: {translator.GetType().Name}");

            // The LLM has a richer response contract than TranslatorBase.Translate().
            if (llmTranslator != null)
            {
                TranslationResponse response = await llmTranslator.TranslateWithDialogueAsync(text, gameDescContext);
                Debug.Log($"[Translation] LLM response type: {response.Type}");
                return response;
            }
            else
            {
                var output = await translator.Translate(text);
                if (output.succeeded && output.instructions != null && output.instructions.Count > 0)
                {
                    Debug.Log($"[Translation] Translator {translator.GetType().Name} succeeded with {output.instructions.Count} instructions");
                    return new TranslationResponse
                    {
                        Type = TranslationResponseType.Instruction,
                        Instructions = output.instructions
                    };
                }

                return TranslationResponse.Error("No translator could handle the request.");
            }
        }

        public void CancelClarification()
        {
            _llmTranslator?.ClearPendingClarification();
        }
    }
}