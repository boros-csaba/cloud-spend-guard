using System.Security.Cryptography;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace CloudSpendGuard.Functions.Connections;

public class ConnectionsHandler
{
    private readonly ConnectionStore _store = new();

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request)
    {
        if (request.RouteKey == "POST /connections")
        {
            var connection = new Connection(
                Guid.NewGuid().ToString(),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                null,
                null);

            await _store.Save(connection);

            return Http.Connection(201, connection);
        }

        var existing = await _store.Get(request.PathParameters["id"]);

        return existing is null
            ? Http.Error(404, "Connection not found.")
            : Http.Connection(200, existing);
    }
}
