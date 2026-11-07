using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class AddressCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var response = await ec2.DescribeAddressesAsync(new DescribeAddressesRequest());
        var resources = new List<Resource>();

        foreach (var address in response.Addresses ?? [])
        {
            var tags = (address.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
            var details = new Dictionary<string, string>
            {
                ["publicIp"] = address.PublicIp,
                ["allocationId"] = address.AllocationId,
            };

            if (address.AssociationId is not null)
            {
                details["associationId"] = address.AssociationId;
            }

            if (address.InstanceId is not null)
            {
                details["instanceId"] = address.InstanceId;
            }

            if (address.NetworkInterfaceId is not null)
            {
                details["networkInterfaceId"] = address.NetworkInterfaceId;
            }

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:elastic-ip/{address.AllocationId}",
                "ec2:elastic-ip",
                region,
                tags.GetValueOrDefault("Name") ?? address.PublicIp,
                details,
                tags,
                default,
                null,
                null));
        }

        return resources;
    }
}
