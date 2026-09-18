using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace CloudSpendGuard.Functions.Connections;

public record PermissionResult(string Action, bool Allowed);

public record Verification(
    string Status,
    string AssumeRole,
    string? Error,
    IReadOnlyList<PermissionResult> Permissions,
    DateTimeOffset VerifiedAt);

public record Connection(string Id, string ExternalId, string? RoleArn, Verification? Verification);

public class ConnectionStore
{
    private readonly AmazonDynamoDBClient _dynamoDb = new();
    private readonly string _tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;

    public async Task<Connection?> Get(string id)
    {
        var response = await _dynamoDb.GetItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["id"] = new() { S = id },
        });

        if (response.Item is not { Count: > 0 } item)
        {
            return null;
        }

        return new Connection(
            item["id"].S,
            item["externalId"].S,
            item.TryGetValue("roleArn", out var roleArn) ? roleArn.S : null,
            item.TryGetValue("verification", out var verification)
                ? JsonSerializer.Deserialize<Verification>(verification.S, Http.JsonOptions)
                : null);
    }

    public async Task Save(Connection connection)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["id"] = new() { S = connection.Id },
            ["externalId"] = new() { S = connection.ExternalId },
        };

        if (connection.RoleArn is not null)
        {
            item["roleArn"] = new() { S = connection.RoleArn };
        }

        if (connection.Verification is not null)
        {
            item["verification"] = new() { S = JsonSerializer.Serialize(connection.Verification, Http.JsonOptions) };
        }

        await _dynamoDb.PutItemAsync(_tableName, item);
    }
}
