using CloudSpendGuard.Functions.Connections;

namespace CloudSpendGuard.Functions.Connections.Tests;

public class SnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly RegionScan Scanned = new("us-east-1", "scanned", null);
    private static readonly RegionScan Skipped = new("us-east-1", "skipped", "Access denied.");

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
        var kept = Snapshot.Build("111111111111", [Skipped], [], first, Now.AddDays(1)).Resources.Single();

        Assert.Null(kept.DeletedAt);
        Assert.Equal(new Idle("unattached", Now), kept.Idle);
    }

    private static Snapshot Build(IReadOnlyList<Resource> collected, Snapshot? previous, DateTimeOffset now) =>
        Snapshot.Build("111111111111", [Scanned], collected, previous, now);

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
}
