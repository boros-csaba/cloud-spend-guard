using Amazon.CDK;
using Amazon.CDK.AWS.Route53;
using Constructs;

namespace CloudSpendGuard.Infrastructure;

public class CloudSpendGuardDnsStack : Stack
{
    public const string DomainName = "cloudspendguard.com";

    public HostedZone HostedZone { get; }

    public CloudSpendGuardDnsStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
        HostedZone = new HostedZone(this, "HostedZone", new HostedZoneProps
        {
            ZoneName = DomainName,
        });

        HostedZone.ApplyRemovalPolicy(RemovalPolicy.RETAIN);

        new CfnOutput(this, "NameServers", new CfnOutputProps
        {
            Value = Fn.Join(", ", HostedZone.HostedZoneNameServers!),
        });
    }
}
