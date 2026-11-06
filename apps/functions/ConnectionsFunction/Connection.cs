using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace CloudSpendGuard.Functions.Connections;

public record Check(string Name, string Status, string? Message);

public record Verification(string Status, IReadOnlyList<Check> Checks, DateTimeOffset VerifiedAt, DateTimeOffset? FailingSince)
{
    public static Verification From(IReadOnlyList<Check> checks, Verification? previous, DateTimeOffset now)
    {
        if (checks.All(check => check.Status != "failed"))
        {
            return new Verification("verified", checks, now, null);
        }

        var failingSince = previous?.Status == "verified" ? now : previous?.FailingSince;

        return new Verification("failed", checks, now, failingSince);
    }
}

public record Connection(
    string Id,
    string WorkspaceId,
    string Type,
    string AccountId,
    string ExternalId,
    DateTimeOffset CreatedAt,
    Verification? Verification)
{
    public string State => Verification switch
    {
        null or { Status: "failed", FailingSince: null } => "pending",
        { Status: "verified" } => "working",
        _ => "broken",
    };
}

public class ConnectionStore
{
    private readonly AmazonDynamoDBClient _dynamoDb = new();
    private readonly string _tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;

    public async Task<string> GetOrCreateWorkspaceId(string userId)
    {
        var existing = await FindWorkspaceId(userId);

        if (existing is not null)
        {
            return existing;
        }

        var workspaceId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow.ToString("O");

        try
        {
            await _dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    Put(new() { ["pk"] = S($"USER#{userId}"), ["sk"] = S("PERSONAL_WORKSPACE") }, "attribute_not_exists(pk)"),
                    Put(new() { ["pk"] = S($"USER#{userId}"), ["sk"] = S($"WORKSPACE#{workspaceId}"), ["workspaceId"] = S(workspaceId) }),
                    Put(new() { ["pk"] = S($"WORKSPACE#{workspaceId}"), ["sk"] = S("WORKSPACE"), ["id"] = S(workspaceId), ["createdAt"] = S(now) }),
                ],
            });

            return workspaceId;
        }
        catch (TransactionCanceledException)
        {
            return await FindWorkspaceId(userId) ?? throw new InvalidOperationException("Workspace was not created.");
        }
    }

    public async Task<IReadOnlyList<Connection>> List(string workspaceId)
    {
        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
            ExpressionAttributeValues = new() { [":pk"] = S($"WORKSPACE#{workspaceId}"), [":sk"] = S("CONNECTION#") },
        });

        return (response.Items ?? []).Select(ToConnection).ToList();
    }

    public async Task<Connection?> Get(string workspaceId, string id)
    {
        var response = await _dynamoDb.GetItemAsync(_tableName, Key(workspaceId, id));

        return response.Item is { Count: > 0 } item ? ToConnection(item) : null;
    }

    public async Task Save(Connection connection)
    {
        var item = Key(connection.WorkspaceId, connection.Id);
        item["id"] = S(connection.Id);
        item["workspaceId"] = S(connection.WorkspaceId);
        item["type"] = S(connection.Type);
        item["accountId"] = S(connection.AccountId);
        item["externalId"] = S(connection.ExternalId);
        item["createdAt"] = S(connection.CreatedAt.ToString("O"));

        if (connection.Verification is not null)
        {
            item["verification"] = S(JsonSerializer.Serialize(connection.Verification, Http.JsonOptions));
        }

        await _dynamoDb.PutItemAsync(_tableName, item);
    }

    public Task Delete(string workspaceId, string id) => _dynamoDb.DeleteItemAsync(_tableName, Key(workspaceId, id));

    private async Task<string?> FindWorkspaceId(string userId)
    {
        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "pk = :pk AND begins_with(sk, :sk)",
            ExpressionAttributeValues = new() { [":pk"] = S($"USER#{userId}"), [":sk"] = S("WORKSPACE#") },
            Limit = 1,
        });

        return response.Items?.FirstOrDefault()?["workspaceId"].S;
    }

    private TransactWriteItem Put(Dictionary<string, AttributeValue> item, string? condition = null) => new()
    {
        Put = new Put { TableName = _tableName, Item = item, ConditionExpression = condition },
    };

    private static Dictionary<string, AttributeValue> Key(string workspaceId, string id) => new()
    {
        ["pk"] = S($"WORKSPACE#{workspaceId}"),
        ["sk"] = S($"CONNECTION#{id}"),
    };

    private static AttributeValue S(string value) => new() { S = value };

    private static Connection ToConnection(Dictionary<string, AttributeValue> item) => new(
        item["id"].S,
        item["workspaceId"].S,
        item["type"].S,
        item["accountId"].S,
        item["externalId"].S,
        DateTimeOffset.Parse(item["createdAt"].S),
        item.TryGetValue("verification", out var verification)
            ? JsonSerializer.Deserialize<Verification>(verification.S, Http.JsonOptions)
            : null);
}
