using System.Threading.Tasks;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor("connect")]
    public class Executor_Connect : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            if(CommunicatorClient.Instance.IsConnected)
            {
                return (true, "Is already connected!", null);
            }

            await CommunicatorClient.Instance.StartClient();
            if (CommunicatorClient.Instance.IsConnected)
            {
                return (true, "Connected!", null);
            }
            else
            {
                return (false, "Failed to establish connection!", null);
            }
        }
    }
}
