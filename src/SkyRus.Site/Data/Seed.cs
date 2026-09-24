using Dapper;
using Microsoft.Data.Sqlite;

namespace SkyRus.Site.Data;

/// <summary>Starting content of a fresh database: the FIRs of Russia, main airports, assessment
/// templates and the first documents. Each part is added only while its table is empty, so staff edits stay.</summary>
internal static class Seed
{
    private static readonly (string Code, string Name, (string Icao, string Name)[] Airports)[] Firs =
    [
        ("UUWV", "Москва", [("UUEE", "Шереметьево"), ("UUDD", "Домодедово"), ("UUWW", "Внуково"), ("UUBW", "Жуковский")]),
        ("ULLL", "Санкт-Петербург", [("ULLI", "Пулково"), ("ULMM", "Мурманск"), ("ULAA", "Архангельск")]),
        ("UMKK", "Калининград", [("UMKK", "Храброво")]),
        ("URRV", "Ростов-на-Дону", [("URRP", "Платов"), ("URSS", "Сочи"), ("URKK", "Краснодар"), ("URMM", "Минеральные Воды")]),
        ("UWWW", "Самара", [("UWWW", "Курумоч"), ("UWKD", "Казань"), ("UWUU", "Уфа"), ("UWGG", "Нижний Новгород")]),
        ("USSV", "Екатеринбург", [("USSS", "Кольцово"), ("USCC", "Челябинск"), ("USPP", "Пермь")]),
        ("USTV", "Тюмень", [("USTR", "Рощино"), ("USNN", "Нижневартовск"), ("USRR", "Сургут")]),
        ("UNNT", "Новосибирск", [("UNNT", "Толмачёво"), ("UNOO", "Омск"), ("UNBB", "Барнаул")]),
        ("UNKL", "Красноярск", [("UNKL", "Емельяново"), ("UOOO", "Норильск")]),
        ("UIII", "Иркутск", [("UIII", "Иркутск"), ("UIUU", "Улан-Удэ")]),
        ("UEEE", "Якутск", [("UEEE", "Якутск")]),
        ("UHHH", "Хабаровск", [("UHHH", "Новый"), ("UHWW", "Кневичи"), ("UHSS", "Хомутово")]),
        ("UHMM", "Магадан", [("UHMM", "Сокол")]),
        ("UHPP", "Петропавловск-Камчатский", [("UHPP", "Елизово")]),
    ];

    private static readonly (string Title, string Description)[] Theory =
    [
        ("Правила подключения к сети SkyNetwork", "позывной, имя, радиус видимости клиента, частоты и ATIS"),
        ("Назначение диспетчерского пункта", "зона ответственности, задачи пункта в системе ОВД"),
        ("Технология работы диспетчера", "рубежи приёма-передачи, взаимодействие со смежными пунктами"),
        ("Фразеология радиообмена на русском языке", ""),
        ("Фразеология радиообмена на английском языке", ""),
        ("Использование карт AIP", "схема аэродрома, стоянки, SID/STAR, схемы заходов"),
        ("Аэродром и РПИ приписки", "ВПП, РД, частоты, процедуры, особенности"),
        ("Метеорология", "METAR/TAF, курс магнитный и истинный, влияние ветра на полёт ВС"),
        ("Интервалы эшелонирования", "вертикальные, горизонтальные, по спутному следу, опасное сближение"),
        ("Системы заходов на посадку", "ILS, RNP, VOR/DME, визуальный заход, минимумы"),
    ];

    private static readonly (string Title, string Description)[] Practice =
    [
        ("Фразеология и чёткость ведения связи", "стандартные фразы, скорость речи, подтверждения"),
        ("Ситуационная осведомлённость", "контроль всех ВС в зоне, предвидение конфликтов"),
        ("Координация со смежными пунктами", "передача и приём ВС, согласования"),
        ("Работа с Network-ATC", "стрипы, теги, списки, назначение CFL, SID/STAR, ВПП"),
        ("Действия в нестандартных ситуациях", "уход на второй круг, отказы, аварийный код"),
    ];

    private static readonly (string Pos, string Title, string Description)[] PositionItems =
    [
        ("DEL/GND", "Выдача диспетчерского разрешения", "маршрут, SID, эшелон, код ответчика, проверка плана полёта"),
        ("DEL/GND", "Руление и управление на земле", "маршруты руления, приоритеты, места ожидания"),
        ("TWR", "Взлёт и посадка", "выдача разрешений, интервалы на ВПП, спутный след"),
        ("TWR", "Полёты в зоне аэродрома", "круг, ПВП-полёты, информирование"),
        ("APP", "Векторение и последовательность заходов", "интервалы, скорости, выход на конечный этап"),
        ("APP", "Вылет и набор высоты", "SID, ограничения, передача на районный центр"),
        ("CTR", "Управление в районе", "эшелоны, прямые, ограничения по времени и скорости"),
        ("CTR", "Снижение и передача на подход", "STAR, расчёт снижения, координация"),
    ];

    private static readonly (string Pos, string Rating)[] Stages = [("DEL/GND", "S1"), ("TWR", "S2"), ("APP", "S3"), ("CTR", "C1")];

    public static void Apply(SqliteConnection c)
    {
        using var tx = c.BeginTransaction();
        long now = Database.Now();

        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM firs", transaction: tx) == 0)
        {
            int sort = 0;
            foreach (var (code, name, airports) in Firs)
            {
                long id = c.ExecuteScalar<long>("INSERT INTO firs (code, name, sort) VALUES (@code, @name, @sort) RETURNING id",
                    new { code, name, sort = sort++ }, tx);
                foreach (var (icao, an) in airports)
                    c.Execute("INSERT INTO airports (icao, fir_id, name) VALUES (@icao, @id, @an)", new { icao, id, an }, tx);
            }
        }

        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM templates", transaction: tx) == 0)
        {
            int sort = 0;
            foreach (var (pos, rating) in Stages)
            {
                var own = PositionItems.Where(p => p.Pos == pos).Select(p => (p.Title, p.Description)).ToArray();
                AddTemplate(c, tx, $"{pos}: зачёт по теории", "theory", "", sort++, Theory);
                AddTemplate(c, tx, $"{pos}: зачёт по практике", "practice", "", sort++, [.. own, .. Practice]);
                AddTemplate(c, tx, $"{pos}: экзамен на {rating}", "exam", rating, sort++, [.. own, .. Practice]);
            }
        }

        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM documents", transaction: tx) == 0)
        {
            c.Execute("""
                INSERT INTO documents (title, category, body, sort, updated_at) VALUES
                ('Порядок обучения в SkyRUS', 'Учебный центр', @training, 0, @now),
                ('Допуски на позиции', 'Учебный центр', @permits, 1, @now)
                """, new
            {
                now,
                training = """
                    1. Войдите на сайт с CID и паролем SkyNetwork.
                    2. В личном кабинете выберите РПИ и аэродром приписки и подайте заявку на обучение.
                    3. Инструктор или ментор РПИ берёт заявку в работу и связывается с вами.
                    4. Обучение идёт по этапам: теория, практика, экзамен. Каждая аттестация оформляется протоколом с оценками по пунктам (от 1 до 5).
                    5. Аттестация сдана, если ни один пункт не оценён ниже 3.
                    6. После сданного экзамена на рейтинг дивизион отправляет заявку в SkyNetwork, и рейтинг одобряет супервайзер сети.
                    """,
                permits = """
                    Допуск разрешает работать на конкретной позиции (например, UUEE_TWR) до получения следующего рейтинга или по решению инструктора.
                    Допуски выдают инструкторы учебного центра. Действующие допуски опубликованы в разделе «Допуски».
                    """,
            }, tx);
        }
        tx.Commit();
    }

    private static void AddTemplate(SqliteConnection c, SqliteTransaction tx, string title, string kind, string rating, int sort,
        IEnumerable<(string Title, string Description)> items)
    {
        long id = c.ExecuteScalar<long>("""
            INSERT INTO templates (title, kind, target_rating, sort) VALUES (@title, @kind, @rating, @sort) RETURNING id
            """, new { title, kind, rating, sort }, tx);
        int n = 0;
        foreach (var (t, d) in items)
            c.Execute("INSERT INTO template_items (template_id, sort, title, description) VALUES (@id, @n, @t, @d)", new { id, n = n++, t, d }, tx);
    }
}
