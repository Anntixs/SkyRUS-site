using System.Collections.Concurrent;
using SkyRus.Site.Data;
using SkyRus.Site.Network;

namespace SkyRus.Site.Services;

/// <summary>
/// Keeps a member's rating and the status of their rating requests current while they use the site:
/// at most once a minute per member, and never longer than a few seconds per page.
/// </summary>
public sealed class MemberRefresh(UserService users, ProtocolService protocols, INetworkClient network)
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private readonly ConcurrentDictionary<long, DateTime> _checked = new();

    /// <summary>Returns true when something about the member changed.</summary>
    public async Task<bool> RefreshAsync(long cid, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (_checked.TryGetValue(cid, out var last) && now - last < Interval) return false;
        _checked[cid] = now;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        bool changed = false;
        try
        {
            foreach (var p in protocols.RatingRequestsToSync(cid).Where(p => p.RrId != null))
                changed |= await protocols.RefreshRatingRequestAsync(p, cts.Token);
            if (await network.MemberAsync(cid, cts.Token) is { } m)
            {
                var before = users.Find(cid);
                users.Upsert(m);
                changed |= before == null || before.Rating != m.Rating || before.PilotRating != m.PilotRating || before.Name != m.Name;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        return changed;
    }
}
