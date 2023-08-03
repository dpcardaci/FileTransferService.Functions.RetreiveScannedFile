using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace FileTransferService.Functions
{
    public class ProcessCleanFile
    {

        private readonly IConfiguration _configuration;
        public ProcessCleanFile(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [FunctionName("ProcessCleanFile")]
        public void Run([ActivityTrigger] string blobName, ILogger log) 
        {
            string baseStoragePath = "blob.core.usgovcloudapi.net";

            string destAccountName = _configuration["RetrieveStorageAccountName"];
            string destAccountSas = _configuration["RetrieveStorageAccountSasToken"];
            string destContainer = _configuration["RetrieveDefaultContainerName"];

            string srcAccountName = _configuration["UploadStorageAccountName"];
            string srcAccountSas = _configuration["UploadStorageAccountSasToken"];
            string srcContainer = _configuration["UploadCleanFilesContainerName"];
            string srcContainerSas = _configuration["UploadCleanFilesContainerSasToken"];

            int destBlobNameStartIndex = 37;
            string destBlobName = blobName.Substring(destBlobNameStartIndex);

            string destPath = $"https://{destAccountName}.{baseStoragePath}/{destContainer}/{destBlobName}";
            string srcPath = $"https://{srcAccountName}.{baseStoragePath}/{srcContainer}/{blobName}";

            Uri destUri = new Uri(destPath);
            Uri srcUri = new Uri(srcPath);
            Uri srcUriWithSas = new Uri($"{srcPath}?{srcContainerSas}");

            AzureSasCredential destCredential = new AzureSasCredential(destAccountSas);
            AzureSasCredential srcCredential = new AzureSasCredential(srcAccountSas);

            BlobClient destClient = new BlobClient(destUri, destCredential);
            CopyFromUriOperation copyFromUriOperation = destClient.StartCopyFromUri(srcUriWithSas);
            copyFromUriOperation.WaitForCompletion();

            BlobClient srcClient = new BlobClient(srcUri, srcCredential);
            srcClient.Delete();

        }
    }
}