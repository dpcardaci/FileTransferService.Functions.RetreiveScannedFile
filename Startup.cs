using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Identity;

[assembly: FunctionsStartup(typeof(FileTransferService.Functions.Retrieve.Startup))]

namespace FileTransferService.Functions.Retrieve
{
    class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            string uploadAppConfigurationConnString = Environment.GetEnvironmentVariable("UploadAppConfigurationConnString");
            builder.ConfigurationBuilder.AddAzureAppConfiguration(options =>
            {
                options.Connect(uploadAppConfigurationConnString)
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential(
                                new DefaultAzureCredentialOptions
                                {
                                    AuthorityHost = AzureAuthorityHosts.AzureGovernment
                                }
                            ));
                    });
            });

            string retrieveAppConfigurationConnString = Environment.GetEnvironmentVariable("RetrieveAppConfigurationConnString");
            builder.ConfigurationBuilder.AddAzureAppConfiguration(options =>
            {
                options.Connect(retrieveAppConfigurationConnString)
                    .ConfigureKeyVault(kv =>
                    {
                        kv.SetCredential(new DefaultAzureCredential(
                                new DefaultAzureCredentialOptions
                                {
                                    AuthorityHost = AzureAuthorityHosts.AzureGovernment
                                }
                            ));
                    });
            });
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
        }
    }
}