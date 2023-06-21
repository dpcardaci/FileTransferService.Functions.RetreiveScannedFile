using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FileTransferService.Functions
{
    public class ProcessCleanFile
    {

        [FunctionName("ProcessCleanFile")]
        public void Run([ActivityTrigger] string blobName, ILogger log) 
        {
            string baseStoragePath = "blob.core.usgovcloudapi.net";

            string destAccountName = Environment.GetEnvironmentVariable("destinationstorage_name");
            string destAccountSas = Environment.GetEnvironmentVariable("destinationstorage_sas");
            string destContainer = Environment.GetEnvironmentVariable("default_container");

            string srcAccountName = Environment.GetEnvironmentVariable("uploadstorage_name");
            string srcAccountSas = Environment.GetEnvironmentVariable("uploadstorage_sas");
            string srcContainer = Environment.GetEnvironmentVariable("cleanfiles_container");
            string srcContainerSas = Environment.GetEnvironmentVariable("cleanfiles_container_sas");

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