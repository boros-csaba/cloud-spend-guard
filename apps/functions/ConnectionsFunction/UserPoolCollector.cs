using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;

namespace CloudSpendGuard.Functions.Connections;

public static class UserPoolCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var cognito = new AmazonCognitoIdentityProviderClient(credentials, RegionEndpoint.GetBySystemName(region));
        var resources = new List<Resource>();

        await foreach (var pool in cognito.Paginators.ListUserPools(new ListUserPoolsRequest { MaxResults = 60 }).UserPools)
        {
            resources.Add(new Resource(
                $"arn:aws:cognito-idp:{region}:{accountId}:userpool/{pool.Id}",
                "cognito-idp:userpool",
                region,
                pool.Name,
                new Dictionary<string, string>
                {
                    ["userPoolId"] = pool.Id,
                    ["createdAt"] = pool.CreationDate?.ToString("O") ?? "",
                    ["lastModified"] = pool.LastModifiedDate?.ToString("O") ?? "",
                },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
