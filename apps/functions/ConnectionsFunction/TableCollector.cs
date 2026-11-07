using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class TableCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var dynamoDb = new AmazonDynamoDBClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var name in dynamoDb.Paginators.ListTables(new ListTablesRequest()).TableNames)
        {
            var table = (await dynamoDb.DescribeTableAsync(new DescribeTableRequest { TableName = name })).Table;
            var details = new Dictionary<string, string>
            {
                ["status"] = table.TableStatus?.Value ?? "",
                ["billingMode"] = table.BillingModeSummary?.BillingMode?.Value ?? "PROVISIONED",
                ["sizeBytes"] = table.TableSizeBytes?.ToString() ?? "",
                ["itemCount"] = table.ItemCount?.ToString() ?? "",
                ["tableClass"] = table.TableClassSummary?.TableClass?.Value ?? "STANDARD",
                ["createdAt"] = table.CreationDateTime?.ToString("O") ?? "",
            };

            if (details["billingMode"] == "PROVISIONED")
            {
                details["readCapacity"] = table.ProvisionedThroughput?.ReadCapacityUnits?.ToString() ?? "";
                details["writeCapacity"] = table.ProvisionedThroughput?.WriteCapacityUnits?.ToString() ?? "";
            }

            resources.Add(new Resource(
                table.TableArn,
                "dynamodb:table",
                region,
                table.TableName,
                details,
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
