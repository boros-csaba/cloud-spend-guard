using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class FunctionCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var lambda = new AmazonLambdaClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var function in lambda.Paginators.ListFunctions(new ListFunctionsRequest()).Functions)
        {
            resources.Add(new Resource(
                function.FunctionArn,
                "lambda:function",
                region,
                function.FunctionName,
                new Dictionary<string, string>
                {
                    ["runtime"] = function.Runtime?.Value ?? function.PackageType?.Value ?? "",
                    ["memoryMB"] = function.MemorySize?.ToString() ?? "",
                    ["architecture"] = function.Architectures is [var architecture, ..] ? architecture : "",
                    ["codeSizeBytes"] = function.CodeSize?.ToString() ?? "",
                    ["lastModified"] = function.LastModified ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
