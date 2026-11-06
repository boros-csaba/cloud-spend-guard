using Amazon;
using Amazon.BCMDataExports;
using Amazon.BCMDataExports.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Organizations;
using Amazon.Organizations.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace CloudSpendGuard.Functions.Connections;

public class VerificationHandler
{
    private readonly ConnectionStore _store = new();
    private readonly AmazonSecurityTokenServiceClient _sts = new();

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request)
    {
        var workspaceId = await _store.GetOrCreateWorkspaceId(Http.UserId(request));
        var connection = await _store.Get(workspaceId, request.PathParameters["id"]);

        if (connection is null)
        {
            return Http.Error(404, "Connection not found.");
        }

        var checks = await Verify(connection);
        connection = connection with { Verification = Verification.From(checks, connection.Verification, DateTimeOffset.UtcNow) };

        await _store.Save(connection);

        return Http.Json(200, Http.Body(connection));
    }

    private async Task<IReadOnlyList<Check>> Verify(Connection connection)
    {
        var roleArn = ConnectionTemplate.RoleArn(connection.AccountId, connection.Id);
        AWSCredentials credentials;

        try
        {
            var assumed = await _sts.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = roleArn,
                RoleSessionName = "cloud-spend-guard-verification",
                ExternalId = connection.ExternalId,
                DurationSeconds = 900,
            });

            credentials = new SessionAWSCredentials(
                assumed.Credentials.AccessKeyId, assumed.Credentials.SecretAccessKey, assumed.Credentials.SessionToken);
        }
        catch (AmazonSecurityTokenServiceException)
        {
            return [new Check("role", "failed",
                $"Could not assume {roleArn}. Launch the stack in account {connection.AccountId} and wait until it is complete.")];
        }

        if (await CanAssumeWithoutExternalId(roleArn))
        {
            return [new Check("role", "failed", "The role can be assumed without the External ID. Restore the template's trust policy.")];
        }

        return
        [
            new Check("role", "ok", null),
            await CheckPermissions(credentials, roleArn),
            await CheckCostExport(credentials, connection.Id),
            await CheckAccountType(credentials, connection.AccountId),
        ];
    }

    private async Task<bool> CanAssumeWithoutExternalId(string roleArn)
    {
        try
        {
            await _sts.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = roleArn,
                RoleSessionName = "cloud-spend-guard-verification",
                DurationSeconds = 900,
            });

            return true;
        }
        catch (AmazonSecurityTokenServiceException)
        {
            return false;
        }
    }

    private static async Task<Check> CheckPermissions(AWSCredentials credentials, string roleArn)
    {
        using var iam = new AmazonIdentityManagementServiceClient(credentials);

        try
        {
            var simulation = await iam.SimulatePrincipalPolicyAsync(new SimulatePrincipalPolicyRequest
            {
                PolicySourceArn = roleArn,
                ActionNames = [.. ConnectionTemplate.RequiredPermissions],
            });

            var missing = ConnectionTemplate.RequiredPermissions
                .Where(action => simulation.EvaluationResults?.Any(result =>
                    result.EvalActionName == action && result.EvalDecision == PolicyEvaluationDecisionType.Allowed) != true)
                .ToList();

            return missing.Count == 0
                ? new Check("permissions", "ok", null)
                : new Check("permissions", "failed", $"The role is missing: {string.Join(", ", missing)}.");
        }
        catch (AmazonIdentityManagementServiceException)
        {
            return new Check("permissions", "failed", "The role cannot check its own permissions. Allow iam:SimulatePrincipalPolicy on it.");
        }
    }

    private static async Task<Check> CheckCostExport(AWSCredentials credentials, string connectionId)
    {
        var name = ConnectionTemplate.Name(connectionId);

        try
        {
            using var dataExports = new AmazonBCMDataExportsClient(credentials, RegionEndpoint.USEast1);
            var exports = await dataExports.ListExportsAsync(new ListExportsRequest());
            var reference = exports.Exports?.FirstOrDefault(export => export.ExportName == name);

            if (reference is null)
            {
                return new Check("costExport", "failed", $"Cost Export {name} does not exist.");
            }

            var export = (await dataExports.GetExportAsync(new GetExportRequest { ExportArn = reference.ExportArn })).Export;
            var problems = ExportSettings.Problems(export, name);

            if (problems.Count > 0)
            {
                return new Check("costExport", "failed", string.Join(" ", problems));
            }

            using var s3 = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
            await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = name, MaxKeys = 1 });

            return new Check("costExport", "ok", null);
        }
        catch (AmazonServiceException exception)
        {
            return new Check("costExport", "failed", $"Could not read the Cost Export: {exception.Message}");
        }
    }

    private static async Task<Check> CheckAccountType(AWSCredentials credentials, string accountId)
    {
        using var organizations = new AmazonOrganizationsClient(credentials, RegionEndpoint.USEast1);

        try
        {
            var organization = (await organizations.DescribeOrganizationAsync(new DescribeOrganizationRequest())).Organization;

            return organization.MasterAccountId == accountId
                ? new Check("accountType", "warning",
                    "This is an organization's management account. Its Cost Export includes the cost of every member account.")
                : new Check("accountType", "ok", null);
        }
        catch (AWSOrganizationsNotInUseException)
        {
            return new Check("accountType", "ok", null);
        }
        catch (AmazonOrganizationsException exception)
        {
            return new Check("accountType", "warning", $"Could not check the organization: {exception.Message}");
        }
    }
}

public static class ExportSettings
{
    public static IReadOnlyList<string> Problems(Export export, string bucketName)
    {
        var problems = new List<string>();
        var table = export.DataQuery?.TableConfigurations?.GetValueOrDefault("COST_AND_USAGE_REPORT");

        if (table?.GetValueOrDefault("INCLUDE_RESOURCES") != "TRUE")
        {
            problems.Add("Resource IDs are not included.");
        }

        if (table?.GetValueOrDefault("TIME_GRANULARITY") != "DAILY")
        {
            problems.Add("Time granularity is not daily.");
        }

        if (export.DestinationConfigurations?.S3Destination?.S3Bucket != bucketName)
        {
            problems.Add($"It is not delivered to bucket {bucketName}.");
        }

        return problems;
    }
}
