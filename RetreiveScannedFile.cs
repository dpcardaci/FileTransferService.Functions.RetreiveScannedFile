using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace FileTransferService.Functions
{
    public class RetreiveScannedFile
    {
        [FunctionName("RetreiveScannedFile")]
        public async Task Run([BlobTrigger("%cleanfiles_container%/{name}", 
                        Connection = "uploadstorage_conn")] Stream myBlob, 
                        string name, 
                        [DurableClient] IDurableOrchestrationClient orchestrationClient,
                        ILogger log)
        {
            FileRetrievalInfo fileRetrievalInfo = new FileRetrievalInfo {fileName = name};
            await orchestrationClient.StartNewAsync("OrchestrateRetreiveScannedFile", fileRetrievalInfo);
        }
    }
}
