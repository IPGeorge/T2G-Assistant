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
            _translators.Add(new PromptTranslator());
        }

        public async Awaitable<List<Instruction>> Translate(string text)
        {
            for (int i = 0; i < _translators.Count; ++i)
            {
                var output = await _translators[i].Translate(text);

                if(output.succeeded && output.instructions != null)
                {
                    return output.instructions;
                }
            }
            return null;
        }
    }
}
