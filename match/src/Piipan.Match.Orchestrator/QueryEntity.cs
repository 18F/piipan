using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace Piipan.Match.Orchestrator
{
    public class QueryEntity : TableEntity
    {
        // Parameterless constructor allows for `null` result when retrieving
        public QueryEntity() { }

        public QueryEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public string Body { get; set; }
    }
}
