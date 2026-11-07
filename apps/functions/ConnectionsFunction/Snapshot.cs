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

public record Scan(string? Region, string Type, string Status, string? Message)
{
    public bool Covers(Resource resource) =>
        Status == "scanned" && Type == resource.Type && (Region is null || Region == resource.Region);
}

public record Snapshot(string AccountId, DateTimeOffset TakenAt, IReadOnlyList<Scan> Scans, IReadOnlyList<Resource> Resources)
{
    public static Snapshot Build(
        string accountId,
        IReadOnlyList<Scan> scans,
        IReadOnlyList<Resource> collected,
        Snapshot? previous,
        DateTimeOffset now)
    {
        var previousByArn = previous?.Resources.ToDictionary(resource => resource.Arn) ?? [];
        var collectedArns = collected.Select(resource => resource.Arn).ToHashSet();
        var idle = IdleRules.Find(collected, now);

        var current = collected.Select(resource =>
        {
            var before = previousByArn.GetValueOrDefault(resource.Arn);
            var verdict = idle.GetValueOrDefault(resource.Arn);
            var since = verdict.Since ?? (before?.Idle?.Reason == verdict.Reason ? before?.Idle?.Since : null) ?? now;

            return resource with
            {
                FirstSeen = before?.FirstSeen ?? now,
                Idle = verdict.Reason is null ? null : new Idle(verdict.Reason, since),
                DeletedAt = null,
            };
        });

        var missing = (previous?.Resources ?? [])
            .Where(resource => !collectedArns.Contains(resource.Arn))
            .Select(resource => resource.DeletedAt is null && scans.Any(scan => scan.Covers(resource))
                ? resource with { Idle = null, DeletedAt = now }
                : resource)
            .Where(resource => resource.DeletedAt is null || now < EndOfFollowingMonth(resource.DeletedAt.Value));

        return new Snapshot(accountId, now, scans, [.. current, .. missing]);
    }

    private static DateTimeOffset EndOfFollowingMonth(DateTimeOffset date) =>
        new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(2);
}

public static class IdleRules
{
    private static readonly TimeSpan StoppedFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan TunnelsDownFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan OrphanedAfter = TimeSpan.FromDays(90);

    public static Dictionary<string, (string? Reason, DateTimeOffset? Since)> Find(IReadOnlyList<Resource> resources, DateTimeOffset now)
    {
        var verdicts = new Dictionary<string, (string? Reason, DateTimeOffset? Since)>();
        var idleInstances = new Dictionary<string, DateTimeOffset?>();

        foreach (var instance in resources.Where(resource => resource.Type == "ec2:instance"))
        {
            if (instance.Details.GetValueOrDefault("state") != "stopped" || instance.Details.ContainsKey("warmPool"))
            {
                continue;
            }

            DateTimeOffset? stoppedAt = DateTimeOffset.TryParse(instance.Details.GetValueOrDefault("stoppedAt"), out var parsed) ? parsed : null;

            if (stoppedAt is null || now - stoppedAt >= StoppedFor)
            {
                verdicts[instance.Arn] = ("stopped", stoppedAt);
                idleInstances[instance.Details["instanceId"]] = stoppedAt;
            }
        }

        foreach (var volume in resources.Where(resource => resource.Type == "ec2:volume"))
        {
            if (volume.Details.GetValueOrDefault("state") == "available")
            {
                verdicts[volume.Arn] = ("unattached", null);
            }
            else if (volume.Details.TryGetValue("attachedTo", out var instanceId) && idleInstances.TryGetValue(instanceId, out var stoppedAt))
            {
                verdicts[volume.Arn] = ("instanceStopped", stoppedAt);
            }
        }

        foreach (var resource in resources)
        {
            var details = resource.Details;
            (string? Reason, DateTimeOffset? Since) verdict = resource.Type switch
            {
                "ec2:elastic-ip" when !details.ContainsKey("associationId") => ("unassociated", null),
                "ec2:natgateway" when details.GetValueOrDefault("state") == "available" && details.GetValueOrDefault("routed") == "false" =>
                    ("noRoutes", null),
                "ec2:vpn-connection" when Date(details, "tunnelsDownSince") is { } downSince && now - downSince >= TunnelsDownFor =>
                    ("tunnelsDown", downSince),
                "elasticloadbalancing:loadbalancer" or "elasticloadbalancing:classic-loadbalancer"
                    when details.GetValueOrDefault("state", "active") == "active" && details.GetValueOrDefault("registeredTargets") == "0" =>
                    ("noTargets", null),
                "elasticloadbalancing:loadbalancer" or "elasticloadbalancing:classic-loadbalancer"
                    when details.GetValueOrDefault("state", "active") == "active" && details.GetValueOrDefault("healthyTargets") == "0" =>
                    ("noHealthyTargets", null),
                "rds:db" when details.GetValueOrDefault("status") == "stopped" => ("stopped", null),
                "ec2:snapshot" when details.GetValueOrDefault("state") == "completed"
                    && details.GetValueOrDefault("volumeExists") == "false"
                    && details.GetValueOrDefault("usedByImage") == "false"
                    && !details.ContainsKey("managedBy")
                    && Date(details, "startedAt") is { } startedAt && now - startedAt >= OrphanedAfter =>
                    ("orphaned", null),
                _ => (null, null),
            };

            if (verdict.Reason is not null)
            {
                verdicts[resource.Arn] = verdict;
            }
        }

        return verdicts;
    }

    private static DateTimeOffset? Date(IReadOnlyDictionary<string, string> details, string key) =>
        DateTimeOffset.TryParse(details.GetValueOrDefault(key), out var date) ? date : null;
}
