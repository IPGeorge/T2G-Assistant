using System.Threading.Tasks;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor(T2G.Actions.clear)]

    public class Executor_Clear : ExecutorBase
    {
        public override async Task<(bool succeeded, 
            string message,
            List<Response.Result> results, 
            List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            ChatBotUI.Instance.ClearChat();
            await Task.Delay(100);
            return (true, "Cleared!", null, null);
        }
    }
}