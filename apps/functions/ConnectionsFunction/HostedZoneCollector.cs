using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class HostedZoneCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var route53 = new AmazonRoute53Client(credentials, RegionEndpoint.USEast1);
        var resources = new List<Resource>();

        await foreach (var zone in route53.Paginators.ListHostedZones(new ListHostedZonesRequest()).HostedZones)
        {
            var id = zone.Id.Split('/').Last();

            resources.Add(new Resource(
                $"arn:aws:route53:::hostedzone/{id}",
                "route53:hostedzone",
                "global",
                zone.Name.TrimEnd('.'),
                new Dictionary<string, string>
                {
                    ["hostedZoneId"] = id,
                    ["private"] = zone.Config?.PrivateZone == true ? "true" : "false",
                    ["records"] = zone.ResourceRecordSetCount?.ToString() ?? "",
                    ["comment"] = zone.Config?.Comment ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
