using Amazon;
using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Runtime;
using Classic = Amazon.ElasticLoadBalancing;

namespace CloudSpendGuard.Functions.Connections;

public static class LoadBalancerCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var elb = new AmazonElasticLoadBalancingV2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var balancer in elb.Paginators.DescribeLoadBalancers(new DescribeLoadBalancersRequest()).LoadBalancers)
        {
            var targetGroups = 0;
            var registered = 0;
            var healthy = 0;

            await foreach (var group in elb.Paginators.DescribeTargetGroups(new DescribeTargetGroupsRequest { LoadBalancerArn = balancer.LoadBalancerArn }).TargetGroups)
            {
                var health = await elb.DescribeTargetHealthAsync(new DescribeTargetHealthRequest { TargetGroupArn = group.TargetGroupArn });
                var targets = health.TargetHealthDescriptions ?? [];

                targetGroups++;
                registered += targets.Count;
                healthy += targets.Count(target => target.TargetHealth?.State?.Value is "healthy" or "initial" or "unavailable");
            }

            resources.Add(new Resource(
                balancer.LoadBalancerArn,
                "elasticloadbalancing:loadbalancer",
                region,
                balancer.LoadBalancerName,
                new Dictionary<string, string>
                {
                    ["type"] = balancer.Type?.Value ?? "",
                    ["scheme"] = balancer.Scheme?.Value ?? "",
                    ["state"] = balancer.State?.Code?.Value ?? "",
                    ["dnsName"] = balancer.DNSName ?? "",
                    ["vpcId"] = balancer.VpcId ?? "",
                    ["createdAt"] = balancer.CreatedTime?.ToString("O") ?? "",
                    ["targetGroups"] = targetGroups.ToString(),
                    ["registeredTargets"] = registered.ToString(),
                    ["healthyTargets"] = healthy.ToString(),
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectClassic(AWSCredentials credentials, string region, string accountId)
    {
        using var elb = new Classic.AmazonElasticLoadBalancingClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var balancer in elb.Paginators.DescribeLoadBalancers(new Classic.Model.DescribeLoadBalancersRequest()).LoadBalancerDescriptions)
        {
            var registered = balancer.Instances?.Count ?? 0;
            var healthy = 0;

            if (registered > 0)
            {
                var health = await elb.DescribeInstanceHealthAsync(new Classic.Model.DescribeInstanceHealthRequest { LoadBalancerName = balancer.LoadBalancerName });
                healthy = health.InstanceStates?.Count(instance => instance.State == "InService") ?? 0;
            }

            resources.Add(new Resource(
                $"arn:aws:elasticloadbalancing:{region}:{accountId}:loadbalancer/{balancer.LoadBalancerName}",
                "elasticloadbalancing:classic-loadbalancer",
                region,
                balancer.LoadBalancerName,
                new Dictionary<string, string>
                {
                    ["scheme"] = balancer.Scheme ?? "",
                    ["dnsName"] = balancer.DNSName ?? "",
                    ["vpcId"] = balancer.VPCId ?? "",
                    ["createdAt"] = balancer.CreatedTime?.ToString("O") ?? "",
                    ["registeredTargets"] = registered.ToString(),
                    ["healthyTargets"] = healthy.ToString(),
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
