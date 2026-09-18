using System.Text.Json;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

namespace CloudSpendGuard.Functions.Connections;

public record VerificationRequest(string? RoleArn);

public class VerificationHandler
{
    private const string AssumeRoleFailedMessage =
        $"Could not assume the role. Check that it is named {AuditorRole.Name}, that it trusts the account shown " +
        "in the trust policy, and that the trust policy uses this Connection's External ID.";

    private const string SimulateFailedMessage =
        "The role was assumed but could not check its own permissions. Allow iam:SimulatePrincipalPolicy on it.";

    private readonly ConnectionStore _store = new();
    private readonly AmazonSecurityTokenServiceClient _sts = new();

    public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(APIGatewayHttpApiV2ProxyRequest request)
    {
        string? roleArn;

        try
        {
            roleArn = JsonSerializer.Deserialize<VerificationRequest>(request.Body ?? "{}", Http.JsonOptions)?.RoleArn?.Trim();
        }
        catch (JsonException)
        {
            roleArn = null;
        }

        if (roleArn is null || !AuditorRole.ArnPattern().IsMatch(roleArn))
        {
            return Http.Error(400, $"Role ARN must look like arn:aws:iam::123456789012:role/{AuditorRole.Name}.");
        }

        var connection = await _store.Get(request.PathParameters["id"]);

        if (connection is null)
        {
            return Http.Error(404, "Connection not found.");
        }

        if (connection.Verification?.Status == "verified" && connection.RoleArn != roleArn)
        {
            return Http.Error(409, "This Connection is verified; its Role ARN can no longer be changed.");
        }

        connection = connection with { RoleArn = roleArn, Verification = await Verify(roleArn, connection.ExternalId) };

        await _store.Save(connection);

        return Http.Connection(200, connection);
    }

    private async Task<Verification> Verify(string roleArn, string externalId)
    {
        Amazon.SecurityToken.Model.Credentials credentials;

        try
        {
            var assumed = await _sts.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = roleArn,
                RoleSessionName = "cloud-spend-guard-verification",
                ExternalId = externalId,
                DurationSeconds = 900,
            });

            credentials = assumed.Credentials;
        }
        catch (AmazonSecurityTokenServiceException)
        {
            return new Verification("failed", "failed", AssumeRoleFailedMessage, [], DateTimeOffset.UtcNow);
        }

        using var iam = new AmazonIdentityManagementServiceClient(
            new SessionAWSCredentials(credentials.AccessKeyId, credentials.SecretAccessKey, credentials.SessionToken));

        SimulatePrincipalPolicyResponse simulation;

        try
        {
            simulation = await iam.SimulatePrincipalPolicyAsync(new SimulatePrincipalPolicyRequest
            {
                PolicySourceArn = roleArn,
                ActionNames = [.. AuditorRole.RequiredPermissions],
            });
        }
        catch (AmazonIdentityManagementServiceException)
        {
            return new Verification("failed", "ok", SimulateFailedMessage, [], DateTimeOffset.UtcNow);
        }

        var permissions = AuditorRole.RequiredPermissions
            .Select(action => new PermissionResult(action, simulation.EvaluationResults?.Any(result =>
                result.EvalActionName == action && result.EvalDecision == PolicyEvaluationDecisionType.Allowed) == true))
            .ToList();

        var status = permissions.All(permission => permission.Allowed) ? "verified" : "failed";

        return new Verification(status, "ok", null, permissions, DateTimeOffset.UtcNow);
    }
}
