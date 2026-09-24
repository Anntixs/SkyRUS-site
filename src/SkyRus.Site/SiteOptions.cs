namespace SkyRus.Site;

/// <summary>Settings from the "Site" section of appsettings.json or environment variables (Site__Database, ...).</summary>
public sealed class SiteOptions
{
    /// <summary>SQLite file of the division site (its own, not the network's).</summary>
    public string Database { get; set; } = "skyrus.db";

    /// <summary>The network website, e.g. https://sky.network.npzy2.us/ (division API lives under /api/division/v1).</summary>
    public string NetworkUrl { get; set; } = "";

    /// <summary>SkyNetwork Connect (sign-in through the network): the client ID and secret issued by a network administrator.</summary>
    public string ConnectClientId { get; set; } = "";
    public string ConnectClientSecret { get; set; } = "";

    /// <summary>The division's API key issued by a network administrator (skd_…), for rating requests.</summary>
    public string NetworkApiKey { get; set; } = "";

    /// <summary>Public address of this site, for links in rating requests (exam reports).</summary>
    public string PublicUrl { get; set; } = "";

    /// <summary>CIDs that are always division administrators (the first staff are added by them).</summary>
    public string Admins { get; set; } = "";

    public bool BehindProxy { get; set; }

    public IReadOnlySet<long> AdminCids =>
        Admins.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s, out var c) ? c : 0).Where(c => c > 0).ToHashSet();
}
