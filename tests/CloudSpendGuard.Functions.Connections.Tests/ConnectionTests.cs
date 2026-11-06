using Amazon.BCMDataExports.Model;
using CloudSpendGuard.Functions.Connections;

namespace CloudSpendGuard.Functions.Connections.Tests;

public class ConnectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Check Ok = new("role", "ok", null);
    private static readonly Check Failed = new("role", "failed", "Could not assume the role.");
    private static readonly Check Warning = new("accountType", "warning", "Management account.");

    [Fact]
    public void Verification_passes_with_only_ok_and_warning_checks()
    {
        var verification = Verification.From([Ok, Warning], null, Now);

        Assert.Equal("verified", verification.Status);
        Assert.Null(verification.FailingSince);
    }

    [Fact]
    public void Verification_failing_before_ever_passing_has_no_failing_since()
    {
        var first = Verification.From([Failed], null, Now);
        var second = Verification.From([Failed], first, Now.AddMinutes(1));

        Assert.Equal("failed", second.Status);
        Assert.Null(second.FailingSince);
    }

    [Fact]
    public void Verification_failing_after_passing_keeps_the_first_failure_time()
    {
        var passed = Verification.From([Ok], null, Now);
        var firstFailure = Verification.From([Failed], passed, Now.AddDays(1));
        var secondFailure = Verification.From([Failed], firstFailure, Now.AddDays(2));

        Assert.Equal(Now.AddDays(1), secondFailure.FailingSince);
    }

    [Theory]
    [InlineData(null, "pending")]
    [InlineData("never-passed", "pending")]
    [InlineData("verified", "working")]
    [InlineData("broken", "broken")]
    public void State_follows_verification(string? history, string expected)
    {
        var verification = history switch
        {
            "never-passed" => Verification.From([Failed], null, Now),
            "verified" => Verification.From([Ok], null, Now),
            "broken" => Verification.From([Failed], Verification.From([Ok], null, Now), Now.AddDays(1)),
            _ => null,
        };

        Assert.Equal(expected, (NewConnection() with { Verification = verification }).State);
    }

    [Fact]
    public void Launch_url_opens_quick_create_in_us_east_1_with_parameters()
    {
        var url = ConnectionTemplate.LaunchUrl("https://bucket.s3.us-east-1.amazonaws.com/connection-standalone/1.yaml", NewConnection(), "999999999999");

        Assert.StartsWith("https://us-east-1.console.aws.amazon.com/cloudformation/home?region=us-east-1#/stacks/quickcreate?", url);
        Assert.Contains("templateURL=https%3A%2F%2Fbucket.s3.us-east-1.amazonaws.com%2Fconnection-standalone%2F1.yaml", url);
        Assert.Contains("&stackName=cloud-spend-guard-0123456789abcdef0123456789abcdef", url);
        Assert.Contains("&param_ExternalId=ABCDEF0123456789ABCDEF0123456789", url);
        Assert.Contains("&param_TrustedAccountId=999999999999", url);
    }

    [Fact]
    public void Export_with_template_settings_has_no_problems()
    {
        Assert.Empty(ExportSettings.Problems(NewExport("TRUE", "DAILY", "cloud-spend-guard-x"), "cloud-spend-guard-x"));
    }

    [Fact]
    public void Export_with_wrong_settings_lists_each_problem()
    {
        var problems = ExportSettings.Problems(NewExport("FALSE", "HOURLY", "other-bucket"), "cloud-spend-guard-x");

        Assert.Equal(3, problems.Count);
    }

    private static Connection NewConnection() => new(
        "0123456789abcdef0123456789abcdef", "workspace", "standalone", "111111111111",
        "ABCDEF0123456789ABCDEF0123456789", Now, null);

    private static Export NewExport(string includeResources, string granularity, string bucket) => new()
    {
        DataQuery = new DataQuery
        {
            TableConfigurations = new()
            {
                ["COST_AND_USAGE_REPORT"] = new() { ["INCLUDE_RESOURCES"] = includeResources, ["TIME_GRANULARITY"] = granularity },
            },
        },
        DestinationConfigurations = new DestinationConfigurations { S3Destination = new S3Destination { S3Bucket = bucket } },
    };
}
