using System.Diagnostics;
using System.Net;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Lambda.Core;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public class SnapshotRunner
{
    private static readonly Func<AWSCredentials, string, string, Task<IReadOnlyList<Resource>>>[] Collectors =
    [
        VolumeCollector.Collect,
    ];

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
            var scans = await Task.WhenAll((await EnabledRegions(credentials)).Select(region => Scan(credentials, region, connection.AccountId)));
            var previous = await _snapshots.GetSnapshot(connection.WorkspaceId, connection.AccountId);

            var snapshot = Snapshot.Build(
                connection.AccountId,
                [.. scans.Select(scan => scan.Region).OrderBy(region => region.Region)],
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

    private static async Task<(RegionScan Region, IReadOnlyList<Resource> Resources)> Scan(
        AWSCredentials credentials, string region, string accountId)
    {
        try
        {
            var resources = await Task.WhenAll(Collectors.Select(collect => collect(credentials, region, accountId)));

            return (new RegionScan(region, "scanned", null), [.. resources.SelectMany(found => found)]);
        }
        catch (AmazonServiceException exception) when (exception.StatusCode == HttpStatusCode.Forbidden
            || exception.ErrorCode is "UnauthorizedOperation" or "AuthFailure" or "AccessDenied" or "AccessDeniedException")
        {
            return (new RegionScan(region, "skipped", "Access denied."), []);
        }
        catch (AmazonServiceException exception)
        {
            return (new RegionScan(region, "failed", exception.Message), []);
        }
    }
}
