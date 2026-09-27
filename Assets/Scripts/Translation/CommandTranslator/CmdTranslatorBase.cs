using System.Collections.Generic;
using System.Reflection;

namespace T2G.Assistant
{
    public class CmdTranslatorBase
    {
        public virtual (bool succeeded, InstructionBase instructionBase) Translate((string name, string value)[] args)
        {
            return (false, null);
        }

        public string GetActionName()
        {
            return GetType().GetCustomAttribute<CommandTranslatorAttribute>()?.Action;
        }
    }
}

public class InstructionIdGenerator
{
    private static InstructionIdGenerator _instance = null;
    public static InstructionIdGenerator Instance
    {
        get
        {
            if (_instance == null) 
                _instance = new InstructionIdGenerator();
            return _instance;
        }
    }

    private ulong _lastId = 0;

    public ulong LastId
    {
        set { _lastId = value; }
        get { return _lastId;  }
    }

    public string NextId()
    {
        return $"I{ ++_lastId:D8}";
    }
}