using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class ContainerCollector
{
    public static async Task<IReadOnlyList<Resource>> CollectClusters(AWSCredentials credentials, string region, string accountId)
    {
        using var ecs = new AmazonECSClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        foreach (var batch in (await ClusterArns(ecs)).Chunk(100))
        {
            var response = await ecs.DescribeClustersAsync(new DescribeClustersRequest { Clusters = [.. batch], Include = ["TAGS"] });

            foreach (var cluster in response.Clusters ?? [])
            {
                resources.Add(new Resource(
                    cluster.ClusterArn,
                    "ecs:cluster",
                    region,
                    cluster.ClusterName,
                    new Dictionary<string, string>
                    {
                        ["status"] = cluster.Status ?? "",
                        ["activeServices"] = cluster.ActiveServicesCount?.ToString() ?? "",
                        ["runningTasks"] = cluster.RunningTasksCount?.ToString() ?? "",
                        ["containerInstances"] = cluster.RegisteredContainerInstancesCount?.ToString() ?? "",
                    },
                    (cluster.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value),
                    default,
                    null,
                    null));
            }
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectServices(AWSCredentials credentials, string region, string accountId)
    {
        using var ecs = new AmazonECSClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        foreach (var clusterArn in await ClusterArns(ecs))
        {
            var serviceArns = new List<string>();

            await foreach (var serviceArn in ecs.Paginators.ListServices(new ListServicesRequest { Cluster = clusterArn, MaxResults = 100 }).ServiceArns)
            {
                serviceArns.Add(serviceArn);
            }

            foreach (var batch in serviceArns.Chunk(10))
            {
                var response = await ecs.DescribeServicesAsync(new DescribeServicesRequest { Cluster = clusterArn, Services = [.. batch], Include = ["TAGS"] });

                foreach (var service in response.Services ?? [])
                {
                    resources.Add(new Resource(
                        service.ServiceArn,
                        "ecs:service",
                        region,
                        service.ServiceName,
                        new Dictionary<string, string>
                        {
                            ["cluster"] = clusterArn.Split('/').Last(),
                            ["status"] = service.Status ?? "",
                            ["launchType"] = service.LaunchType?.Value ?? "",
                            ["desiredCount"] = service.DesiredCount?.ToString() ?? "",
                            ["runningCount"] = service.RunningCount?.ToString() ?? "",
                            ["createdAt"] = service.CreatedAt?.ToString("O") ?? "",
                        },
                        (service.Tags ?? []).ToDictionary(tag => tag.Key, tag => tag.Value),
                        default,
                        null,
                        null));
                }
            }
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectRepositories(AWSCredentials credentials, string region, string accountId)
    {
        using var ecr = new AmazonECRClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var repository in ecr.Paginators.DescribeRepositories(new DescribeRepositoriesRequest { MaxResults = 1000 }).Repositories)
        {
            resources.Add(new Resource(
                repository.RepositoryArn,
                "ecr:repository",
                region,
                repository.RepositoryName,
                new Dictionary<string, string>
                {
                    ["uri"] = repository.RepositoryUri ?? "",
                    ["tagMutability"] = repository.ImageTagMutability?.Value ?? "",
                    ["createdAt"] = repository.CreatedAt?.ToString("O") ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }

    private static async Task<List<string>> ClusterArns(AmazonECSClient ecs)
    {
        var clusterArns = new List<string>();

        await foreach (var clusterArn in ecs.Paginators.ListClusters(new ListClustersRequest { MaxResults = 100 }).ClusterArns)
        {
            clusterArns.Add(clusterArn);
        }

        return clusterArns;
    }
}
