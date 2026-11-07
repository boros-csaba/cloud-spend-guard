using Amazon.Lambda.APIGatewayEvents;

namespace CloudSpendGuard.Functions.Connections;

public class SnapshotsHandler
{
    private readonly ConnectionStore _connections = new();
    private readonly SnapshotStore _snapshots = new();

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request)
    {
        var workspaceId = await _connections.GetOrCreateWorkspaceId(Http.UserId(request));

        if (request.RouteKey == "GET /api/snapshots")
        {
            var connections = await _connections.List(workspaceId);
            var cloudAccounts = await Task.WhenAll(connections.OrderBy(connection => connection.CreatedAt).Select(async connection => new
            {
                ConnectionId = connection.Id,
                connection.AccountId,
                ConnectionState = connection.State,
                Run = await _snapshots.GetRun(workspaceId, connection.AccountId),
                Snapshot = await _snapshots.GetSnapshot(workspaceId, connection.AccountId),
            }));

            return Http.Json(200, cloudAccounts);
        }

        var existing = await _connections.Get(workspaceId, request.PathParameters["id"]);

        if (existing is null)
        {
            return Http.Error(404, "Connection not found.");
        }

        if (existing.State == "pending")
        {
            return Http.Error(409, "Finish setting up the Connection first.");
        }

        var run = await _snapshots.Start(existing);

        return run is null
            ? Http.Error(409, "A Snapshot is already running for this account.")
            : Http.Json(202, run);
    }
}
