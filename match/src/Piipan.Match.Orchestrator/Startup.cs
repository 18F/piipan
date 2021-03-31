using System;
using System.Net.Http;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Piipan.Shared.Authentication;

[assembly: FunctionsStartup(typeof(Piipan.Match.Orchestrator.Startup))]
namespace Piipan.Match.Orchestrator
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            ITokenProvider tokenProvider;
            var configuration = builder.GetContext().Configuration;

            if (configuration?["DEVELOPMENT"] == "true")
            {
                tokenProvider = new CliTokenProvider();
            }
            else
            {
                tokenProvider = new EasyAuthTokenProvider();
            }

            builder.Services.AddSingleton<IAuthorizedApiClient>((s) =>
            {
                return new AuthorizedJsonApiClient(new HttpClient(), tokenProvider);
            });

            builder.Services.AddSingleton<ITableStorage<QueryEntity>>((s) =>
            {
                const string LookupConnectionString = "LookupConnectionString";
                const string LookupTableName = "LookupTableName";

                var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable(LookupConnectionString));
                var tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
                var table = tableClient.GetTableReference(Environment.GetEnvironmentVariable(LookupTableName));

                return new LookupStorage(table);
            });
        }
    }
}
