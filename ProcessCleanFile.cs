using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Castle.Components.DictionaryAdapter;
using System.Collections.Generic;

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

            string srcPath = $"https://{srcAccountName}.{baseStoragePath}/{srcContainer}/{blobName}";

            Uri srcUri = new Uri(srcPath);
            Uri srcUriWithSas = new Uri($"{srcPath}?{srcContainerSas}");

            AzureSasCredential destCredential = new AzureSasCredential(destAccountSas);
            AzureSasCredential srcCredential = new AzureSasCredential(srcAccountSas);

            BlobClient srcClient = new BlobClient(srcUri, srcCredential);
            var metadata = srcClient.GetProperties().Value.Metadata;

            string destBasePath = $"https://{destAccountName}.{baseStoragePath}";

            if(metadata != null)
            {
                if(!String.IsNullOrEmpty(metadata["userprincipalname"]))
                {
                    destContainer = metadata["userprincipalname"];
                    BlobContainerClient destContainerClient = new BlobContainerClient(
                        new Uri($"{destBasePath}/{destContainer}"),
                        destCredential
                    );
                    destContainerClient.CreateIfNotExists();
                }
            }

            string destPath = $"{destBasePath}/{destContainer}/{destBlobName}";
            Uri destUri = new Uri(destPath);

            BlobClient destClient = new BlobClient(destUri, destCredential);
            CopyFromUriOperation copyFromUriOperation = destClient.StartCopyFromUri(srcUriWithSas);
            copyFromUriOperation.WaitForCompletion();
  
            srcClient.Delete();

        }
    }
}