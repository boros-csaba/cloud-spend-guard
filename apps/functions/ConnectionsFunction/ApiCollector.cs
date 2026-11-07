using Amazon;
using Amazon.APIGateway;
using Amazon.APIGateway.Model;
using Amazon.Runtime;
using V2 = Amazon.ApiGatewayV2;

namespace CloudSpendGuard.Functions.Connections;

public static class ApiCollector
{
    public static async Task<IReadOnlyList<Resource>> CollectRest(AWSCredentials credentials, string region, string accountId)
    {
        using var apiGateway = new AmazonAPIGatewayClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var api in apiGateway.Paginators.GetRestApis(new GetRestApisRequest { Limit = 500 }).Items)
        {
            resources.Add(new Resource(
                $"arn:aws:apigateway:{region}::/restapis/{api.Id}",
                "apigateway:restapi",
                region,
                api.Name,
                new Dictionary<string, string>
                {
                    ["apiId"] = api.Id,
                    ["endpointType"] = api.EndpointConfiguration?.Types is [var type, ..] ? type : "",
                    ["createdAt"] = api.CreatedDate?.ToString("O") ?? "",
                },
                api.Tags ?? new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }

    public static async Task<IReadOnlyList<Resource>> CollectHttpAndWebSocket(AWSCredentials credentials, string region, string accountId)
    {
        using var apiGateway = new V2.AmazonApiGatewayV2Client(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();
        var request = new V2.Model.GetApisRequest();

        do
        {
            var response = await apiGateway.GetApisAsync(request);

            foreach (var api in response.Items ?? [])
            {
                resources.Add(new Resource(
                    $"arn:aws:apigateway:{region}::/apis/{api.ApiId}",
                    "apigateway:api",
                    region,
                    api.Name,
                    new Dictionary<string, string>
                    {
                        ["apiId"] = api.ApiId,
                        ["protocol"] = api.ProtocolType?.Value ?? "",
                        ["endpoint"] = api.ApiEndpoint ?? "",
                        ["createdAt"] = api.CreatedDate?.ToString("O") ?? "",
                    },
                    api.Tags ?? new Dictionary<string, string>(),
                    default,
                    null,
                    null));
            }

            request.NextToken = response.NextToken;
        }
        while (request.NextToken is not null);

        return resources;
    }
}
