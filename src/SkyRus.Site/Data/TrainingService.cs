using System.Text.RegularExpressions;
using Dapper;

namespace SkyRus.Site.Data;

/// <summary>Training requests, students, permits and the students' log.</summary>
public sealed partial class TrainingService(Database db)
{
    // ---- requests ----

    private const string RequestSelect = """
        SELECT r.*, f.code AS fir_code, f.name AS fir_name, COALESCE(u.name, '') AS name, COALESCE(u.email, '') AS email,
               COALESCE(u.rating, '') AS rating, COALESCE(h.name, '') AS handled_by_name
        FROM training_requests r JOIN firs f ON f.id = r.fir_id
        LEFT JOIN users u ON u.cid = r.cid LEFT JOIN users h ON h.cid = r.handled_by
        """;

    /// <summary>A member asks to be trained in a FIR; returns an error or null.</summary>
    public string? Request(long cid, long firId, string airport, string message)
    {
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM firs WHERE id = @firId", new { firId }) == 0) return "Выберите РПИ";
        airport = airport.Trim().ToUpperInvariant();
        if (airport.Length > 0 && c.ExecuteScalar<long>("SELECT COUNT(*) FROM airports WHERE icao = @airport AND fir_id = @firId", new { airport, firId }) == 0)
            return "Этот аэродром не относится к выбранному РПИ";
        if (CanRequest(c, cid) is { } error) return error;
        try
        {
            c.Execute("""
                INSERT INTO training_requests (cid, fir_id, airport, message, created_at) VALUES (@cid, @firId, @airport, @message, @now)
                """, new { cid, firId, airport, message = Clip(message, 2000), now = Database.Now() });
        }
        catch (Microsoft.Data.Sqlite.SqliteException e) when (e.SqliteErrorCode == 19) // the same form sent twice at once
        {
            return "Заявка уже подана";
        }
        return null;
    }

    /// <summary>One request per member: none while one is open, and none once they are a student.</summary>
    public string? CanRequest(long cid)
    {
        using var c = db.Open();
        return CanRequest(c, cid);
    }

    private static string? CanRequest(Microsoft.Data.Sqlite.SqliteConnection c, long cid)
    {
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM training_requests WHERE cid = @cid AND status = 'open'", new { cid }) > 0)
            return "Заявка уже подана";
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM students WHERE cid = @cid", new { cid }) > 0
            || c.ExecuteScalar<long>("SELECT COUNT(*) FROM training_requests WHERE cid = @cid AND status = 'accepted'", new { cid }) > 0)
            return "Вы уже проходите обучение";
        return null;
    }

    public IReadOnlyList<TrainingRequest> Requests(bool archive = false, long? firId = null, long? cid = null)
    {
        using var c = db.Open();
        return c.Query<TrainingRequest>(RequestSelect + """
             WHERE (@cid IS NOT NULL OR (r.status = 'open') <> @archive)
               AND (@firId IS NULL OR r.fir_id = @firId) AND (@cid IS NULL OR r.cid = @cid)
            ORDER BY r.created_at DESC LIMIT 500
            """, new { archive, firId, cid }).ToList();
    }

    public TrainingRequest? RequestById(long id)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<TrainingRequest>(RequestSelect + " WHERE r.id = @id", new { id });
    }

    /// <summary>The request is taken: the member becomes a student of that FIR.</summary>
    public bool Accept(long actor, long id)
    {
        var r = RequestById(id);
        if (r is not { Status: "open" }) return false;
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        long now = Database.Now();
        c.Execute("UPDATE training_requests SET status = 'accepted', handled_by = @actor, handled_at = @now WHERE id = @id",
            new { id, actor, now }, tx);
        c.Execute("""
            INSERT INTO students (cid, fir_id, airport, created_at, updated_at) VALUES (@Cid, @FirId, @Airport, @now, @now)
            ON CONFLICT(cid) DO UPDATE SET fir_id = @FirId, airport = @Airport, updated_at = @now
            """, new { r.Cid, r.FirId, r.Airport, now }, tx);
        AddLog(c, tx, r.Cid, actor, $"Заявка #{id} принята: РПИ {r.FirName}{(r.Airport.Length > 0 ? ", " + r.Airport : "")}");
        tx.Commit();
        return true;
    }

    public string? Decline(long actor, long id, string reason)
    {
        reason = reason.Trim();
        if (reason.Length == 0) return "Укажите причину отказа";
        using var c = db.Open();
        return c.Execute("""
            UPDATE training_requests SET status = 'declined', handled_by = @actor, handled_at = @now, reason = @reason
            WHERE id = @id AND status = 'open'
            """, new { id, actor, reason = Clip(reason, 1000), now = Database.Now() }) > 0 ? null : "Заявка уже рассмотрена";
    }

    public int OpenRequests()
    {
        using var c = db.Open();
        return c.ExecuteScalar<int>("SELECT COUNT(*) FROM training_requests WHERE status = 'open'");
    }

    // ---- students ----

    private const string StudentSelect = """
        SELECT s.cid, s.fir_id, s.airport, s.updated_at, COALESCE(u.name, '') AS name, COALESCE(u.rating, '') AS rating,
               COALESCE(f.code, '') AS fir_code, COALESCE(f.name, '') AS fir_name,
               (SELECT COUNT(*) FROM permits p WHERE p.cid = s.cid AND p.revoked_at IS NULL) AS permits,
               COALESCE((SELECT title FROM protocols p WHERE p.cid = s.cid ORDER BY p.id DESC LIMIT 1), '') AS last_protocol,
               COALESCE((SELECT status FROM protocols p WHERE p.cid = s.cid ORDER BY p.id DESC LIMIT 1), '') AS last_protocol_status
        FROM students s LEFT JOIN users u ON u.cid = s.cid LEFT JOIN firs f ON f.id = s.fir_id
        """;

    public IReadOnlyList<Student> Students(long? firId = null, string? query = null)
    {
        query = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        using var c = db.Open();
        return c.Query<Student>(StudentSelect + """
             WHERE (@firId IS NULL OR s.fir_id = @firId)
               AND (@query IS NULL OR CAST(s.cid AS TEXT) = @query OR u.name LIKE '%' || @query || '%')
            ORDER BY s.updated_at DESC LIMIT 1000
            """, new { firId, query }).ToList();
    }

    public Student? Student(long cid)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<Student>(StudentSelect + " WHERE s.cid = @cid", new { cid });
    }

    /// <summary>Makes sure the member has a student card. An open request of theirs is accepted by the same staff member.</summary>
    public void EnsureStudent(long actor, long cid)
    {
        using var c = db.Open();
        var open = c.QuerySingleOrDefault<long?>("SELECT id FROM training_requests WHERE cid = @cid AND status = 'open'", new { cid });
        if (open is { } id && Accept(actor, id)) return;
        long now = Database.Now();
        c.Execute("INSERT INTO students (cid, created_at, updated_at) VALUES (@cid, @now, @now) ON CONFLICT(cid) DO NOTHING", new { cid, now });
    }

    public string? SetHome(long actor, long cid, long firId, string airport)
    {
        airport = airport.Trim().ToUpperInvariant();
        using var c = db.Open();
        var fir = c.QuerySingleOrDefault<string>("SELECT name FROM firs WHERE id = @firId", new { firId });
        if (fir == null) return "Выберите РПИ";
        if (airport.Length > 0 && c.ExecuteScalar<long>("SELECT COUNT(*) FROM airports WHERE icao = @airport AND fir_id = @firId", new { airport, firId }) == 0)
            return "Этот аэродром не относится к выбранному РПИ";
        c.Execute("UPDATE students SET fir_id = @firId, airport = @airport, updated_at = @now WHERE cid = @cid",
            new { cid, firId, airport, now = Database.Now() });
        Log(cid, actor, $"РПИ приписки: {fir}{(airport.Length > 0 ? ", " + airport : "")}");
        return null;
    }

    public void Touch(long cid)
    {
        using var c = db.Open();
        c.Execute("UPDATE students SET updated_at = @now WHERE cid = @cid", new { cid, now = Database.Now() });
    }

    // ---- permits ----

    [GeneratedRegex("^[A-Z]{4}(_[A-Z0-9]{1,3})?_(DEL|GND|TWR|APP|DEP|CTR|FSS)$")]
    private static partial Regex PositionPattern();

    public static string? ValidatePosition(string position) =>
        PositionPattern().IsMatch(position) ? null : "Позиция в формате UUEE_TWR, UUWV_CTR или UUEE_N_APP";

    private const string PermitSelect = """
        SELECT p.*, COALESCE(u.name, '') AS name, COALESCE(i.name, '') AS issued_by_name
        FROM permits p LEFT JOIN users u ON u.cid = p.cid LEFT JOIN users i ON i.cid = p.issued_by
        """;

    public IReadOnlyList<Permit> Permits(long? cid = null, bool activeOnly = true)
    {
        using var c = db.Open();
        return c.Query<Permit>(PermitSelect + """
             WHERE (@cid IS NULL OR p.cid = @cid) AND (NOT @activeOnly OR p.revoked_at IS NULL)
            ORDER BY p.revoked_at IS NOT NULL, p.position, p.issued_at DESC
            """, new { cid, activeOnly }).ToList();
    }

    public string? IssuePermit(long actor, long cid, string position, string note)
    {
        position = position.Trim().ToUpperInvariant();
        if (ValidatePosition(position) is { } error) return error;
        using var c = db.Open();
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM permits WHERE cid = @cid AND position = @position AND revoked_at IS NULL", new { cid, position }) > 0)
            return "Такой допуск уже выдан";
        c.Execute("INSERT INTO permits (cid, position, note, issued_by, issued_at) VALUES (@cid, @position, @note, @actor, @now)",
            new { cid, position, note = Clip(note, 300), actor, now = Database.Now() });
        Log(cid, actor, $"Выдан допуск {position}");
        return null;
    }

    public void RevokePermit(long actor, long id)
    {
        using var c = db.Open();
        var p = c.QuerySingleOrDefault<Permit>("SELECT * FROM permits WHERE id = @id AND revoked_at IS NULL", new { id });
        if (p == null) return;
        c.Execute("UPDATE permits SET revoked_by = @actor, revoked_at = @now WHERE id = @id", new { id, actor, now = Database.Now() });
        Log(p.Cid, actor, $"Отозван допуск {p.Position}");
    }

    // ---- log ----

    public IReadOnlyList<Comment> Log(long cid)
    {
        using var c = db.Open();
        return c.Query<Comment>("""
            SELECT m.*, COALESCE(u.name, '') AS author_name FROM comments m LEFT JOIN users u ON u.cid = m.author_cid
            WHERE m.cid = @cid ORDER BY m.id DESC
            """, new { cid }).ToList();
    }

    public void Log(long cid, long author, string body)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        AddLog(c, tx, cid, author, body);
        tx.Commit();
    }

    private static void AddLog(Microsoft.Data.Sqlite.SqliteConnection c, Microsoft.Data.Sqlite.SqliteTransaction tx, long cid, long author, string body)
    {
        long now = Database.Now();
        c.Execute("INSERT INTO comments (cid, author_cid, body, created_at) VALUES (@cid, @author, @body, @now)",
            new { cid, author, body = Clip(body, 4000), now }, tx);
        c.Execute("UPDATE students SET updated_at = @now WHERE cid = @cid", new { cid, now }, tx);
    }

    internal static string Clip(string? s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length > max ? s[..max] : s;
    }
}
