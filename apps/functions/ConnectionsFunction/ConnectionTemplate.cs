namespace CloudSpendGuard.Functions.Connections;

public static class ConnectionTemplate
{
    public const string Region = "us-east-1";

    public static readonly string[] RequiredPermissions =
    [
        "ec2:DescribeRegions",
        "ec2:DescribeInstances",
        "ec2:DescribeVolumes",
        "ec2:DescribeVpcs",
        "ec2:DescribeSubnets",
        "ec2:DescribeAddresses",
        "ec2:DescribeNatGateways",
        "ec2:DescribeRouteTables",
        "ec2:DescribeVpnConnections",
        "ec2:DescribeVpcEndpoints",
        "ec2:DescribeTransitGatewayAttachments",
        "ec2:DescribeSnapshots",
        "ec2:DescribeImages",
        "elasticloadbalancing:DescribeLoadBalancers",
        "elasticloadbalancing:DescribeTargetGroups",
        "elasticloadbalancing:DescribeTargetHealth",
        "elasticloadbalancing:DescribeInstanceHealth",
        "ec2:DescribeSecurityGroups",
        "ec2:DescribeInternetGateways",
        "lambda:ListFunctions",
        "dynamodb:ListTables",
        "dynamodb:DescribeTable",
        "apigateway:GET",
        "states:ListStateMachines",
        "ecs:ListClusters",
        "ecs:DescribeClusters",
        "ecs:ListServices",
        "ecs:DescribeServices",
        "ecr:DescribeRepositories",
        "logs:DescribeLogGroups",
        "cognito-idp:ListUserPools",
        "cloudformation:DescribeStacks",
        "route53:ListHostedZones",
        "autoscaling:DescribeAutoScalingGroups",
        "autoscaling:DescribeWarmPool",
        "cloudfront:ListDistributions",
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
