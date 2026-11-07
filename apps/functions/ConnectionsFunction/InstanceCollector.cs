using System.Globalization;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.AutoScaling;
using Amazon.AutoScaling.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static partial class InstanceCollector
{
    [GeneratedRegex(@"\((\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) GMT\)")]
    private static partial Regex StoppedAtPattern();

    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var warmPool = await WarmPoolInstanceIds(credentials, region);
        var resources = new List<Resource>();

        await foreach (var reservation in ec2.Paginators.DescribeInstances(new DescribeInstancesRequest()).Reservations)
        {
            foreach (var instance in reservation.Instances ?? [])
            {
                if (instance.State.Name == InstanceStateName.Terminated)
                {
                    continue;
                }

                var tags = (instance.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value);
                var details = new Dictionary<string, string>
                {
                    ["instanceId"] = instance.InstanceId,
                    ["instanceType"] = instance.InstanceType.Value,
                    ["state"] = instance.State.Name.Value,
                    ["lifecycle"] = instance.InstanceLifecycle?.Value ?? "on-demand",
                    ["launchedAt"] = instance.LaunchTime?.ToString("O") ?? "",
                };

                if (instance.Placement?.AvailabilityZone is { } availabilityZone)
                {
                    details["availabilityZone"] = availabilityZone;
                }

                if (instance.VpcId is not null)
                {
                    details["vpcId"] = instance.VpcId;
                    details["subnetId"] = instance.SubnetId;
                }

                if (instance.PublicIpAddress is not null)
                {
                    details["publicIp"] = instance.PublicIpAddress;
                }

                if (StoppedAt(instance.StateTransitionReason) is { } stoppedAt)
                {
                    details["stoppedAt"] = stoppedAt.ToString("O");
                }

                if (warmPool.Contains(instance.InstanceId))
                {
                    details["warmPool"] = "true";
                }

                resources.Add(new Resource(
                    $"arn:aws:ec2:{region}:{accountId}:instance/{instance.InstanceId}",
                    "ec2:instance",
                    region,
                    tags.GetValueOrDefault("Name"),
                    details,
                    tags,
                    default,
                    null,
                    null));
            }
        }

        return resources;
    }

    public static DateTimeOffset? StoppedAt(string? stateTransitionReason) =>
        stateTransitionReason is not null && StoppedAtPattern().Match(stateTransitionReason) is { Success: true } match
            ? DateTimeOffset.ParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            : null;

    private static async Task<HashSet<string>> WarmPoolInstanceIds(AWSCredentials credentials, string region)
    {
        using var autoScaling = new AmazonAutoScalingClient(credentials, RegionEndpoint.GetBySystemName(region));
        var ids = new HashSet<string>();

        await foreach (var group in autoScaling.Paginators.DescribeAutoScalingGroups(new DescribeAutoScalingGroupsRequest()).AutoScalingGroups)
        {
            if (group.WarmPoolConfiguration is null)
            {
                continue;
            }

            var request = new DescribeWarmPoolRequest { AutoScalingGroupName = group.AutoScalingGroupName };

            await foreach (var instance in autoScaling.Paginators.DescribeWarmPool(request).Instances)
            {
                ids.Add(instance.InstanceId);
            }
        }

        return ids;
    }
}
