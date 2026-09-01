using Amazon.CDK;
using CloudSpendGuard.Infrastructure;

var app = new App();
new CloudSpendGuardStack(app, "CloudSpendGuardStack");
app.Synth();
