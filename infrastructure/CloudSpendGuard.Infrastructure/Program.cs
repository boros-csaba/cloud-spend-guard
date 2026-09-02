using Amazon.CDK;
using CloudSpendGuard.Infrastructure;

var app = new App();

Tags.Of(app).Add("Project", "CloudSpendGuard");
Tags.Of(app).Add("Environment", "Production");
Tags.Of(app).Add("ManagedBy", "CDK");

var dnsStack = new CloudSpendGuardDnsStack(app, "CloudSpendGuardDnsStack");

new CloudSpendGuardStack(app, "CloudSpendGuardStack", new CloudSpendGuardStackProps
{
    HostedZone = dnsStack.HostedZone,
});

app.Synth();
