using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class StackCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var cloudFormation = new AmazonCloudFormationClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var stack in cloudFormation.Paginators.DescribeStacks(new DescribeStacksRequest()).Stacks)
        {
            if (stack.ParentId is not null)
            {
                continue;
            }

            resources.Add(new Resource(
                stack.StackId,
                "cloudformation:stack",
                region,
                stack.StackName,
                new Dictionary<string, string>
                {
                    ["status"] = stack.StackStatus?.Value ?? "",
                    ["createdAt"] = stack.CreationTime?.ToString("O") ?? "",
                    ["updatedAt"] = stack.LastUpdatedTime?.ToString("O") ?? "",
                },
                (stack.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value),
                default,
                null,
                null));
        }

        return resources;
    }
}
