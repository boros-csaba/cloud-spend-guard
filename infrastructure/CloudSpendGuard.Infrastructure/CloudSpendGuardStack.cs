using Amazon.CDK;
using Constructs;

namespace CloudSpendGuard.Infrastructure;

public class CloudSpendGuardStack : Stack
{
    public CloudSpendGuardStack(Construct scope, string id, IStackProps? props = null)
        : base(scope, id, props)
    {
    }
}
