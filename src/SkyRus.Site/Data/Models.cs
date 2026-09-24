namespace SkyRus.Site.Data;

internal static class Time
{
    public static DateTime Utc(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
}

public sealed class User
{
    public long Cid { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rating { get; set; } = "OBS";
    public string PilotRating { get; set; } = "";
}

public sealed class Fir
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Sort { get; set; }
    /// <summary>Filled by list queries.</summary>
    public int Students { get; set; }
    public int OpenRequests { get; set; }
    public IReadOnlyList<Airport> Airports { get; set; } = [];
}

public sealed class Airport
{
    public string Icao { get; set; } = "";
    public long FirId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TrainingRequest
{
    public long Id { get; set; }
    public long Cid { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Rating { get; set; } = "";
    public long FirId { get; set; }
    public string FirCode { get; set; } = "";
    public string FirName { get; set; } = "";
    public string Airport { get; set; } = "";
    public string Message { get; set; } = "";
    /// <summary>open, accepted or declined.</summary>
    public string Status { get; set; } = "open";
    public long? HandledBy { get; set; }
    public string HandledByName { get; set; } = "";
    public long? HandledAt { get; set; }
    public string Reason { get; set; } = "";
    public long CreatedAt { get; set; }
    public DateTime Created => Time.Utc(CreatedAt);
    public DateTime? Handled => HandledAt is { } h ? Time.Utc(h) : null;
}

public sealed class Student
{
    public long Cid { get; set; }
    public string Name { get; set; } = "";
    public string Rating { get; set; } = "";
    public long? FirId { get; set; }
    public string FirCode { get; set; } = "";
    public string FirName { get; set; } = "";
    public string Airport { get; set; } = "";
    public long UpdatedAt { get; set; }
    public int Permits { get; set; }
    /// <summary>The latest protocol: its title and status (open, passed, failed).</summary>
    public string LastProtocol { get; set; } = "";
    public string LastProtocolStatus { get; set; } = "";
    public DateTime Updated => Time.Utc(UpdatedAt);
}

public sealed class Permit
{
    public long Id { get; set; }
    public long Cid { get; set; }
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public string Note { get; set; } = "";
    public long IssuedBy { get; set; }
    public string IssuedByName { get; set; } = "";
    public long IssuedAt { get; set; }
    public long? RevokedAt { get; set; }
    public DateTime Issued => Time.Utc(IssuedAt);
    public bool Active => RevokedAt == null;
}

public sealed class Template
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    /// <summary>theory, practice or exam.</summary>
    public string Kind { get; set; } = "theory";
    public string Track { get; set; } = "atc";
    /// <summary>For exams: the rating asked for after a pass (S1, S2, …).</summary>
    public string TargetRating { get; set; } = "";
    public int Sort { get; set; }
    public bool Active { get; set; }
    public IReadOnlyList<TemplateItem> Items { get; set; } = [];
}

public sealed class TemplateItem
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public int Sort { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class Protocol
{
    public long Id { get; set; }
    public long Cid { get; set; }
    public string StudentName { get; set; } = "";
    public long TemplateId { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "theory";
    public string Track { get; set; } = "atc";
    public string TargetRating { get; set; } = "";
    /// <summary>open, passed or failed.</summary>
    public string Status { get; set; } = "open";
    public double? Score { get; set; }
    public string Comment { get; set; } = "";
    public long CreatedBy { get; set; }
    public string CreatedByName { get; set; } = "";
    public long CreatedAt { get; set; }
    public long? ClosedBy { get; set; }
    public string ClosedByName { get; set; } = "";
    public long? ClosedAt { get; set; }
    public string ReportToken { get; set; } = "";
    /// <summary>The network's rating request sent after a passed exam.</summary>
    public long? RrId { get; set; }
    public string RrStatus { get; set; } = "";
    public string RrComment { get; set; } = "";
    public string RrError { get; set; } = "";
    public IReadOnlyList<ProtocolItem> Items { get; set; } = [];

    public DateTime Created => Time.Utc(CreatedAt);
    public DateTime? Closed => ClosedAt is { } t ? Time.Utc(t) : null;
    public bool IsOpen => Status == "open";
    public bool IsExam => Kind == "exam" && TargetRating.Length > 0;
    /// <summary>A passed exam whose rating request still has to reach the network.</summary>
    public bool RatingRequestUnsent => IsExam && Status == "passed" && RrId == null;
}

public sealed class ProtocolItem
{
    public long Id { get; set; }
    public long ProtocolId { get; set; }
    public int Sort { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int? Grade { get; set; }
}

public sealed class Comment
{
    public long Id { get; set; }
    public long Cid { get; set; }
    public long AuthorCid { get; set; }
    public string AuthorName { get; set; } = "";
    public string Body { get; set; } = "";
    public long CreatedAt { get; set; }
    public DateTime Created => Time.Utc(CreatedAt);
}

public sealed class NewsPost
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public long AuthorCid { get; set; }
    public string AuthorName { get; set; } = "";
    public bool Published { get; set; }
    public long CreatedAt { get; set; }
    public DateTime Created => Time.Utc(CreatedAt);
}

public sealed class Document
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Body { get; set; } = "";
    public string Url { get; set; } = "";
    public int Sort { get; set; }
    public long UpdatedAt { get; set; }
    public DateTime Updated => Time.Utc(UpdatedAt);
}

public sealed class StaffMember
{
    public long Cid { get; set; }
    public string Name { get; set; } = "";
    public string Rating { get; set; } = "";
    public string Role { get; set; } = "";
    public long AddedAt { get; set; }
    public DateTime Added => Time.Utc(AddedAt);
}

public static class Kinds
{
    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        ["theory"] = "Зачёт по теории", ["practice"] = "Зачёт по практике", ["exam"] = "Экзамен на рейтинг",
    };

    public static string Title(string kind) => Titles.GetValueOrDefault(kind, kind);
}

public static class RequestStatuses
{
    public static string Title(string status) => status switch
    {
        "open" => "ожидает", "accepted" => "принята", "declined" => "отклонена", _ => status,
    };
}

/// <summary>Status of the network's rating request, as shown on the site.</summary>
public static class RatingRequestStatuses
{
    public static string Title(string status) => status switch
    {
        "pending" => "на рассмотрении у супервайзера",
        "approved" => "рейтинг выдан",
        "declined" => "отклонена супервайзером",
        "withdrawn" => "отозвана",
        _ => "не отправлена",
    };
}
