using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace SkyRus.Site.Data;

/// <summary>SQLite storage of the division site. Members sign in with the network CID; their name and
/// ratings are copied from the network on every sign-in.</summary>
public sealed class Database
{
    private readonly string _connectionString;

    static Database() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    public Database(IOptions<SiteOptions> options)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.Value.Database,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        c.Execute("PRAGMA busy_timeout = 3000;");
        return c;
    }

    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void Migrate()
    {
        using var c = Open();
        c.Execute("PRAGMA journal_mode = WAL;");
        c.Execute("""
            CREATE TABLE IF NOT EXISTS users (
                cid INTEGER PRIMARY KEY, name TEXT NOT NULL, email TEXT NOT NULL DEFAULT '',
                rating TEXT NOT NULL DEFAULT 'OBS', pilot_rating TEXT NOT NULL DEFAULT '',
                last_login_at INTEGER, updated_at INTEGER NOT NULL);

            -- Training center staff: admin, instructor or mentor.
            CREATE TABLE IF NOT EXISTS staff (
                cid INTEGER PRIMARY KEY, role TEXT NOT NULL, added_by INTEGER NOT NULL, added_at INTEGER NOT NULL);

            CREATE TABLE IF NOT EXISTS firs (
                id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT NOT NULL UNIQUE COLLATE NOCASE, name TEXT NOT NULL,
                description TEXT NOT NULL DEFAULT '', sort INTEGER NOT NULL DEFAULT 0);

            CREATE TABLE IF NOT EXISTS airports (
                icao TEXT PRIMARY KEY COLLATE NOCASE, fir_id INTEGER NOT NULL REFERENCES firs(id), name TEXT NOT NULL);

            CREATE TABLE IF NOT EXISTS students (
                cid INTEGER PRIMARY KEY, fir_id INTEGER REFERENCES firs(id), airport TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL);

            CREATE TABLE IF NOT EXISTS training_requests (
                id INTEGER PRIMARY KEY AUTOINCREMENT, cid INTEGER NOT NULL, fir_id INTEGER NOT NULL REFERENCES firs(id),
                airport TEXT NOT NULL DEFAULT '', message TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'open',
                handled_by INTEGER, handled_at INTEGER, reason TEXT NOT NULL DEFAULT '', created_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_requests_status ON training_requests (status, fir_id, created_at);

            -- Position endorsements ("solo"/permits), e.g. UUEE_TWR.
            CREATE TABLE IF NOT EXISTS permits (
                id INTEGER PRIMARY KEY AUTOINCREMENT, cid INTEGER NOT NULL, position TEXT NOT NULL,
                note TEXT NOT NULL DEFAULT '', issued_by INTEGER NOT NULL, issued_at INTEGER NOT NULL,
                revoked_by INTEGER, revoked_at INTEGER);
            CREATE INDEX IF NOT EXISTS ix_permits_cid ON permits (cid);

            -- Kinds of assessment: theory test, practical test, rating exam.
            CREATE TABLE IF NOT EXISTS templates (
                id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, kind TEXT NOT NULL,
                track TEXT NOT NULL DEFAULT 'atc', target_rating TEXT NOT NULL DEFAULT '',
                sort INTEGER NOT NULL DEFAULT 0, active INTEGER NOT NULL DEFAULT 1);

            CREATE TABLE IF NOT EXISTS template_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT, template_id INTEGER NOT NULL REFERENCES templates(id),
                sort INTEGER NOT NULL, title TEXT NOT NULL, description TEXT NOT NULL DEFAULT '');

            -- An assessment of a student: the template's items are copied in and graded 1–5.
            CREATE TABLE IF NOT EXISTS protocols (
                id INTEGER PRIMARY KEY AUTOINCREMENT, cid INTEGER NOT NULL, template_id INTEGER NOT NULL,
                title TEXT NOT NULL, kind TEXT NOT NULL, track TEXT NOT NULL DEFAULT 'atc', target_rating TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'open', score REAL, comment TEXT NOT NULL DEFAULT '',
                created_by INTEGER NOT NULL, created_at INTEGER NOT NULL, closed_by INTEGER, closed_at INTEGER,
                report_token TEXT NOT NULL,
                rr_id INTEGER, rr_status TEXT NOT NULL DEFAULT '', rr_comment TEXT NOT NULL DEFAULT '',
                rr_error TEXT NOT NULL DEFAULT '', rr_updated_at INTEGER);
            CREATE INDEX IF NOT EXISTS ix_protocols_cid ON protocols (cid, id);

            CREATE TABLE IF NOT EXISTS protocol_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT, protocol_id INTEGER NOT NULL REFERENCES protocols(id),
                sort INTEGER NOT NULL, title TEXT NOT NULL, description TEXT NOT NULL DEFAULT '', grade INTEGER);

            -- The student's log: instructors' comments and system events.
            CREATE TABLE IF NOT EXISTS comments (
                id INTEGER PRIMARY KEY AUTOINCREMENT, cid INTEGER NOT NULL, author_cid INTEGER NOT NULL,
                body TEXT NOT NULL, created_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_comments_cid ON comments (cid, id);

            CREATE TABLE IF NOT EXISTS news (
                id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, body TEXT NOT NULL,
                author_cid INTEGER NOT NULL, published INTEGER NOT NULL DEFAULT 1, created_at INTEGER NOT NULL);

            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT, title TEXT NOT NULL, category TEXT NOT NULL DEFAULT '',
                body TEXT NOT NULL DEFAULT '', url TEXT NOT NULL DEFAULT '', sort INTEGER NOT NULL DEFAULT 0,
                updated_at INTEGER NOT NULL);
            """);
        Seed.Apply(c);
    }
}
