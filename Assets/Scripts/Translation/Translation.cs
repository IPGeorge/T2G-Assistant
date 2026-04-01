using System;
using System.Collections.Generic;
using UnityEngine;
using T2G;

namespace T2G.Assistant
{
    public class Translation
    {
         List<TranslatorBase> _translators = new List<TranslatorBase>();

        public Translation()
        {
            RegisterTranslators();
        }

        public void RegisterTranslators()
        {
            _translators.Add(new CommandTranslator());
            _translators.Add(new CommandTranslatorEx());
            _translators.Add(new PromptTranslator());
            _translators.Add(new PromptTranslatorEx());
        }

        public async Awaitable<List<Instruction>> Translate(string text)
        {
            for (int i = 0; i < _translators.Count; ++i)
            {
                Debug.Log($"[Translation] Trying translator {i}: {_translators[i].GetType().Name}");
                var output = await _translators[i].Translate(text);

                if(output.succeeded && output.instructions != null)
                {
                    Debug.Log($"[Translation] Translator {i} succeeded with {output.instructions.Count} instructions");
                    return output.instructions;
                }
                else
                {
                    Debug.Log($"[Translation] Translator {i} failed: succeeded={output.succeeded}, instructions={(output.instructions != null ? output.instructions.Count.ToString() : "null")}");
                }
            }
            return null;
        }
    }
}
