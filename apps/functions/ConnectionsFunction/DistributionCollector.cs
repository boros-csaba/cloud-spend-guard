using Amazon;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class DistributionCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var cloudFront = new AmazonCloudFrontClient(credentials, RegionEndpoint.USEast1);
        var resources = new List<Resource>();

        await foreach (var distribution in cloudFront.Paginators.ListDistributions(new ListDistributionsRequest()).Items)
        {
            resources.Add(new Resource(
                distribution.ARN,
                "cloudfront:distribution",
                "global",
                distribution.Comment is { Length: > 0 } comment ? comment : null,
                new Dictionary<string, string>
                {
                    ["distributionId"] = distribution.Id,
                    ["domainName"] = distribution.DomainName,
                    ["aliases"] = string.Join(", ", distribution.Aliases?.Items ?? []),
                    ["origins"] = string.Join(", ", distribution.Origins?.Items?.Select(origin => origin.DomainName) ?? []),
                    ["enabled"] = distribution.Enabled == true ? "true" : "false",
                    ["status"] = distribution.Status,
                    ["priceClass"] = distribution.PriceClass?.Value ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
