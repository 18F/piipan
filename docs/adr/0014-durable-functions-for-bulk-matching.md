# 14. Durable functions for bulk matching

Date: 2021-05-17

## Status

Proposed

## Context

Our architecture includes a sub-system for performing bulk matching of participant records across all participating states. We desire an architecture for this sub-system that supports the following qualities:

- Adhere to our PaaS-exclusive philosophy, limiting the amount of human maintenance required over time and the scope of a prospective ATO
- Able to run bulk match process on both a scheduled and ad-hoc basis
- Strike an appropriate balance between cost and compute power
- Support potential future use of Privacy-Preserving Set Intersection (PPSI) when conducting bulk matches

Our research identified Azure Batch and Durable Functions as two potentially suitable services.

Azure Batch is a natural fit for our use case of bringing up large amounts of compute power on an ad-hoc or scheduled basis. However, it has the undesired qualities of using resources like VMs and self-contained executable applications which would break out of our PaaS-exclusive philosophy.

## Decision

We anticipate using Durable Functions, specifically the fan out/fan in pattern, to perform the bulk match process.

## Consequences

- We will be able to rely on our established methods of using managed identities to secure communication between internal resources.
- We will continue to exclusively rely on PaaS services.
- The fan out / fan in paradigm where an orchestrator stages data and parcels out comparison tasks fits both the current use case and potential expansion to a PPSI approach.
- Our use of the premier tier for Function Apps means there will be a baseline cost of ~$50/month in addition to the cost for resources used during the bulk match process.
- More work may be required to successfully manage and orchestrate the matching process (e.g., [managing connections](https://docs.microsoft.com/en-us/azure/azure-functions/manage-connections?toc=/azure/azure-functions/durable/toc.json), avoiding timeouts)

## Resources

- [Azure Batch documentation](https://docs.microsoft.com/en-us/azure/batch/)
- [Durable functions documentation](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=csharp)
- [Durable functions fan out / fan in pattern](https://docs.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-overview?tabs=csharp#fan-in-out)
- [Privacy-Preserving Set Intersection](https://www.cs.cmu.edu/~leak/papers/set-tech-full.pdf)
