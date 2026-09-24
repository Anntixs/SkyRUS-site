using Dapper;

namespace SkyRus.Site.Data;

/// <summary>Regions (FIRs and airports), news and documents.</summary>
public sealed class SiteContent(Database db)
{
    // ---- regions ----

    public IReadOnlyList<Fir> Firs()
    {
        using var c = db.Open();
        var firs = c.Query<Fir>("""
            SELECT f.*,
                   (SELECT COUNT(*) FROM students s WHERE s.fir_id = f.id) AS students,
                   (SELECT COUNT(*) FROM training_requests r WHERE r.fir_id = f.id AND r.status = 'open') AS open_requests
            FROM firs f ORDER BY f.sort, f.name
            """).ToList();
        var airports = c.Query<Airport>("SELECT * FROM airports ORDER BY icao").ToLookup(a => a.FirId);
        foreach (var f in firs) f.Airports = airports[f.Id].ToList();
        return firs;
    }

    public Fir? Fir(long id) => Firs().FirstOrDefault(f => f.Id == id);
    public Fir? Fir(string code) => Firs().FirstOrDefault(f => f.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>The FIR a position belongs to: UUEE_TWR by its airport, UUWV_CTR by the FIR code.</summary>
    public static Fir? FirOfPosition(IEnumerable<Fir> firs, string position)
    {
        var prefix = position.Split('_')[0];
        var list = firs.ToList();
        return list.FirstOrDefault(f => f.Code.Equals(prefix, StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(f => f.Airports.Any(a => a.Icao.Equals(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    public string? SaveFir(long? id, string code, string name, string description)
    {
        code = code.Trim().ToUpperInvariant();
        name = name.Trim();
        if (code.Length != 4 || !code.All(char.IsLetter)) return "Код РПИ — четыре латинские буквы, например UUWV";
        if (name.Length is < 2 or > 60) return "Укажите название РПИ";
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM firs WHERE code = @code AND id <> @id", new { code, id = id ?? 0 }) > 0)
            return "РПИ с таким кодом уже есть";
        if (id is { } existing)
            c.Execute("UPDATE firs SET code = @code, name = @name, description = @description WHERE id = @existing",
                new { code, name, description = description.Trim(), existing });
        else
            c.Execute("INSERT INTO firs (code, name, description, sort) VALUES (@code, @name, @description, (SELECT COALESCE(MAX(sort), 0) + 1 FROM firs))",
                new { code, name, description = description.Trim() });
        return null;
    }

    public string? AddAirport(long firId, string icao, string name)
    {
        icao = icao.Trim().ToUpperInvariant();
        if (icao.Length != 4 || !icao.All(char.IsLetter)) return "Код аэродрома — четыре латинские буквы";
        using var c = db.Open();
        c.Execute("""
            INSERT INTO airports (icao, fir_id, name) VALUES (@icao, @firId, @name)
            ON CONFLICT(icao) DO UPDATE SET fir_id = @firId, name = @name
            """, new { icao, firId, name = name.Trim() });
        return null;
    }

    public void RemoveAirport(string icao)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM airports WHERE icao = @icao", new { icao });
    }

    // ---- news ----

    public IReadOnlyList<NewsPost> News(int limit = 50, bool all = false)
    {
        using var c = db.Open();
        return c.Query<NewsPost>("""
            SELECT n.*, COALESCE(u.name, 'SkyRUS') AS author_name FROM news n LEFT JOIN users u ON u.cid = n.author_cid
            WHERE @all OR n.published = 1 ORDER BY n.created_at DESC LIMIT @limit
            """, new { limit, all }).ToList();
    }

    public NewsPost? Post(long id)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<NewsPost>("""
            SELECT n.*, COALESCE(u.name, 'SkyRUS') AS author_name FROM news n LEFT JOIN users u ON u.cid = n.author_cid WHERE n.id = @id
            """, new { id });
    }

    public long SavePost(long? id, long author, string title, string body, bool published)
    {
        using var c = db.Open();
        if (id is { } existing)
        {
            c.Execute("UPDATE news SET title = @title, body = @body, published = @published WHERE id = @existing",
                new { title, body, published, existing });
            return existing;
        }
        return c.ExecuteScalar<long>("""
            INSERT INTO news (title, body, author_cid, published, created_at) VALUES (@title, @body, @author, @published, @now) RETURNING id
            """, new { title, body, author, published, now = Database.Now() });
    }

    public void DeletePost(long id)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM news WHERE id = @id", new { id });
    }

    // ---- documents ----

    public IReadOnlyList<Document> Documents()
    {
        using var c = db.Open();
        return c.Query<Document>("SELECT * FROM documents ORDER BY category, sort, title").ToList();
    }

    public Document? Document(long id)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<Document>("SELECT * FROM documents WHERE id = @id", new { id });
    }

    public long SaveDocument(long? id, string title, string category, string body, string url, int sort)
    {
        using var c = db.Open();
        var p = new { title, category, body, url, sort, now = Database.Now(), id };
        if (id != null)
        {
            c.Execute("UPDATE documents SET title = @title, category = @category, body = @body, url = @url, sort = @sort, updated_at = @now WHERE id = @id", p);
            return id.Value;
        }
        return c.ExecuteScalar<long>("""
            INSERT INTO documents (title, category, body, url, sort, updated_at) VALUES (@title, @category, @body, @url, @sort, @now) RETURNING id
            """, p);
    }

    public void DeleteDocument(long id)
    {
        using var c = db.Open();
        c.Execute("DELETE FROM documents WHERE id = @id", new { id });
    }
}
