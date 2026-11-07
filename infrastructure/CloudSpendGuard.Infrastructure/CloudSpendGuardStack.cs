using Amazon.CDK;
using Amazon.CDK.AWS.Apigatewayv2;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using Amazon.CDK.AwsApigatewayv2Authorizers;
using Amazon.CDK.AwsApigatewayv2Integrations;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;
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
    private const string ConnectionsProjectPath = "../apps/functions/ConnectionsFunction";
    private const string ConnectionsHandlerPrefix = "CloudSpendGuard.Functions.Connections::CloudSpendGuard.Functions.Connections";
    private const string WebProjectPath = "../apps/web";
    private const string TemplatesPath = "templates";
    // Bump on every change to a template: each version is kept under its own prefix for existing customers.
    private const string TemplateVersion = "1";

    public CloudSpendGuardStack(Construct scope, string id, CloudSpendGuardStackProps props)
        : base(scope, id, props)
    {
        var auditsFunction = new Function(this, "AuditsFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = "CloudSpendGuard.Functions.Audits::CloudSpendGuard.Functions.Audits.Function::Handler",
            Code = Code.FromAsset(AuditsProjectPath, new AssetOptions
            {
                Bundling = new BundlingOptions
                {
                    Image = Runtime.DOTNET_10.BundlingImage,
                    Local = new DotNetBundler(AuditsProjectPath),
                },
            }),
            LogGroup = new LogGroup(this, "AuditsFunctionLogGroup", new LogGroupProps
            {
                Retention = RetentionDays.ONE_MONTH,
            }),
        });

        var workspacesTable = new Table(this, "WorkspacesTable", new TableProps
        {
            PartitionKey = new Attribute { Name = "pk", Type = AttributeType.STRING },
            SortKey = new Attribute { Name = "sk", Type = AttributeType.STRING },
            BillingMode = BillingMode.PAY_PER_REQUEST,
            RemovalPolicy = RemovalPolicy.RETAIN,
        });

        var templatesBucket = new Bucket(this, "TemplatesBucket", new BucketProps
        {
            PublicReadAccess = true,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ACLS_ONLY,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
        });

        new BucketDeployment(this, "TemplatesDeployment", new BucketDeploymentProps
        {
            Sources = [Source.Asset(TemplatesPath)],
            DestinationBucket = templatesBucket,
            DestinationKeyPrefix = $"v{TemplateVersion}/",
            Prune = false,
        });

        var connectionsCode = Code.FromAsset(ConnectionsProjectPath, new AssetOptions
        {
            Bundling = new BundlingOptions
            {
                Image = Runtime.DOTNET_10.BundlingImage,
                Local = new DotNetBundler(ConnectionsProjectPath),
            },
        });

        var snapshotsBucket = new Bucket(this, "SnapshotsBucket", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            EnforceSSL = true,
            RemovalPolicy = RemovalPolicy.RETAIN,
        });

        var assumeConnectionRole = new PolicyStatement(new PolicyStatementProps
        {
            Actions = ["sts:AssumeRole"],
            Resources = ["arn:aws:iam::*:role/cloud-spend-guard-*"],
        });

        var snapshotRunnerFunction = new Function(this, "SnapshotRunnerFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = $"{ConnectionsHandlerPrefix}.SnapshotRunner::Run",
            Code = connectionsCode,
            Environment = new Dictionary<string, string>
            {
                ["TABLE_NAME"] = workspacesTable.TableName,
                ["SNAPSHOTS_BUCKET"] = snapshotsBucket.BucketName,
            },
            MemorySize = 1024,
            Timeout = Duration.Minutes(5),
            RetryAttempts = 0,
            LogGroup = new LogGroup(this, "SnapshotRunnerFunctionLogGroup", new LogGroupProps
            {
                Retention = RetentionDays.ONE_MONTH,
            }),
        });

        workspacesTable.GrantReadWriteData(snapshotRunnerFunction);
        snapshotsBucket.GrantReadWrite(snapshotRunnerFunction);
        snapshotRunnerFunction.AddToRolePolicy(assumeConnectionRole);

        var connectionsEnvironment = new Dictionary<string, string>
        {
            ["TABLE_NAME"] = workspacesTable.TableName,
            ["TRUSTED_ACCOUNT_ID"] = Account,
            ["TEMPLATE_URL"] = $"https://{templatesBucket.BucketRegionalDomainName}/v{TemplateVersion}/connection-standalone.yaml",
            ["SNAPSHOTS_BUCKET"] = snapshotsBucket.BucketName,
            ["SNAPSHOT_RUNNER_FUNCTION"] = snapshotRunnerFunction.FunctionName,
        };

        var connectionsFunction = new Function(this, "ConnectionsFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = $"{ConnectionsHandlerPrefix}.ConnectionsHandler::Handle",
            Code = connectionsCode,
            Environment = connectionsEnvironment,
            MemorySize = 512,
            Timeout = Duration.Seconds(15),
            LogGroup = new LogGroup(this, "ConnectionsFunctionLogGroup", new LogGroupProps
            {
                Retention = RetentionDays.ONE_MONTH,
            }),
        });

        var verificationFunction = new Function(this, "VerificationFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = $"{ConnectionsHandlerPrefix}.VerificationHandler::Handle",
            Code = connectionsCode,
            Environment = connectionsEnvironment,
            MemorySize = 512,
            Timeout = Duration.Seconds(30),
            LogGroup = new LogGroup(this, "VerificationFunctionLogGroup", new LogGroupProps
            {
                Retention = RetentionDays.ONE_MONTH,
            }),
        });

        var snapshotsFunction = new Function(this, "SnapshotsFunction", new FunctionProps
        {
            Runtime = Runtime.DOTNET_10,
            Handler = $"{ConnectionsHandlerPrefix}.SnapshotsHandler::Handle",
            Code = connectionsCode,
            Environment = connectionsEnvironment,
            MemorySize = 512,
            Timeout = Duration.Seconds(15),
            LogGroup = new LogGroup(this, "SnapshotsFunctionLogGroup", new LogGroupProps
            {
                Retention = RetentionDays.ONE_MONTH,
            }),
        });

        workspacesTable.GrantReadWriteData(connectionsFunction);
        workspacesTable.GrantReadWriteData(verificationFunction);
        workspacesTable.GrantReadWriteData(snapshotsFunction);
        snapshotsBucket.GrantRead(snapshotsFunction);
        snapshotRunnerFunction.GrantInvoke(verificationFunction);
        snapshotRunnerFunction.GrantInvoke(snapshotsFunction);

        verificationFunction.AddToRolePolicy(assumeConnectionRole);

        var api = new HttpApi(this, "CloudSpendGuardApi");

        ((CfnStage)api.DefaultStage!.Node.DefaultChild!).DefaultRouteSettings = new CfnStage.RouteSettingsProperty
        {
            ThrottlingRateLimit = 5,
            ThrottlingBurstLimit = 10,
        };

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/api/audits",
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

        var serveSite = new FunctionAssociation
        {
            EventType = FunctionEventType.VIEWER_REQUEST,
            Function = new CloudFrontFunction(this, "ServeSite", new CloudFrontFunctionProps
            {
                Runtime = FunctionRuntime.JS_2_0,
                Code = FunctionCode.FromInline($$"""
                    function handler(event) {
                      var request = event.request;

                      if (request.headers.host.value === '{{wwwDomainName}}') {
                        return {
                          statusCode: 301,
                          statusDescription: 'Moved Permanently',
                          headers: { location: { value: 'https://{{domainName}}' + request.uri } },
                        };
                      }

                      if (!request.uri.split('/').pop().includes('.')) {
                        request.uri = '/index.html';
                      }

                      return request;
                    }
                    """),
            }),
        };

        var apiBehavior = new BehaviorOptions
        {
            Origin = new HttpOrigin(Fn.Select(2, Fn.Split("/", api.Url!))),
            ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
            AllowedMethods = AllowedMethods.ALLOW_ALL,
            CachePolicy = new CachePolicy(this, "ApiCachePolicy", new CachePolicyProps
            {
                Comment = "Forwards Authorization on GET; API responses are not cached.",
                DefaultTtl = Duration.Seconds(0),
                MinTtl = Duration.Seconds(0),
                MaxTtl = Duration.Seconds(1),
                HeaderBehavior = CacheHeaderBehavior.AllowList("Authorization"),
                QueryStringBehavior = CacheQueryStringBehavior.All(),
            }),
            OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_EXCEPT_HOST_HEADER,
            FunctionAssociations = [redirectToApex],
        };

        var distribution = new Distribution(this, "SiteDistribution", new DistributionProps
        {
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(siteBucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                FunctionAssociations = [serveSite],
            },
            AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>
            {
                ["/api/*"] = apiBehavior,
            },
            DefaultRootObject = "index.html",
            DomainNames = [domainName, wwwDomainName],
            Certificate = certificate,
            PriceClass = PriceClass.PRICE_CLASS_100,
        });

        var userPool = new UserPool(this, "UserPool", new UserPoolProps
        {
            SelfSignUpEnabled = true,
            SignInAliases = new SignInAliases { Email = true },
            AutoVerify = new AutoVerifiedAttrs { Email = true },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            FeaturePlan = FeaturePlan.ESSENTIALS,
            DeletionProtection = true,
            RemovalPolicy = RemovalPolicy.RETAIN,
        });

        var googleClientId = new CfnParameter(this, "GoogleClientId");
        var googleClientSecret = new CfnParameter(this, "GoogleClientSecret", new CfnParameterProps { NoEcho = true });

        var googleProvider = new UserPoolIdentityProviderGoogle(this, "GoogleIdentityProvider", new UserPoolIdentityProviderGoogleProps
        {
            UserPool = userPool,
            ClientId = googleClientId.ValueAsString,
            ClientSecretValue = SecretValue.CfnParameter(googleClientSecret),
            Scopes = ["openid", "email", "profile"],
            AttributeMapping = new AttributeMapping
            {
                Email = ProviderAttribute.GOOGLE_EMAIL,
                EmailVerified = ProviderAttribute.GOOGLE_EMAIL_VERIFIED,
                Fullname = ProviderAttribute.GOOGLE_NAME,
                ProfilePicture = ProviderAttribute.GOOGLE_PICTURE,
            },
        });

        var authRedirectUrls = new[] { $"https://{domainName}", "http://localhost:5173" };

        var userPoolClient = userPool.AddClient("WebClient", new UserPoolClientOptions
        {
            SupportedIdentityProviders = [UserPoolClientIdentityProvider.COGNITO, UserPoolClientIdentityProvider.GOOGLE],
            OAuth = new OAuthSettings
            {
                Flows = new OAuthFlows { AuthorizationCodeGrant = true },
                Scopes = [OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.PROFILE, OAuthScope.COGNITO_ADMIN],
                CallbackUrls = authRedirectUrls,
                LogoutUrls = authRedirectUrls,
            },
        });

        userPoolClient.Node.AddDependency(googleProvider);

        var authorizer = new HttpUserPoolAuthorizer("UserPoolAuthorizer", userPool, new HttpUserPoolAuthorizerProps
        {
            UserPoolClients = [userPoolClient],
        });

        var connectionsIntegration = new HttpLambdaIntegration("ConnectionsIntegration", connectionsFunction);

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/api/connections",
            Methods = [HttpMethod.GET, HttpMethod.POST],
            Integration = connectionsIntegration,
            Authorizer = authorizer,
        });

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/api/connections/{id}",
            Methods = [HttpMethod.GET, HttpMethod.DELETE],
            Integration = connectionsIntegration,
            Authorizer = authorizer,
        });

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/api/connections/{id}/verifications",
            Methods = [HttpMethod.POST],
            Integration = new HttpLambdaIntegration("VerificationIntegration", verificationFunction),
            Authorizer = authorizer,
        });

        var snapshotsIntegration = new HttpLambdaIntegration("SnapshotsIntegration", snapshotsFunction);

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/api/connections/{id}/snapshots",
            Methods = [HttpMethod.POST],
            Integration = snapshotsIntegration,
            Authorizer = authorizer,
        });

        api.AddRoutes(new AddRoutesOptions
        {
            Path = "/api/snapshots",
            Methods = [HttpMethod.GET],
            Integration = snapshotsIntegration,
            Authorizer = authorizer,
        });

        var userPoolDomain = userPool.AddDomain("UserPoolDomain", new UserPoolDomainOptions
        {
            CognitoDomain = new CognitoDomainOptions { DomainPrefix = "cloudspendguard" },
            ManagedLoginVersion = ManagedLoginVersion.NEWER_MANAGED_LOGIN,
        });

        new CfnManagedLoginBranding(this, "ManagedLoginBranding", new CfnManagedLoginBrandingProps
        {
            UserPoolId = userPool.UserPoolId,
            ClientId = userPoolClient.UserPoolClientId,
            UseCognitoProvidedValues = true,
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
            }), Source.JsonData("config.json", new Dictionary<string, object>
            {
                ["authority"] = userPool.UserPoolProviderUrl,
                ["clientId"] = userPoolClient.UserPoolClientId,
                ["loginDomain"] = userPoolDomain.BaseUrl(),
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
