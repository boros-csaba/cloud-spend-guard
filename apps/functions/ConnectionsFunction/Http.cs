using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;

namespace CloudSpendGuard.Functions.Connections;

public static class Http
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string TrustedAccountId = Environment.GetEnvironmentVariable("TRUSTED_ACCOUNT_ID")!;
    private static readonly string TemplateUrl = Environment.GetEnvironmentVariable("TEMPLATE_URL")!;

    public static string UserId(APIGatewayHttpApiV2ProxyRequest request) =>
        request.RequestContext.Authorizer.Jwt.Claims["sub"];

    public static APIGatewayHttpApiV2ProxyResponse Json(int statusCode, object body) => new()
    {
        StatusCode = statusCode,
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        Body = JsonSerializer.Serialize(body, JsonOptions),
    };

    public static APIGatewayHttpApiV2ProxyResponse Error(int statusCode, string message) =>
        Json(statusCode, new { error = message });

    public static object Body(Connection connection) => new
    {
        connection.Id,
        connection.Type,
        connection.AccountId,
        connection.CreatedAt,
        connection.State,
        connection.Verification,
        LaunchUrl = ConnectionTemplate.LaunchUrl(TemplateUrl, connection, TrustedAccountId),
        TemplateUrl,
    };
}
