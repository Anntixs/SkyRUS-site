using Dapper;
using Microsoft.Extensions.Options;
using SkyRus.Site.Network;

namespace SkyRus.Site.Data;

/// <summary>Members known to the site (copied from the network) and the training center staff.</summary>
public sealed class UserService(Database db, IOptions<SiteOptions> options)
{
    public static readonly IReadOnlyDictionary<string, string> Roles = new Dictionary<string, string>
    {
        ["admin"] = "Администратор", ["instructor"] = "Инструктор", ["mentor"] = "Ментор",
    };

    public User? Find(long cid)
    {
        using var c = db.Open();
        return c.QuerySingleOrDefault<User>("SELECT * FROM users WHERE cid = @cid", new { cid });
    }

    /// <summary>Stores the member's current name and ratings from the network.</summary>
    public void Upsert(NetworkMember m, bool login = false)
    {
        using var c = db.Open();
        long now = Database.Now();
        c.Execute("""
            INSERT INTO users (cid, name, email, rating, pilot_rating, last_login_at, updated_at)
            VALUES (@Cid, @Name, @Email, @Rating, @PilotRating, @login, @now)
            ON CONFLICT(cid) DO UPDATE SET name = @Name, email = CASE WHEN @Email = '' THEN email ELSE @Email END,
                rating = @Rating, pilot_rating = @PilotRating, updated_at = @now,
                last_login_at = COALESCE(@login, last_login_at)
            """, new { m.Cid, m.Name, m.Email, m.Rating, m.PilotRating, now, login = login ? now : (long?)null });
    }

    /// <summary>admin, instructor, mentor or null. Administrators from the settings always count.</summary>
    public string? RoleOf(long cid)
    {
        if (options.Value.AdminCids.Contains(cid)) return "admin";
        using var c = db.Open();
        return c.QuerySingleOrDefault<string?>("SELECT role FROM staff WHERE cid = @cid", new { cid });
    }

    public IReadOnlyList<StaffMember> Staff()
    {
        using var c = db.Open();
        var list = c.Query<StaffMember>("""
            SELECT s.cid, s.role, s.added_at, COALESCE(u.name, '') AS name, COALESCE(u.rating, '') AS rating
            FROM staff s LEFT JOIN users u ON u.cid = s.cid ORDER BY s.role, u.name
            """).ToList();
        foreach (var cid in options.Value.AdminCids.Where(a => list.All(s => s.Cid != a)))
        {
            var u = Find(cid);
            list.Insert(0, new StaffMember { Cid = cid, Role = "admin", Name = u?.Name ?? "", Rating = u?.Rating ?? "" });
        }
        return list;
    }

    public void SetRole(long actor, long cid, string? role)
    {
        using var c = db.Open();
        if (role == null || !Roles.ContainsKey(role)) c.Execute("DELETE FROM staff WHERE cid = @cid", new { cid });
        else c.Execute("""
            INSERT INTO staff (cid, role, added_by, added_at) VALUES (@cid, @role, @actor, @now)
            ON CONFLICT(cid) DO UPDATE SET role = @role
            """, new { cid, role, actor, now = Database.Now() });
    }
}
