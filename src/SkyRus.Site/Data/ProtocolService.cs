using System.Globalization;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Extensions.Options;
using SkyRus.Site.Network;

namespace SkyRus.Site.Data;

/// <summary>
/// Assessment templates and protocols. A protocol copies its template's items; each item is graded
/// 1–5. A protocol passes when no item is below <see cref="PassGrade"/>. When a rating exam passes,
/// the rating request goes to the network, where a supervisor approves it.
/// </summary>
public sealed class ProtocolService(Database db, TrainingService training, INetworkClient network, IOptions<SiteOptions> options)
{
    public const int PassGrade = 3;

    // ---- templates ----

    public IReadOnlyList<Template> Templates(bool activeOnly = true)
    {
        using var c = db.Open();
        var list = c.Query<Template>("SELECT * FROM templates WHERE NOT @activeOnly OR active = 1 ORDER BY sort, title", new { activeOnly }).ToList();
        var items = c.Query<TemplateItem>("SELECT * FROM template_items ORDER BY sort").ToLookup(i => i.TemplateId);
        foreach (var t in list) t.Items = items[t.Id].ToList();
        return list;
    }

    public Template? Template(long id) => Templates(activeOnly: false).FirstOrDefault(t => t.Id == id);

    /// <summary>Items as text lines: "Title | description".</summary>
    public static IReadOnlyList<(string Title, string Description)> ParseItems(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
            .Select(l => l.Split('|', 2) is var p ? (p[0].Trim(), p.Length > 1 ? p[1].Trim() : "") : default)
            .Where(i => i.Item1.Length > 0).ToList();

    public (long Id, string? Error) SaveTemplate(long? id, string title, string kind, string track, string targetRating, int sort, bool active, string itemsText)
    {
        title = title.Trim();
        targetRating = targetRating.Trim().ToUpperInvariant();
        if (title.Length is < 3 or > 120) return (0, "Укажите название");
        if (!Kinds.Titles.ContainsKey(kind)) return (0, "Выберите вид аттестации");
        if (track is not ("atc" or "pilot" or "military")) track = "atc";
        if (kind == "exam" && targetRating.Length == 0) return (0, "Для экзамена укажите рейтинг, например S2");
        if (kind != "exam") targetRating = "";
        var items = ParseItems(itemsText);
        if (items.Count == 0) return (0, "Добавьте хотя бы один пункт");

        using var c = db.Open();
        using var tx = c.BeginTransaction();
        var p = new { title, kind, track, targetRating, sort, active, id };
        long tid = id ?? c.ExecuteScalar<long>("""
            INSERT INTO templates (title, kind, track, target_rating, sort, active) VALUES (@title, @kind, @track, @targetRating, @sort, @active) RETURNING id
            """, p, tx);
        if (id != null)
        {
            c.Execute("UPDATE templates SET title = @title, kind = @kind, track = @track, target_rating = @targetRating, sort = @sort, active = @active WHERE id = @id", p, tx);
            // Protocols keep their own copy of the items, so the template's can be replaced.
            c.Execute("DELETE FROM template_items WHERE template_id = @tid", new { tid }, tx);
        }
        int n = 0;
        foreach (var (t, d) in items)
            c.Execute("INSERT INTO template_items (template_id, sort, title, description) VALUES (@tid, @n, @t, @d)", new { tid, n = n++, t, d }, tx);
        tx.Commit();
        return (tid, null);
    }

    // ---- protocols ----

    private const string ProtocolSelect = """
        SELECT p.*, COALESCE(s.name, '') AS student_name, COALESCE(a.name, '') AS created_by_name, COALESCE(b.name, '') AS closed_by_name
        FROM protocols p LEFT JOIN users s ON s.cid = p.cid LEFT JOIN users a ON a.cid = p.created_by LEFT JOIN users b ON b.cid = p.closed_by
        """;

    public IReadOnlyList<Protocol> Protocols(long cid)
    {
        using var c = db.Open();
        return c.Query<Protocol>(ProtocolSelect + " WHERE p.cid = @cid ORDER BY p.id DESC", new { cid }).ToList();
    }

    public Protocol? Protocol(long id)
    {
        using var c = db.Open();
        var p = c.QuerySingleOrDefault<Protocol>(ProtocolSelect + " WHERE p.id = @id", new { id });
        if (p != null) p.Items = c.Query<ProtocolItem>("SELECT * FROM protocol_items WHERE protocol_id = @id ORDER BY sort", new { id }).ToList();
        return p;
    }

    public (long Id, string? Error) Create(long actor, long cid, long templateId)
    {
        var t = Template(templateId);
        if (t is not { Active: true }) return (0, "Выберите вид аттестации");
        training.EnsureStudent(actor, cid);
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        long id = c.ExecuteScalar<long>("""
            INSERT INTO protocols (cid, template_id, title, kind, track, target_rating, created_by, created_at, report_token)
            VALUES (@cid, @Id, @Title, @Kind, @Track, @TargetRating, @actor, @now, @token) RETURNING id
            """, new { cid, t.Id, t.Title, t.Kind, t.Track, t.TargetRating, actor, now = Database.Now(),
                       token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant() }, tx);
        foreach (var i in t.Items)
            c.Execute("INSERT INTO protocol_items (protocol_id, sort, title, description) VALUES (@id, @Sort, @Title, @Description)",
                new { id, i.Sort, i.Title, i.Description }, tx);
        tx.Commit();
        training.Log(cid, actor, $"Создан протокол #{id} «{t.Title}»");
        return (id, null);
    }

    /// <summary>Saves grades (item id → 1..5 or null) and the comment of an open protocol.</summary>
    public string? Save(long id, IReadOnlyDictionary<long, int?> grades, string comment)
    {
        var p = Protocol(id);
        if (p == null) return "Протокол не найден";
        if (!p.IsOpen) return "Протокол уже закрыт";
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        foreach (var item in p.Items)
            if (grades.TryGetValue(item.Id, out var g))
                c.Execute("UPDATE protocol_items SET grade = @g WHERE id = @Id", new { g = g is >= 1 and <= 5 ? g : null, item.Id }, tx);
        c.Execute("UPDATE protocols SET comment = @comment WHERE id = @id", new { id, comment = TrainingService.Clip(comment, 4000) }, tx);
        tx.Commit();
        training.Touch(p.Cid);
        return null;
    }

    /// <summary>
    /// Closes the protocol: every item must be graded. Passed when no grade is below 3. A passed rating
    /// exam sends the rating request to the network right away (retried later if the network is down).
    /// </summary>
    public async Task<string?> CloseAsync(long actor, long id, CancellationToken ct = default)
    {
        var p = Protocol(id);
        if (p == null) return "Протокол не найден";
        if (!p.IsOpen) return "Протокол уже закрыт";
        if (p.Items.Any(i => i.Grade == null)) return "Поставьте оценки по всем пунктам";
        bool passed = p.Items.All(i => i.Grade >= PassGrade);
        double score = Math.Round(p.Items.Average(i => i.Grade!.Value), 2);
        using (var c = db.Open())
            c.Execute("UPDATE protocols SET status = @status, score = @score, closed_by = @actor, closed_at = @now WHERE id = @id",
                new { id, status = passed ? "passed" : "failed", score, actor, now = Database.Now() });
        training.Log(p.Cid, actor, $"Протокол #{id} «{p.Title}» закрыт: {(passed ? "сдал" : "не сдал")}, {Score(score)}");
        if (passed && p.IsExam) await SendRatingRequestAsync(id, ct);
        return null;
    }

    public static string Score(double? score) => score?.ToString("0.00", CultureInfo.InvariantCulture) ?? "—";

    public string ReportUrl(Protocol p)
    {
        var baseUrl = options.Value.PublicUrl.TrimEnd('/');
        return baseUrl.Length == 0 ? "" : $"{baseUrl}/report/{p.Id}?t={p.ReportToken}";
    }

    /// <summary>Sends the rating request of a passed exam (once: the protocol id is the external id).</summary>
    public async Task SendRatingRequestAsync(long id, CancellationToken ct = default)
    {
        var p = Protocol(id);
        if (p is not { RatingRequestUnsent: true }) return;
        var examiner = p.ClosedByName.Length > 0 ? p.ClosedByName : p.CreatedByName;
        var result = await network.SendRatingRequestAsync(new RatingRequestPayload(
            p.Cid, p.Track, p.TargetRating, p.ClosedBy ?? p.CreatedBy, examiner,
            (p.Closed ?? DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Score(p.Score), ReportUrl(p), p.Comment, $"skyrus-protocol-{p.Id}"), ct);
        using var c = db.Open();
        c.Execute("""
            UPDATE protocols SET rr_id = @Id, rr_status = @Status, rr_comment = @ReviewComment, rr_error = @err, rr_updated_at = @now WHERE id = @pid
            """, new { result.Id, result.Status, result.ReviewComment, err = result.Error ?? "", now = Database.Now(), pid = id });
        if (result.Id != null) training.Log(p.Cid, 0, $"Отправлена заявка на рейтинг {p.TargetRating} в SkyNetwork (№ {result.Id})");
    }

    /// <summary>Protocols whose rating request is unsent or still pending at the network.</summary>
    public IReadOnlyList<Protocol> RatingRequestsToSync(long? cid = null)
    {
        using var c = db.Open();
        return c.Query<Protocol>(ProtocolSelect + """
             WHERE p.kind = 'exam' AND p.target_rating <> '' AND p.status = 'passed' AND (p.rr_id IS NULL OR p.rr_status = 'pending')
               AND (@cid IS NULL OR p.cid = @cid)
            """, new { cid }).ToList();
    }

    /// <summary>Reads the network's decision; returns true when it changed.</summary>
    public async Task<bool> RefreshRatingRequestAsync(Protocol p, CancellationToken ct = default)
    {
        if (p.RrId is not { } rr) return false;
        var state = await network.RatingRequestAsync(rr, ct);
        if (state.Error != null || state.Status == p.RrStatus) return false;
        using (var c = db.Open())
            c.Execute("UPDATE protocols SET rr_status = @Status, rr_comment = @ReviewComment, rr_error = '', rr_updated_at = @now WHERE id = @pid",
                new { state.Status, state.ReviewComment, now = Database.Now(), pid = p.Id });
        training.Log(p.Cid, 0, $"Заявка на рейтинг {p.TargetRating}: {RatingRequestStatuses.Title(state.Status)}" +
                               (state.ReviewComment.Length > 0 ? $" ({state.ReviewComment})" : ""));
        return true;
    }

    /// <summary>The member's exams with rating requests, for the personal cabinet.</summary>
    public IReadOnlyList<Protocol> RatingRequestsOf(long cid) =>
        Protocols(cid).Where(p => p.IsExam && p.Status == "passed").ToList();
}
