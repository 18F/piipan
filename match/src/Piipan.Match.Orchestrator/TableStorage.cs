using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;

namespace Piipan.Match.Orchestrator
{
    public interface ITableStorage<T>
    {
        Task<T> InsertAsync(T entity);
        Task<T> RetrieveOrInsertAsync(T entity);
        Task<T> PointQueryAsync(string partitionKey, string rowKey);
    }

    public class LookupStorage : ITableStorage<QueryEntity>
    {
        private CloudTable _table;

        public LookupStorage(CloudTable table)
        {
            _table = table;
        }

        /// <summary>
        /// Insert a QueryEntity instance into storage
        /// </summary>
        /// <returns>Inserted entity as a QueryEntity instance</returns>
        /// <param name="entity">QueryEntity instance for insert</param>
        public async Task<QueryEntity> InsertAsync(QueryEntity entity)
        {
            var op = TableOperation.Insert(entity);
            var result = await _table.ExecuteAsync(op);

            return result.Result as QueryEntity;
        }

        /// <summary>
        /// Retrieve a given entity from storage and return it. If the entity
        /// does not exist in storage, insert it and return the result.
        /// </summary>
        /// <returns>Inserted or existing entity as QueryEntity</returns>
        /// <param name="entity">QueryEntity instance for insert</param>
        /// <remarks>
        /// If an entity is retrieved from storage but does not match the
        /// incoming entity (retrieved.Body != incoming.Body), this method
        /// throws a StorageException.
        /// </remarks>
        public async Task<QueryEntity> RetrieveOrInsertAsync(QueryEntity entity)
        {
            var stored = await PointQueryAsync(entity.PartitionKey, entity.RowKey);

            if (stored == null)
            {
                stored = await InsertAsync(entity);
            }
            else if (stored.Body != entity.Body)
            {
                throw new StorageException($"Row key {entity.RowKey} already exists for a non-matching query");
            }

            return stored;
        }

        /// <summary>
        /// Retrieve an entity from storage using a PartitionKey:RowKey pair.
        /// </summary>
        /// <returns>Retrieved entity as a QueryEntity instance, if one exists</returns>
        /// <param name="partitionKey">Entity's PartitionKey property</param>
        /// <param name="rowKey">Entity's RowKey property</param>
        public async Task<QueryEntity> PointQueryAsync(string partitionKey, string rowKey)
        {
            var op = TableOperation.Retrieve<QueryEntity>(partitionKey, rowKey);
            var result = await _table.ExecuteAsync(op);

            return result.Result as QueryEntity;
        }

    }
}
