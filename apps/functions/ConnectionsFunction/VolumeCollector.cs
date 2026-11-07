using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class VolumeCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var volume in ec2.Paginators.DescribeVolumes(new DescribeVolumesRequest()).Volumes)
        {
            var tags = (volume.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
            var details = new Dictionary<string, string>
            {
                ["state"] = volume.State.Value,
                ["volumeType"] = volume.VolumeType.Value,
                ["sizeGiB"] = volume.Size?.ToString() ?? "",
                ["createdAt"] = volume.CreateTime?.ToString("O") ?? "",
                ["availabilityZone"] = volume.AvailabilityZone,
            };

            if (volume.Iops is not null)
            {
                details["iops"] = volume.Iops.Value.ToString();
            }

            if (volume.Attachments is [var attachment, ..])
            {
                details["attachedTo"] = attachment.InstanceId;
            }

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:volume/{volume.VolumeId}",
                "ec2:volume",
                region,
                tags.GetValueOrDefault("Name"),
                details,
                tags,
                default,
                null,
                null));
        }

        return resources;
    }
}
