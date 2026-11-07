using System.Diagnostics;
using System.Net;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Lambda.Core;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public record Collector(string Type, bool Global, Func<AWSCredentials, string, string, Task<IReadOnlyList<Resource>>> Collect);

public class SnapshotRunner
{
    private static readonly Collector[] Collectors =
    [
        new("ec2:vpc", false, NetworkCollector.CollectVpcs),
        new("ec2:subnet", false, NetworkCollector.CollectSubnets),
        new("ec2:volume", false, VolumeCollector.Collect),
        new("ec2:instance", false, InstanceCollector.Collect),
        new("s3:bucket", true, BucketCollector.Collect),
        new("cloudfront:distribution", true, DistributionCollector.Collect),
        new("ec2:elastic-ip", false, AddressCollector.Collect),
        new("ec2:natgateway", false, NetworkCollector.CollectNatGateways),
        new("ec2:vpn-connection", false, NetworkCollector.CollectVpnConnections),
        new("ec2:vpc-endpoint", false, NetworkCollector.CollectVpcEndpoints),
        new("ec2:transit-gateway-attachment", false, NetworkCollector.CollectTransitGatewayAttachments),
        new("elasticloadbalancing:loadbalancer", false, LoadBalancerCollector.Collect),
        new("elasticloadbalancing:classic-loadbalancer", false, LoadBalancerCollector.CollectClassic),
        new("rds:db", false, DatabaseCollector.Collect),
        new("ec2:snapshot", false, EbsSnapshotCollector.Collect),
        new("ec2:security-group", false, NetworkCollector.CollectSecurityGroups),
        new("ec2:internet-gateway", false, NetworkCollector.CollectInternetGateways),
        new("ec2:route-table", false, NetworkCollector.CollectRouteTables),
        new("lambda:function", false, FunctionCollector.Collect),
        new("dynamodb:table", false, TableCollector.Collect),
        new("apigateway:restapi", false, ApiCollector.CollectRest),
        new("apigateway:api", false, ApiCollector.CollectHttpAndWebSocket),
        new("states:statemachine", false, StateMachineCollector.Collect),
        new("ecs:cluster", false, ContainerCollector.CollectClusters),
        new("ecs:service", false, ContainerCollector.CollectServices),
        new("ecr:repository", false, ContainerCollector.CollectRepositories),
        new("logs:log-group", false, LogGroupCollector.Collect),
        new("cognito-idp:userpool", false, UserPoolCollector.Collect),
        new("cloudformation:stack", false, StackCollector.Collect),
        new("route53:hostedzone", true, HostedZoneCollector.Collect),
    ];

    private static readonly SemaphoreSlim ScanSlots = new(32);

    private readonly ConnectionStore _connections = new();
    private readonly SnapshotStore _snapshots = new();
    private readonly VerificationHandler _verification = new();

    public async Task Run(SnapshotRequest request, ILambdaContext context)
    {
        var connection = await _connections.Get(request.WorkspaceId, request.ConnectionId);

        if (connection is null)
        {
            return;
        }

        var run = await _snapshots.GetRun(connection.WorkspaceId, connection.AccountId);
        var startedAt = run?.StartedAt ?? DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var checks = await _verification.Verify(connection);
            connection = connection with { Verification = Verification.From(checks, connection.Verification, DateTimeOffset.UtcNow) };
            await _connections.Save(connection);

            if (connection.Verification.Status != "verified")
            {
                await Finish(connection, startedAt, "failed", "Verification failed. Open the Connection to see why.", null);
                return;
            }

            var credentials = await _verification.AssumeRole(connection, "cloud-spend-guard-snapshot");
            var regions = await EnabledRegions(credentials);
            var scans = await Task.WhenAll(Collectors.SelectMany(collector => collector.Global
                ? [Scan(credentials, collector, null, connection.AccountId)]
                : regions.Select(region => Scan(credentials, collector, region, connection.AccountId))));
            var previous = await _snapshots.GetSnapshot(connection.WorkspaceId, connection.AccountId);

            var snapshot = Snapshot.Build(
                connection.AccountId,
                [.. scans.Select(scan => scan.Scan)],
                [.. scans.SelectMany(scan => scan.Resources)],
                previous,
                DateTimeOffset.UtcNow);

            await _snapshots.SaveSnapshot(connection.WorkspaceId, snapshot);
            await Finish(connection, startedAt, "done", null, snapshot);
        }
        catch (Exception exception)
        {
            context.Logger.LogError(exception.ToString());
            await Finish(connection, startedAt, "failed", "The Snapshot could not be completed.", null);
        }

        context.Logger.LogInformation($"Snapshot of {connection.AccountId} took {stopwatch.ElapsedMilliseconds} ms.");
    }

    private Task Finish(Connection connection, DateTimeOffset startedAt, string status, string? message, Snapshot? snapshot) =>
        _snapshots.Finish(connection.WorkspaceId, connection.AccountId, new Run(
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            message,
            snapshot?.Resources.Count(resource => resource.DeletedAt is null),
            snapshot?.Resources.Count(resource => resource.Idle is not null)));

    private static async Task<IReadOnlyList<string>> EnabledRegions(AWSCredentials credentials)
    {
        using var ec2 = new AmazonEC2Client(credentials, RegionEndpoint.USEast1);
        var response = await ec2.DescribeRegionsAsync(new DescribeRegionsRequest());

        return [.. response.Regions.Select(region => region.RegionName)];
    }

    private static async Task<(Scan Scan, IReadOnlyList<Resource> Resources)> Scan(
        AWSCredentials credentials, Collector collector, string? region, string accountId)
    {
        await ScanSlots.WaitAsync();

        try
        {
            var resources = await collector.Collect(credentials, region ?? "us-east-1", accountId);

            return (new Scan(region, collector.Type, "scanned", null), resources);
        }
        catch (AmazonServiceException exception) when (exception.StatusCode == HttpStatusCode.Forbidden
            || exception.ErrorCode is "UnauthorizedOperation" or "AuthFailure" or "AccessDenied" or "AccessDeniedException")
        {
            return (new Scan(region, collector.Type, "skipped", "Access denied."), []);
        }
        catch (Exception exception)
        {
            return (new Scan(region, collector.Type, "failed", exception.Message), []);
        }
        finally
        {
            ScanSlots.Release();
        }
    }
}
