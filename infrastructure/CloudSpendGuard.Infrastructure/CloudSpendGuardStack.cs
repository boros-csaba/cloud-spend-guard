using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;
using HttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;

namespace CloudSpendGuard.Infrastructure;

public class CloudSpendGuardStack : Stack
{
    private const string AuditsProjectPath = "../apps/functions/AuditsFunction";

    public CloudSpendGuardStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        var auditsFunction = new Function(this, "AuditsFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_8,
            Handler = "CloudSpendGuard.Functions.Audits::CloudSpendGuard.Functions.Audits.Function::Handler",
            Code = Code.FromAsset(AuditsProjectPath, new AssetOptions
            {
                Bundling = new BundlingOptions
                {
                    Image = Runtime.DOTNET_8.BundlingImage,
                    Local = new DotNetBundler(AuditsProjectPath),
                },
            }),
            LogGroup = new LogGroup(this, "AuditsFunctionLogGroup", new LogGroupProps
            {
                Retention = RetentionDays.ONE_MONTH,
            }),
        });

        var api = new HttpApi(this, "CloudSpendGuardApi");

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/audits",
            Methods = [HttpMethod.GET],
            Integration = new HttpLambdaIntegration("AuditsIntegration", auditsFunction),
        });

        new CfnOutput(this, "AuditsUrl", new CfnOutputProps { Value = $"{api.Url}audits" });
    }
}
