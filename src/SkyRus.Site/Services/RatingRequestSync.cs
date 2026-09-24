using SkyRus.Site.Data;
using SkyRus.Site.Network;

namespace SkyRus.Site.Services;

/// <summary>
/// Every few minutes: sends rating requests that could not reach the network, reads the supervisors'
/// decisions, and refreshes the rating of students whose request was approved.
/// </summary>
public sealed class RatingRequestSync(ProtocolService protocols, UserService users, INetworkClient network, ILogger<RatingRequestSync> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogWarning(e, "Rating request sync failed"); }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        foreach (var p in protocols.RatingRequestsToSync())
        {
            if (p.RrId == null) { await protocols.SendRatingRequestAsync(p.Id, ct); continue; }
            if (await protocols.RefreshRatingRequestAsync(p, ct) && await network.MemberAsync(p.Cid, ct) is { } m) users.Upsert(m);
        }
    }
}
