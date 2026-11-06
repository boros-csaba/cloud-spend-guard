namespace CloudSpendGuard.Functions.Connections;

public static class ConnectionTemplate
{
    public const string Region = "us-east-1";

    public static readonly string[] RequiredPermissions =
    [
        "ec2:DescribeInstances",
        "ec2:DescribeVolumes",
        "rds:DescribeDBInstances",
        "s3:ListAllMyBuckets",
        "cloudwatch:GetMetricData",
        "bcm-data-exports:GetExport",
        "organizations:DescribeOrganization",
        "resource-explorer-2:ListIndexes",
    ];

    public static string Name(string connectionId) => $"cloud-spend-guard-{connectionId}";

    public static string RoleArn(string accountId, string connectionId) =>
        $"arn:aws:iam::{accountId}:role/{Name(connectionId)}";

    public static string LaunchUrl(string templateUrl, Connection connection, string trustedAccountId) =>
        $"https://{Region}.console.aws.amazon.com/cloudformation/home?region={Region}#/stacks/quickcreate" +
        $"?templateURL={Uri.EscapeDataString(templateUrl)}" +
        $"&stackName={Name(connection.Id)}" +
        $"&param_ConnectionId={connection.Id}" +
        $"&param_ExternalId={connection.ExternalId}" +
        $"&param_TrustedAccountId={trustedAccountId}";
}
