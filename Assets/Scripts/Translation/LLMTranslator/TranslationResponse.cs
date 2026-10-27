using System;
using System.Collections.Generic;
using T2G;

namespace T2G.Assistant
{
    public enum TranslationResponseType 
    { 
        Instruction, 
        Clarification, 
        Conversation, 
        Error 
    }

    public class TranslationResponse
    {
        public TranslationResponseType Type { get; set; }
        public List<InstructionBase> Instructions { get; set; } = new List<InstructionBase>();
        public string Message { get; set; } = string.Empty;
        public bool HasInstructions => Type == TranslationResponseType.Instruction && Instructions != null && Instructions.Count > 0;
        public static TranslationResponse Error(string message) => new TranslationResponse { Type = TranslationResponseType.Error, Message = message };
    }
}
