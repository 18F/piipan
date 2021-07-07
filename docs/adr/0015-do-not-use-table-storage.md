# 15. Do not use Table storage

Date: 2021-07-01

## Status

Accepted

## Context

We implemented the Lookup API using [Azure Table storage](https://docs.microsoft.com/en-us/azure/storage/tables/table-storage-overview) as the backing database. We did not strictly need a relational database solution, only a method of storing a unique identifier and the match request PII as a chunk of serialized data. This was an opportunity to evaluate Table storage as a quick, cheap, adaptable, and easy to implement NoSQL solution.

Through implementation we found Table storage lacks certain necessary out-of-the-box features. Namely, it does not offer a built-in automated backup mechanism.

## Decision

We will avoid Table storage as a datastore solution.

## Consequences

Moving forward, we will prefer PostgreSQL—[our default database](0004-default-database.md)—or investigate [Cosmos DB](https://docs.microsoft.com/en-us/azure/cosmos-db/introduction) as an alternative NoSQL solution.

Before deviating from PostgreSQL, we will evaluate the following criteria:

- Backup needs
- Geo-redundancy and high availability
- Cost
- Ease of incorporating managed identity for authentication
- US Government cloud support, at the API version level (e.g., support for our approach to [log streaming](../log-streaming.md#resource-configuration))
