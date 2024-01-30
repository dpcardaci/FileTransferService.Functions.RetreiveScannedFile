using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Authorization.Models;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.AppService;
using System.Linq;
using System.Collections.Generic;
using Azure.Messaging.EventGrid;
using FileTransferService.Core;

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
            try
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
                string destBasePath = $"https://{destAccountName}.{baseStoragePath}";
                if (destBlobName == "__TESTING_FTS_TRANSFER_ERROR__.txt") { throw new Exception("Testing FTS Transfer Error"); }

                string srcPath = $"https://{srcAccountName}.{baseStoragePath}/{srcContainer}/{blobName}";

                Uri srcUri = new Uri(srcPath);
                Uri srcUriWithSas = new Uri($"{srcPath}?{srcContainerSas}");

                AzureSasCredential destCredential = new AzureSasCredential(destAccountSas);
                AzureSasCredential srcCredential = new AzureSasCredential(srcAccountSas);
                            
                BlobClient srcClient = new BlobClient(srcUri, srcCredential);
                var metadata = srcClient.GetProperties().Value.Metadata;
                string userPrincipalName;
                string userId;

                if (metadata != null)
                {
                    if(metadata.TryGetValue("onbehalfofuserprincipalname", out userPrincipalName) 
                        && metadata.TryGetValue("onbehalfofuserid", out userId))
                    {
                        CreateUserContainer(userPrincipalName, userId);
                    }
                    else if (metadata.TryGetValue("userprincipalname", out userPrincipalName) 
                        && metadata.TryGetValue("userid", out userId))
                    {
                        CreateUserContainer(userPrincipalName, userId);
                    }
                }

                void CreateUserContainer(string userPrincipalName, string userId)
                {
                    destContainer = userPrincipalName;
                    destContainer = destContainer.Replace(".", "-");
                    destContainer = destContainer.Replace("@", "-");
                    destContainer = destContainer.ToLower();

                    BlobContainerClient destContainerClient = new BlobContainerClient(
                        new Uri($"{destBasePath}/{destContainer}"),
                        destCredential
                    );
                    destContainerClient.CreateIfNotExists();

                    string subscriptionId = _configuration["SubscriptionId"];
                    string retreiveUserRoleDefinitionId = _configuration["RetreiveUserRoleDefinitionId"];
                    string readUserRoleDefinitionId = _configuration["ReadUserRoleDefinitionId"];
                    string resourceGroupName = _configuration["RetrieveResourceGroupName"];

                    DefaultAzureCredential credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        AuthorityHost = AzureAuthorityHosts.AzureGovernment
                    });

                    ArmClient armClient = new ArmClient(credential,
                        subscriptionId,
                        new ArmClientOptions
                        {
                            Environment = ArmEnvironment.AzureGovernment
                        });

                    ResourceIdentifier storageAccountResourceIdentifier = StorageAccountResource.
                                                                            CreateResourceIdentifier(subscriptionId,
                                                                                                    resourceGroupName,
                                                                                                    destAccountName);

                    ResourceIdentifier blobContainerResourceIdentifier = BlobContainerResource.
                                                                            CreateResourceIdentifier(subscriptionId,
                                                                                                    resourceGroupName,
                                                                                                    destAccountName,
                                                                                                    destContainer);

                    ResourceIdentifier readUserRoleDefinitionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{readUserRoleDefinitionId}");
                    RoleAssignmentCreateOrUpdateContent readUserRoleAssignmentCreateOrUpdateContent = new RoleAssignmentCreateOrUpdateContent(readUserRoleDefinitionResourceId, Guid.Parse(userId));
                    string readUserRleAssignmentName = Guid.NewGuid().ToString();

                    ResourceIdentifier retreiveUserRoleDefinitionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/{retreiveUserRoleDefinitionId}");
                    RoleAssignmentCreateOrUpdateContent retreiveUserRoleAssignmentCreateOrUpdateContent = new RoleAssignmentCreateOrUpdateContent(retreiveUserRoleDefinitionResourceId, Guid.Parse(userId));
                    string retreiveUserRoleAssignmentName = Guid.NewGuid().ToString();

                    var storageAccountResource = armClient.GetStorageAccountResource(storageAccountResourceIdentifier);
                    storageAccountResource = storageAccountResource.Get();

                    var blobContainerResource = armClient.GetBlobContainerResource(blobContainerResourceIdentifier);
                    blobContainerResource = blobContainerResource.Get();

                    var storageAccountRoleAssignments = storageAccountResource.GetRoleAssignments().GetAll($"principalId eq '{userId}'");
                    if (!storageAccountRoleAssignments.Any(a => a.Data.RoleDefinitionId.ToString().Substring(a.Data.RoleDefinitionId.ToString().LastIndexOf("/") + 1) == readUserRoleDefinitionId
                                            && a.Data.Scope == storageAccountResource.Id))
                    {
                        storageAccountResource.GetRoleAssignments().CreateOrUpdate(WaitUntil.Completed, readUserRleAssignmentName, readUserRoleAssignmentCreateOrUpdateContent);
                    }

                    var containerRoleAssignments = blobContainerResource.GetRoleAssignments().GetAll($"principalId eq '{userId}'");
                    if (!containerRoleAssignments.Any(a => a.Data.RoleDefinitionId.ToString().Substring(a.Data.RoleDefinitionId.ToString().LastIndexOf("/") + 1) == retreiveUserRoleDefinitionId
                                            && a.Data.Scope == blobContainerResource.Id))
                    {
                        blobContainerResource.GetRoleAssignments().CreateOrUpdate(WaitUntil.Completed, retreiveUserRoleAssignmentName, retreiveUserRoleAssignmentCreateOrUpdateContent);
                    }
                }

                string destPath = $"{destBasePath}/{destContainer}/{destBlobName}";

                Uri destUri = new Uri(destPath);

                BlobClient destClient = new BlobClient(destUri, destCredential);
                CopyFromUriOperation copyFromUriOperation = destClient.StartCopyFromUri(srcUriWithSas);
                copyFromUriOperation.WaitForCompletion();

                var blobProperties = destClient.GetProperties().Value.Metadata;
                var destinationImpactLevel = EnvironmentImpactLevel.Parse<EnvironmentImpactLevel>(metadata["destinationimpactlevel"]);
                var transferId = metadata["transferid"];
                
                var originationDateTime = metadata["originationdatetime"];
                userPrincipalName = metadata["userprincipalname"];

                TransferInfo transferInfo = new TransferInfo
                {
                    TransferId = Guid.Parse(transferId),
                    OriginatingUserPrincipalName = userPrincipalName,
                    OriginationDateTime = DateTime.Parse(originationDateTime),
                    FileName = destBlobName,
                    FilePath = destContainer,
                    ImpactLevel = destinationImpactLevel
                };

                EventGridPublisherClient transferCompletedPublisher = new EventGridPublisherClient(
                    new Uri(_configuration["TransferCompletedTopicUri"]),
                    new Azure.AzureKeyCredential(_configuration["TransferCompletedTopicKey"]));

                EventGridEvent transferCompletedEventGridEvent = new EventGridEvent(
                    "FileTransferService/Transfer",
                    "Completed",
                    "1.0",
                    transferInfo
                );

                transferCompletedPublisher.SendEvent(transferCompletedEventGridEvent);

                srcClient.Delete();
            }
            catch (Exception ex)
            {
                string baseStoragePath = "blob.core.usgovcloudapi.net";

                string srcAccountName = _configuration["UploadStorageAccountName"];
                string srcAccountSas = _configuration["UploadStorageAccountSasToken"];
                string srcContainer = _configuration["UploadCleanFilesContainerName"];
                string srcContainerSas = _configuration["UploadCleanFilesContainerSasToken"];

                string srcPath = $"https://{srcAccountName}.{baseStoragePath}/{srcContainer}/{blobName}";

                Uri srcUri = new Uri(srcPath);
                Uri srcUriWithSas = new Uri($"{srcPath}?{srcContainerSas}");

                AzureSasCredential srcCredential = new AzureSasCredential(srcAccountSas);

                BlobClient srcClient = new BlobClient(srcUri, srcCredential);
                var metadata = srcClient.GetProperties().Value.Metadata;

                int destBlobNameStartIndex = 37;
                string destBlobName = blobName.Substring(destBlobNameStartIndex);

                TransferError transferError = new TransferError
                {
                    TransferId = Guid.Parse(metadata["transferid"]),
                    OriginatingUserPrincipalName = metadata["userprincipalname"],
                    OnBehalfOfUserPrincipalName = metadata"onbehalfofuserprincipalname"
                    OriginationDateTime = DateTime.Parse(metadata["originationdatetime"]),
                    FileName = destBlobName,
                    Message = ex.Message
                };               

                EventGridPublisherClient transferErrorPublisher = new EventGridPublisherClient(
                    new Uri(_configuration["TransferCompletedTopicUri"]),
                    new Azure.AzureKeyCredential(_configuration["TransferCompletedTopicKey"]));

                EventGridEvent transferErrorEventGridEvent = new EventGridEvent(
                    "FileTransferService/Transfer",
                    "Error",
                    "1.0",
                    transferError
                );

                transferErrorPublisher.SendEvent(transferErrorEventGridEvent);

                log.LogError(ex.Message);
                throw;
            }



        }
    }
}