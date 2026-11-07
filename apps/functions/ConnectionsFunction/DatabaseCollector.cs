using Amazon;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class DatabaseCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var rds = new AmazonRDSClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var database in rds.Paginators.DescribeDBInstances(new DescribeDBInstancesRequest()).DBInstances)
        {
            var tags = (database.TagList ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
            var details = new Dictionary<string, string>
            {
                ["engine"] = database.Engine ?? "",
                ["engineVersion"] = database.EngineVersion ?? "",
                ["instanceClass"] = database.DBInstanceClass ?? "",
                ["status"] = database.DBInstanceStatus ?? "",
                ["storageGiB"] = database.AllocatedStorage?.ToString() ?? "",
                ["storageType"] = database.StorageType ?? "",
                ["multiAZ"] = database.MultiAZ == true ? "true" : "false",
                ["createdAt"] = database.InstanceCreateTime?.ToString("O") ?? "",
            };

            if (database.DBClusterIdentifier is not null)
            {
                details["clusterId"] = database.DBClusterIdentifier;
            }

            resources.Add(new Resource(
                database.DBInstanceArn,
                "rds:db",
                region,
                database.DBInstanceIdentifier,
                details,
                tags,
                default,
                null,
                null));
        }

        return resources;
    }
}
