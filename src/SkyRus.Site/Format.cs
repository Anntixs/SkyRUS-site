using System.Globalization;

namespace SkyRus.Site;

/// <summary>Dates on the site: Moscow time is not assumed, everything is shown in UTC like on the network.</summary>
public static class Format
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Utc(DateTime t) => t.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + "z";
    public static string Date(DateTime t) => t.ToString("d MMMM yyyy", Ru);
    public static string Short(DateTime t) => t.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    /// <summary>"1 студент", "3 студента", "5 студентов".</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        int m10 = n % 10, m100 = n % 100;
        var word = m10 == 1 && m100 != 11 ? one : m10 is >= 2 and <= 4 && m100 is < 12 or > 14 ? few : many;
        return $"{n} {word}";
    }
}
