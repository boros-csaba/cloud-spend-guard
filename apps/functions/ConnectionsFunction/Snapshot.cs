namespace CloudSpendGuard.Functions.Connections;

public record Idle(string Reason, DateTimeOffset Since);

public record Resource(
    string Arn,
    string Type,
    string Region,
    string? Name,
    IReadOnlyDictionary<string, string> Details,
    IReadOnlyDictionary<string, string> Tags,
    DateTimeOffset FirstSeen,
    Idle? Idle,
    DateTimeOffset? DeletedAt);

public record RegionScan(string Region, string Status, string? Message);

public record Snapshot(string AccountId, DateTimeOffset TakenAt, IReadOnlyList<RegionScan> Regions, IReadOnlyList<Resource> Resources)
{
    public static Snapshot Build(
        string accountId,
        IReadOnlyList<RegionScan> regions,
        IReadOnlyList<Resource> collected,
        Snapshot? previous,
        DateTimeOffset now)
    {
        var previousByArn = previous?.Resources.ToDictionary(resource => resource.Arn) ?? [];
        var collectedArns = collected.Select(resource => resource.Arn).ToHashSet();
        var scannedRegions = regions.Where(region => region.Status == "scanned").Select(region => region.Region).ToHashSet();

        var current = collected.Select(resource =>
        {
            var before = previousByArn.GetValueOrDefault(resource.Arn);
            var reason = IdleRules.Reason(resource);
            var idle = reason is null ? null : new Idle(reason, before?.Idle?.Reason == reason ? before.Idle.Since : now);

            return resource with { FirstSeen = before?.FirstSeen ?? now, Idle = idle, DeletedAt = null };
        });

        var missing = (previous?.Resources ?? [])
            .Where(resource => !collectedArns.Contains(resource.Arn))
            .Select(resource => scannedRegions.Contains(resource.Region) && resource.DeletedAt is null
                ? resource with { Idle = null, DeletedAt = now }
                : resource)
            .Where(resource => resource.DeletedAt is null || now < EndOfFollowingMonth(resource.DeletedAt.Value));

        return new Snapshot(accountId, now, regions, [.. current, .. missing]);
    }

    private static DateTimeOffset EndOfFollowingMonth(DateTimeOffset date) =>
        new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(2);
}

public static class IdleRules
{
    public static string? Reason(Resource resource) => resource.Type switch
    {
        "ec2:volume" when resource.Details.GetValueOrDefault("state") == "available" => "unattached",
        _ => null,
    };
}
