using CloudSpendGuard.Functions.Connections;

namespace CloudSpendGuard.Functions.Connections.Tests;

public class SnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Scan[] Scanned =
    [
        new("us-east-1", "ec2:volume", "scanned", null),
        new("us-east-1", "ec2:instance", "scanned", null),
        new(null, "cloudfront:distribution", "scanned", null),
    ];

    [Fact]
    public void Unattached_volume_is_idle_from_now()
    {
        var snapshot = Build([Volume("available")], null, Now);

        Assert.Equal(new Idle("unattached", Now), snapshot.Resources.Single().Idle);
        Assert.Equal(Now, snapshot.Resources.Single().FirstSeen);
    }

    [Fact]
    public void Attached_volume_is_not_idle()
    {
        Assert.Null(Build([Volume("in-use")], null, Now).Resources.Single().Idle);
    }

    [Fact]
    public void Idle_since_and_first_seen_carry_forward()
    {
        var first = Build([Volume("available")], null, Now);
        var second = Build([Volume("available")], first, Now.AddDays(1));

        Assert.Equal(Now, second.Resources.Single().Idle!.Since);
        Assert.Equal(Now, second.Resources.Single().FirstSeen);
    }

    [Fact]
    public void Reattached_volume_is_no_longer_idle()
    {
        var first = Build([Volume("available")], null, Now);

        Assert.Null(Build([Volume("in-use")], first, Now.AddDays(1)).Resources.Single().Idle);
    }

    [Fact]
    public void Missing_resource_in_scanned_region_is_deleted()
    {
        var first = Build([Volume("available")], null, Now);
        var deleted = Build([], first, Now.AddDays(1)).Resources.Single();

        Assert.Equal(Now.AddDays(1), deleted.DeletedAt);
        Assert.Null(deleted.Idle);
    }

    [Fact]
    public void Deleted_resource_keeps_its_deletion_date()
    {
        var first = Build([Volume("available")], null, Now);
        var deleted = Build([], first, Now.AddDays(1));

        Assert.Equal(Now.AddDays(1), Build([], deleted, Now.AddDays(2)).Resources.Single().DeletedAt);
    }

    [Fact]
    public void Deleted_resource_stays_until_the_end_of_the_following_month()
    {
        var first = Build([Volume("available")], null, Now);
        var deleted = Build([], first, Now);

        Assert.Single(Build([], deleted, new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.Zero)).Resources);
        Assert.Empty(Build([], deleted, new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)).Resources);
    }

    [Fact]
    public void Resource_in_a_region_that_was_not_scanned_is_kept()
    {
        var first = Build([Volume("available")], null, Now);
        var kept = Snapshot.Build("111111111111", [new Scan("us-east-1", "ec2:volume", "skipped", "Access denied.")], [], first, Now.AddDays(1)).Resources.Single();

        Assert.Null(kept.DeletedAt);
        Assert.Equal(new Idle("unattached", Now), kept.Idle);
    }

    [Fact]
    public void Failed_scan_of_one_type_keeps_only_that_type()
    {
        var first = Build([Volume("available"), Instance("running")], null, Now);
        var scans = new Scan[] { new("us-east-1", "ec2:volume", "scanned", null), new("us-east-1", "ec2:instance", "failed", "Throttled.") };
        var second = Snapshot.Build("111111111111", scans, [], first, Now.AddDays(1));

        Assert.NotNull(second.Resources.Single(resource => resource.Type == "ec2:volume").DeletedAt);
        Assert.Null(second.Resources.Single(resource => resource.Type == "ec2:instance").DeletedAt);
    }

    [Fact]
    public void Global_scan_covers_every_region()
    {
        var distribution = Volume("available") with { Arn = "arn:aws:cloudfront::111111111111:distribution/E1", Type = "cloudfront:distribution", Region = "global" };
        var first = Build([distribution], null, Now);

        Assert.NotNull(Build([], first, Now.AddDays(1)).Resources.Single().DeletedAt);
    }

    [Fact]
    public void Instance_stopped_for_a_week_is_idle_since_it_stopped()
    {
        var stoppedAt = Now.AddDays(-8);
        var snapshot = Build([Instance("stopped", stoppedAt)], null, Now);

        Assert.Equal(new Idle("stopped", stoppedAt), snapshot.Resources.Single().Idle);
    }

    [Fact]
    public void Instance_stopped_for_less_than_a_week_is_not_idle()
    {
        Assert.Null(Build([Instance("stopped", Now.AddDays(-3))], null, Now).Resources.Single().Idle);
    }

    [Fact]
    public void Instance_in_a_warm_pool_is_not_idle()
    {
        var instance = Instance("stopped", Now.AddDays(-30));
        instance = instance with { Details = new Dictionary<string, string>(instance.Details) { ["warmPool"] = "true" } };

        Assert.Null(Build([instance], null, Now).Resources.Single().Idle);
    }

    [Fact]
    public void Volume_of_an_idle_instance_is_idle_since_the_instance_stopped()
    {
        var stoppedAt = Now.AddDays(-8);
        var volume = Volume("in-use") with { Details = new Dictionary<string, string> { ["state"] = "in-use", ["attachedTo"] = "i-1" } };
        var snapshot = Build([Instance("stopped", stoppedAt), volume], null, Now);

        Assert.Equal(new Idle("instanceStopped", stoppedAt), snapshot.Resources.Single(resource => resource.Type == "ec2:volume").Idle);
    }

    [Fact]
    public void Stop_date_is_read_from_the_state_transition_reason()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 12, 34, 56, TimeSpan.Zero), InstanceCollector.StoppedAt("User initiated (2026-10-01 12:34:56 GMT)"));
        Assert.Null(InstanceCollector.StoppedAt(""));
    }

    [Fact]
    public void Elastic_ip_without_association_is_idle()
    {
        Assert.Equal("unassociated", Verdict(Of("ec2:elastic-ip", [])));
        Assert.Null(Verdict(Of("ec2:elastic-ip", ("associationId", "eipassoc-1"))));
    }

    [Fact]
    public void Nat_gateway_without_routes_is_idle()
    {
        Assert.Equal("noRoutes", Verdict(Of("ec2:natgateway", ("state", "available"), ("routed", "false"))));
        Assert.Null(Verdict(Of("ec2:natgateway", ("state", "available"), ("routed", "true"))));
        Assert.Null(Verdict(Of("ec2:natgateway", ("state", "pending"), ("routed", "false"))));
    }

    [Fact]
    public void Vpn_connection_is_idle_a_week_after_its_tunnels_went_down()
    {
        var downSince = Now.AddDays(-8);
        var idle = IdleRules.Find([Of("ec2:vpn-connection", ("tunnelsDownSince", downSince.ToString("O")))], Now).Single().Value;

        Assert.Equal(("tunnelsDown", downSince), idle);
        Assert.Null(Verdict(Of("ec2:vpn-connection", ("tunnelsDownSince", Now.AddDays(-3).ToString("O")))));
        Assert.Null(Verdict(Of("ec2:vpn-connection", [])));
    }

    [Fact]
    public void Load_balancer_without_targets_or_healthy_targets_is_idle()
    {
        Assert.Equal("noTargets", Verdict(Of("elasticloadbalancing:loadbalancer", ("state", "active"), ("registeredTargets", "0"), ("healthyTargets", "0"))));
        Assert.Equal("noHealthyTargets", Verdict(Of("elasticloadbalancing:loadbalancer", ("state", "active"), ("registeredTargets", "2"), ("healthyTargets", "0"))));
        Assert.Null(Verdict(Of("elasticloadbalancing:loadbalancer", ("state", "active"), ("registeredTargets", "2"), ("healthyTargets", "1"))));
        Assert.Null(Verdict(Of("elasticloadbalancing:loadbalancer", ("state", "provisioning"), ("registeredTargets", "0"), ("healthyTargets", "0"))));
        Assert.Equal("noTargets", Verdict(Of("elasticloadbalancing:classic-loadbalancer", ("registeredTargets", "0"), ("healthyTargets", "0"))));
    }

    [Fact]
    public void Stopped_database_is_idle()
    {
        Assert.Equal("stopped", Verdict(Of("rds:db", ("status", "stopped"))));
        Assert.Null(Verdict(Of("rds:db", ("status", "available"))));
    }

    [Fact]
    public void Old_ebs_snapshot_of_a_deleted_volume_is_idle_unless_an_image_or_backup_uses_it()
    {
        (string, string)[] orphaned =
        [
            ("state", "completed"), ("volumeExists", "false"), ("usedByImage", "false"), ("startedAt", Now.AddDays(-91).ToString("O")),
        ];

        Assert.Equal("orphaned", Verdict(Of("ec2:snapshot", orphaned)));
        Assert.Null(Verdict(Of("ec2:snapshot", [.. orphaned, ("managedBy", "AWS Backup")])));
        Assert.Null(Verdict(Of("ec2:snapshot", [.. orphaned.Where(detail => detail.Item1 != "usedByImage"), ("usedByImage", "true")])));
        Assert.Null(Verdict(Of("ec2:snapshot", [.. orphaned.Where(detail => detail.Item1 != "volumeExists"), ("volumeExists", "true")])));
        Assert.Null(Verdict(Of("ec2:snapshot", [.. orphaned.Where(detail => detail.Item1 != "startedAt"), ("startedAt", Now.AddDays(-30).ToString("O"))])));
    }

    private static string? Verdict(Resource resource) =>
        IdleRules.Find([resource], Now).GetValueOrDefault(resource.Arn).Reason;

    private static Resource Of(string type, params (string Key, string Value)[] details) => new(
        $"arn:aws:test:us-east-1:111111111111:{type}/1",
        type,
        "us-east-1",
        null,
        details.ToDictionary(detail => detail.Key, detail => detail.Value),
        new Dictionary<string, string>(),
        default,
        null,
        null);

    private static Snapshot Build(IReadOnlyList<Resource> collected, Snapshot? previous, DateTimeOffset now) =>
        Snapshot.Build("111111111111", Scanned, collected, previous, now);

    private static Resource Volume(string state) => new(
        "arn:aws:ec2:us-east-1:111111111111:volume/vol-1",
        "ec2:volume",
        "us-east-1",
        null,
        new Dictionary<string, string> { ["state"] = state },
        new Dictionary<string, string>(),
        default,
        null,
        null);

    private static Resource Instance(string state, DateTimeOffset? stoppedAt = null)
    {
        var details = new Dictionary<string, string> { ["instanceId"] = "i-1", ["state"] = state };

        if (stoppedAt is not null)
        {
            details["stoppedAt"] = stoppedAt.Value.ToString("O");
        }

        return new Resource(
            "arn:aws:ec2:us-east-1:111111111111:instance/i-1",
            "ec2:instance",
            "us-east-1",
            null,
            details,
            new Dictionary<string, string>(),
            default,
            null,
            null);
    }
}
