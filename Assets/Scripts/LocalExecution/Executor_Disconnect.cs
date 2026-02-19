using System.Threading.Tasks;
using System.Collections.Generic;

namespace T2G.Assistant
{
    [LocalExecutor("disconnect")]
    public class Executor_Diconnect : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            if (!CommunicatorClient.Instance.IsConnected)
            {
                return (true, "Is already disconnected!", null);
            }

            CommunicatorClient.Instance.Disconnect();
            await Task.Yield();

            if (CommunicatorClient.Instance.IsConnected)
            {
                return (false, "Couldn't disconnect!", null);
            }
            else
            {
                return (true, "Disconnected!", null);
            }
        }
    }
}
