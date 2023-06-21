using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace FileTransferService.Functions
{
    public static class OrchestrateRetreiveScannedFile
    {
        [FunctionName("OrchestrateRetreiveScannedFile")]
        public static async Task Run([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            string blobName = context.GetInput<FileRetrievalInfo>().fileName;   
            await context.CallActivityAsync("ProcessCleanFile", blobName);
        }
    }
}