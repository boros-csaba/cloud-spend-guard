using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace CloudSpendGuard.Functions.Connections;

public record CreateConnectionRequest(string? AccountId);

public partial class ConnectionsHandler
{
    private readonly ConnectionStore _store = new();

    [GeneratedRegex(@"^\d{12}$")]
    private static partial Regex AccountIdPattern();

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request)
    {
        var workspaceId = await _store.GetOrCreateWorkspaceId(Http.UserId(request));

        switch (request.RouteKey)
        {
            case "GET /api/connections":
                var connections = await _store.List(workspaceId);
                return Http.Json(200, connections.OrderBy(connection => connection.CreatedAt).Select(Http.Body));

            case "POST /api/connections":
                return await Create(workspaceId, request.Body);
        }

        var existing = await _store.Get(workspaceId, request.PathParameters["id"]);

        if (existing is null)
        {
            return Http.Error(404, "Connection not found.");
        }

        if (request.RouteKey == "DELETE /api/connections/{id}")
        {
            await _store.Delete(workspaceId, existing.Id);
            return new APIGatewayHttpApiV2ProxyResponse { StatusCode = 204 };
        }

        return Http.Json(200, Http.Body(existing));
    }

    private async Task<APIGatewayHttpApiV2ProxyResponse> Create(string workspaceId, string? body)
    {
        string? accountId;

        try
        {
            accountId = JsonSerializer.Deserialize<CreateConnectionRequest>(body ?? "{}", Http.JsonOptions)?.AccountId?.Trim();
        }
        catch (JsonException)
        {
            accountId = null;
        }

        if (accountId is null || !AccountIdPattern().IsMatch(accountId))
        {
            return Http.Error(400, "AWS account ID must be 12 digits.");
        }

        if ((await _store.List(workspaceId)).Any(connection => connection.AccountId == accountId))
        {
            return Http.Error(409, "This AWS account is already connected in your workspace.");
        }

        var connection = new Connection(
            Guid.NewGuid().ToString("N"),
            workspaceId,
            "standalone",
            accountId,
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            DateTimeOffset.UtcNow,
            null);

        await _store.Save(connection);

        return Http.Json(201, Http.Body(connection));
    }
}
