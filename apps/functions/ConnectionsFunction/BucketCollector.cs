using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace CloudSpendGuard.Functions.Connections;

public static class BucketCollector
{
    public static async Task<IReadOnlyList<Resource>> Collect(AWSCredentials credentials, string region, string accountId)
    {
        using var s3 = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
        var resources = new List<Resource>();

        await foreach (var bucket in s3.Paginators.ListBuckets(new ListBucketsRequest { MaxBuckets = 1000 }).Buckets)
        {
            resources.Add(new Resource(
                $"arn:aws:s3:::{bucket.BucketName}",
                "s3:bucket",
                bucket.BucketRegion ?? "unknown",
                bucket.BucketName,
                new Dictionary<string, string> { ["createdAt"] = bucket.CreationDate?.ToString("O") ?? "" },
                new Dictionary<string, string>(),
                default,
                null,
                null));
        }

        return resources;
    }
}
