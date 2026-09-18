using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;

namespace CloudSpendGuard.Functions.Connections;

public static class Http
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string TrustedAccountId = Environment.GetEnvironmentVariable("TRUSTED_ACCOUNT_ID")!;

    public static APIGatewayHttpApiV2ProxyResponse Json(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(body, JsonOptions),
    };

    public static APIGatewayHttpApiV2ProxyResponse Error(int statusCode, string message) =>
        Json(statusCode, new { error = message });

    public static APIGatewayHttpApiV2ProxyResponse Connection(int statusCode, Connection connection) =>
        Json(statusCode, new
        {
            connection.Id,
            connection.ExternalId,
            connection.RoleArn,
            connection.Verification,
            RoleName = AuditorRole.Name,
            TrustPolicy = AuditorRole.TrustPolicy(TrustedAccountId, connection.ExternalId),
            PermissionsPolicy = AuditorRole.PermissionsPolicy(),
        });
}
