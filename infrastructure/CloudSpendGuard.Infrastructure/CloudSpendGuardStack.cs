using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using AssetOptions = Amazon.CDK.AWS.S3.Assets.AssetOptions;
using Distribution = Amazon.CDK.AWS.CloudFront.Distribution;
using Function = Amazon.CDK.AWS.Lambda.Function;
using FunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;
using CloudFrontFunction = Amazon.CDK.AWS.CloudFront.Function;
using CloudFrontFunctionProps = Amazon.CDK.AWS.CloudFront.FunctionProps;
using HttpMethod = Amazon.CDK.AWS.Apigatewayv2.HttpMethod;

namespace CloudSpendGuard.Infrastructure;

public class CloudSpendGuardStackProps : StackProps
{
    public required IHostedZone HostedZone { get; init; }
}

public class CloudSpendGuardStack : Stack
{
    private const string AuditsProjectPath = "../apps/functions/AuditsFunction";
    private const string WebProjectPath = "../apps/web";

    public CloudSpendGuardStack(Construct scope, string id, CloudSpendGuardStackProps props)
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

        var domainName = props.HostedZone.ZoneName;
        var wwwDomainName = $"www.{domainName}";

        var siteBucket = new Bucket(this, "SiteBucket", new BucketProps
        {
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
        });

        var certificate = new Certificate(this, "SiteCertificate", new CertificateProps
        {
            DomainName = domainName,
            SubjectAlternativeNames = [wwwDomainName],
            Validation = CertificateValidation.FromDns(props.HostedZone),
        });

        var redirectToApex = new FunctionAssociation
        {
            EventType = FunctionEventType.VIEWER_REQUEST,
            Function = new CloudFrontFunction(this, "RedirectToApex", new CloudFrontFunctionProps
            {
                Runtime = FunctionRuntime.JS_2_0,
                Code = FunctionCode.FromInline($$"""
                    function handler(event) {
                      var request = event.request;

                      if (request.headers.host.value !== '{{wwwDomainName}}') {
                        return request;
                      }

                      return {
                        statusCode: 301,
                        statusDescription: 'Moved Permanently',
                        headers: { location: { value: 'https://{{domainName}}' + request.uri } },
                      };
                    }
                    """),
            }),
        };

        var distribution = new Distribution(this, "SiteDistribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(siteBucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                FunctionAssociations = [redirectToApex],
            },
            AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>
            {
                ["/audits*"] = new BehaviorOptions
                {
                    Origin = new HttpOrigin(Fn.Select(2, Fn.Split("/", api.Url!))),
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                    AllowedMethods = AllowedMethods.ALLOW_ALL,
                    CachePolicy = CachePolicy.CACHING_DISABLED,
                    OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
                    FunctionAssociations = [redirectToApex],
                },
            },
            DefaultRootObject = "index.html",
            DomainNames = [domainName, wwwDomainName],
            Certificate = certificate,
            PriceClass = PriceClass.PRICE_CLASS_100,
        });

        new BucketDeployment(this, "SiteDeployment", new BucketDeploymentProps
        {
            Sources = [Source.Asset(WebProjectPath, new AssetOptions
            {
                Bundling = new BundlingOptions
                {
                    Image = DockerImage.FromRegistry("public.ecr.aws/docker/library/node:24"),
                    Local = new ViteBundler(WebProjectPath),
                },
            })],
            DestinationBucket = siteBucket,
            Distribution = distribution,
            DistributionPaths = ["/*"],
        });

        var aliasTarget = RecordTarget.FromAlias(new CloudFrontTarget(distribution));

        foreach (var (recordId, recordName) in new[] { ("Apex", domainName), ("Www", wwwDomainName) })
        {
            new ARecord(this, $"{recordId}AliasRecord", new ARecordProps
            {
                Zone = props.HostedZone,
                RecordName = recordName,
                Target = aliasTarget,
            });

            new AaaaRecord(this, $"{recordId}AliasRecordIpv6", new AaaaRecordProps
            {
                Zone = props.HostedZone,
                RecordName = recordName,
                Target = aliasTarget,
            });
        }

        new CfnOutput(this, "AuditsUrl", new CfnOutputProps { Value = $"{api.Url}audits" });
        new CfnOutput(this, "SiteUrl", new CfnOutputProps { Value = $"https://{domainName}" });
    }
}
