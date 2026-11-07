using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class EbsSnapshotCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();
        var volumeIds = new HashSet<string>();
        var imageSnapshotIds = new HashSet<string>();

        await foreach (var volume in ec2.Paginators.DescribeVolumes(new DescribeVolumesRequest()).Volumes)
        {
            volumeIds.Add(volume.VolumeId);
        }

        await foreach (var image in ec2.Paginators.DescribeImages(new DescribeImagesRequest { Owners = ["self"] }).Images)
        {
            foreach (var mapping in image.BlockDeviceMappings ?? [])
            {
                if (mapping.Ebs?.SnapshotId is not null)
                {
                    imageSnapshotIds.Add(mapping.Ebs.SnapshotId);
                }
            }
        }

        await foreach (var snapshot in ec2.Paginators.DescribeSnapshots(new DescribeSnapshotsRequest { OwnerIds = ["self"] }).Snapshots)
        {
            var tags = (snapshot.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
            var details = new Dictionary<string, string>
            {
                ["snapshotId"] = snapshot.SnapshotId,
                ["volumeId"] = snapshot.VolumeId ?? "",
                ["sizeGiB"] = snapshot.VolumeSize?.ToString() ?? "",
                ["state"] = snapshot.State?.Value ?? "",
                ["storageTier"] = snapshot.StorageTier?.Value ?? "",
                ["startedAt"] = snapshot.StartTime?.ToString("O") ?? "",
                ["description"] = snapshot.Description ?? "",
                ["volumeExists"] = volumeIds.Contains(snapshot.VolumeId ?? "") ? "true" : "false",
                ["usedByImage"] = imageSnapshotIds.Contains(snapshot.SnapshotId) ? "true" : "false",
            };

            if (tags.Keys.Any(key => key.StartsWith("aws:backup:")) || details["description"].Contains("AWS Backup service"))
            {
                details["managedBy"] = "AWS Backup";
            }
            else if (tags.Keys.Any(key => key.StartsWith("aws:dlm:")))
            {
                details["managedBy"] = "Data Lifecycle Manager";
            }

            resources.Add(new Resource(
                $"arn:aws:ec2:{region}:{accountId}:snapshot/{snapshot.SnapshotId}",
                "ec2:snapshot",
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
