using System.Text.Json;
using System.Text.RegularExpressions;

namespace CloudSpendGuard.Functions.Connections;

public static partial class AuditorRole
{
    public const string Name = "cloud-spend-guard-auditor";

    public static readonly string[] RequiredPermissions = ["ec2:DescribeInstances"];

    private static readonly JsonSerializerOptions PolicyJsonOptions = new() { WriteIndented = true };

    [GeneratedRegex($@"^arn:aws:iam::\d{{12}}:role/{Name}$")]
    public static partial Regex ArnPattern();

    public static string TrustPolicy(string trustedAccountId, string externalId) => JsonSerializer.Serialize(new
    {
        Version = "2012-10-17",
        Statement = new[]
        {
            new
            {
                Effect = "Allow",
                Principal = new { AWS = $"arn:aws:iam::{trustedAccountId}:root" },
                Action = "sts:AssumeRole",
                Condition = new
                {
                    StringEquals = new Dictionary<string, string> { ["sts:ExternalId"] = externalId },
                },
            },
        },
    }, PolicyJsonOptions);

    public static string PermissionsPolicy() => JsonSerializer.Serialize(new
    {
        Version = "2012-10-17",
        Statement = new object[]
        {
            new { Effect = "Allow", Action = RequiredPermissions, Resource = "*" },
            new { Effect = "Allow", Action = "iam:SimulatePrincipalPolicy", Resource = $"arn:aws:iam::*:role/{Name}" },
        },
    }, PolicyJsonOptions);
}
