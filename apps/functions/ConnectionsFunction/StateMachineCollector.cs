using Amazon;
using Amazon.Runtime;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

namespace CloudSpendGuard.Functions.Connections;

public static class StateMachineCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var stepFunctions = new AmazonStepFunctionsClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var machine in stepFunctions.Paginators.ListStateMachines(new ListStateMachinesRequest { MaxResults = 1000 }).StateMachines)
        {
            resources.Add(new Resource(
                machine.StateMachineArn,
                "states:statemachine",
                region,
                machine.Name,
                new Dictionary<string, string>
                {
                    ["type"] = machine.Type?.Value ?? "",
                    ["createdAt"] = machine.CreationDate?.ToString("O") ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
