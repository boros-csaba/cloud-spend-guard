using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class LogGroupCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var logs = new AmazonCloudWatchLogsClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var group in logs.Paginators.DescribeLogGroups(new DescribeLogGroupsRequest()).LogGroups)
        {
            resources.Add(new Resource(
                group.LogGroupArn ?? $"arn:aws:logs:{region}:{accountId}:log-group:{group.LogGroupName}",
                "logs:log-group",
                region,
                group.LogGroupName,
                new Dictionary<string, string>
                {
                    ["retentionDays"] = group.RetentionInDays?.ToString() ?? "",
                    ["storedBytes"] = group.StoredBytes?.ToString() ?? "",
                    ["class"] = group.LogGroupClass?.Value ?? "",
                    ["createdAt"] = group.CreationTime?.ToString("O") ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
