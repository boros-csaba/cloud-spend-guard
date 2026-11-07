using System.IO.Compression;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Environment = System.Environment;

namespace CloudSpendGuard.Functions.Connections;

public record Run(string Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string? Message, int? ResourceCount, int? IdleCount);

public record SnapshotRequest(string WorkspaceId, string ConnectionId);

public class SnapshotStore
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(6);

    private readonly AmazonDynamoDBClient _dynamoDb = new();
    private readonly AmazonS3Client _s3 = new();
    private readonly AmazonLambdaClient _lambda = new();
    private readonly string _tableName = Environment.GetEnvironmentVariable("TABLE_NAME")!;
    private readonly string _bucketName = Environment.GetEnvironmentVariable("SNAPSHOTS_BUCKET")!;

    public async Task<Run?> Start(Connection connection)
    {
        var now = DateTimeOffset.UtcNow;

        try
        {
            await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = Key(connection.WorkspaceId, connection.AccountId),
                UpdateExpression = "SET accountId = :accountId, connectionId = :connectionId, runStatus = :running, runStartedAt = :now " +
                    "REMOVE runFinishedAt, runMessage, resourceCount, idleCount",
                ConditionExpression = "attribute_not_exists(runStatus) OR runStatus <> :running OR runStartedAt < :stale",
                ExpressionAttributeValues = new()
                {
                    [":accountId"] = S(connection.AccountId),
                    [":connectionId"] = S(connection.Id),
                    [":running"] = S("running"),
                    [":now"] = S(now.ToString("O")),
                    [":stale"] = S((now - StaleAfter).ToString("O")),
                },
            });
        }
        catch (ConditionalCheckFailedException)
        {
            return null;
        }

        try
        {
            await _lambda.InvokeAsync(new InvokeRequest
            {
                FunctionName = Environment.GetEnvironmentVariable("SNAPSHOT_RUNNER_FUNCTION"),
                InvocationType = InvocationType.Event,
                Payload = JsonSerializer.Serialize(new SnapshotRequest(connection.WorkspaceId, connection.Id)),
            });
        }
        catch (AmazonLambdaException)
        {
            var failed = new Run("failed", now, DateTimeOffset.UtcNow, "The Snapshot could not be started.", null, null);
            await Finish(connection.WorkspaceId, connection.AccountId, failed);
            return failed;
        }

        return new Run("running", now, null, null, null, null);
    }

    public async Task Finish(string workspaceId, string accountId, Run run)
    {
        var values = new Dictionary<string, AttributeValue>
        {
            [":status"] = S(run.Status),
            [":finishedAt"] = S(run.FinishedAt!.Value.ToString("O")),
            [":message"] = run.Message is null ? new AttributeValue { NULL = true } : S(run.Message),
            [":resourceCount"] = N(run.ResourceCount ?? 0),
            [":idleCount"] = N(run.IdleCount ?? 0),
        };

        await _dynamoDb.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _tableName,
            Key = Key(workspaceId, accountId),
            UpdateExpression = "SET runStatus = :status, runFinishedAt = :finishedAt, runMessage = :message, " +
                "resourceCount = :resourceCount, idleCount = :idleCount",
            ExpressionAttributeValues = values,
        });

        await _dynamoDb.PutItemAsync(_tableName, new Dictionary<string, AttributeValue>
        {
            ["pk"] = S($"WORKSPACE#{workspaceId}"),
            ["sk"] = S($"RUN#{accountId}#{run.StartedAt:O}"),
            ["run"] = S(JsonSerializer.Serialize(run, Http.JsonOptions)),
        });
    }

    public async Task<Run?> GetRun(string workspaceId, string accountId)
    {
        var item = (await _dynamoDb.GetItemAsync(_tableName, Key(workspaceId, accountId))).Item;

        if (item is not { Count: > 0 } || !item.ContainsKey("runStatus"))
        {
            return null;
        }

        var startedAt = DateTimeOffset.Parse(item["runStartedAt"].S);

        if (item["runStatus"].S == "running" && startedAt < DateTimeOffset.UtcNow - StaleAfter)
        {
            return new Run("failed", startedAt, null, "The Snapshot did not finish.", null, null);
        }

        return new Run(
            item["runStatus"].S,
            startedAt,
            item.TryGetValue("runFinishedAt", out var finishedAt) ? DateTimeOffset.Parse(finishedAt.S) : null,
            item.TryGetValue("runMessage", out var message) ? message.S : null,
            item.TryGetValue("resourceCount", out var resourceCount) ? int.Parse(resourceCount.N) : null,
            item.TryGetValue("idleCount", out var idleCount) ? int.Parse(idleCount.N) : null);
    }

    public async Task<Snapshot?> GetSnapshot(string workspaceId, string accountId)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_bucketName, SnapshotKey(workspaceId, accountId));
            await using var gzip = new GZipStream(response.ResponseStream, CompressionMode.Decompress);

            return await JsonSerializer.DeserializeAsync<Snapshot>(gzip, Http.JsonOptions);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task SaveSnapshot(string workspaceId, Snapshot snapshot)
    {
        using var content = new MemoryStream();

        await using (var gzip = new GZipStream(content, CompressionLevel.Optimal, leaveOpen: true))
        {
            await JsonSerializer.SerializeAsync(gzip, snapshot, Http.JsonOptions);
        }

        content.Position = 0;

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = SnapshotKey(workspaceId, snapshot.AccountId),
            InputStream = content,
            ContentType = "application/json",
            Headers = { ContentEncoding = "gzip" },
        });
    }

    private static string SnapshotKey(string workspaceId, string accountId) =>
        $"workspaces/{workspaceId}/accounts/{accountId}/latest.json.gz";

    private static Dictionary<string, AttributeValue> Key(string workspaceId, string accountId) => new()
    {
        ["pk"] = S($"WORKSPACE#{workspaceId}"),
        ["sk"] = S($"CLOUDACCOUNT#{accountId}"),
    };

    private static AttributeValue S(string value) => new() { S = value };

    private static AttributeValue N(int value) => new() { N = value.ToString() };
}
