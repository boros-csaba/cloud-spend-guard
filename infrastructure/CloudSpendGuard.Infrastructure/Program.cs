using Amazon.CDK;
using CloudSpendGuard.Infrastructure;

var app = new App();

Tags.Of(app).Add("Project", "CloudSpendGuard");
Tags.Of(app).Add("Environment", "Production");
Tags.Of(app).Add("ManagedBy", "CDK");

new CloudSpendGuardStack(app, "CloudSpendGuardStack");
app.Synth();
