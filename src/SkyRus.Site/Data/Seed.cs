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

    // Added in version 2: the other Russian-speaking countries (without the Baltic states).
    private static readonly (string Code, string Name, (string Icao, string Name)[] Airports)[] Neighbours =
    [
        ("UMMV", "Минск (Беларусь)", [("UMMS", "Национальный аэропорт Минск"), ("UMGG", "Гомель"), ("UMBB", "Брест"), ("UMII", "Витебск"), ("UMMG", "Гродно"), ("UMOO", "Могилёв")]),
        ("UKBV", "Киев (Украина)", [("UKBB", "Борисполь"), ("UKKK", "Жуляны")]),
        ("UKDV", "Днепр (Украина)", [("UKDD", "Днепр"), ("UKHH", "Харьков"), ("UKDE", "Запорожье")]),
        ("UKLV", "Львов (Украина)", [("UKLL", "Львов")]),
        ("UKOV", "Одесса (Украина)", [("UKOO", "Одесса")]),
        ("LUUU", "Кишинёв (Молдова)", [("LUKK", "Кишинёв")]),
        ("UGGG", "Тбилиси (Грузия)", [("UGTB", "Тбилиси"), ("UGKO", "Кутаиси"), ("UGSB", "Батуми")]),
        ("UDDD", "Ереван (Армения)", [("UDYZ", "Звартноц"), ("UDSG", "Гюмри")]),
        ("UBBA", "Баку (Азербайджан)", [("UBBB", "Гейдар Алиев"), ("UBBG", "Гянджа"), ("UBBN", "Нахичевань")]),
        ("UAAA", "Алматы (Казахстан)", [("UAAA", "Алматы"), ("UAAH", "Балхаш")]),
        ("UACN", "Астана (Казахстан)", [("UACC", "Астана"), ("UAKK", "Караганда"), ("UASS", "Семей"), ("UASK", "Усть-Каменогорск")]),
        ("UATT", "Актобе (Казахстан)", [("UATT", "Актобе"), ("UATG", "Атырау"), ("UATE", "Актау"), ("UARR", "Уральск")]),
        ("UAII", "Шымкент (Казахстан)", [("UAII", "Шымкент"), ("UAOO", "Кызылорда"), ("UADD", "Тараз")]),
        ("UTTR", "Ташкент (Узбекистан)", [("UTTT", "Ташкент"), ("UTSS", "Самарканд"), ("UTSB", "Бухара"), ("UTFN", "Наманган"), ("UTNU", "Ургенч")]),
        ("UCFM", "Бишкек (Кыргызстан)", [("UCFM", "Манас"), ("UCFO", "Ош")]),
        ("UTDD", "Душанбе (Таджикистан)", [("UTDD", "Душанбе"), ("UTDL", "Худжанд")]),
        ("UTAA", "Ашхабад (Туркменистан)", [("UTAA", "Ашхабад"), ("UTAK", "Туркменбаши"), ("UTAM", "Мары")]),
    ];

    /// <summary>Raised when the starting content grows; existing databases get only what was added.</summary>
    private const int Version = 2;

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

        c.Execute("CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)", transaction: tx);
        bool fresh = c.ExecuteScalar<long>("SELECT COUNT(*) FROM firs", transaction: tx) == 0;
        int version = fresh ? 0 : c.ExecuteScalar<int?>("SELECT CAST(value AS INTEGER) FROM meta WHERE key = 'seed'", transaction: tx) ?? 1;

        if (fresh) AddFirs(c, tx, Firs);
        if (version < 2)
        {
            AddFirs(c, tx, Neighbours);
            c.Execute("UPDATE documents SET body = REPLACE(body, 'Войдите на сайт с CID и паролем SkyNetwork.', 'Войдите через аккаунт SkyNetwork.')",
                transaction: tx);
        }
        c.Execute("INSERT INTO meta (key, value) VALUES ('seed', @Version) ON CONFLICT(key) DO UPDATE SET value = @Version",
            new { Version = Version.ToString() }, tx);

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
                    1. Войдите через аккаунт SkyNetwork.
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

    /// <summary>Adds regions and airports that are not there yet (staff may have created some by hand).</summary>
    private static void AddFirs(SqliteConnection c, SqliteTransaction tx, IEnumerable<(string Code, string Name, (string Icao, string Name)[] Airports)> firs)
    {
        int sort = c.ExecuteScalar<int>("SELECT COALESCE(MAX(sort) + 1, 0) FROM firs", transaction: tx);
        foreach (var (code, name, airports) in firs)
        {
            var id = c.QuerySingleOrDefault<long?>("SELECT id FROM firs WHERE code = @code", new { code }, tx)
                     ?? c.ExecuteScalar<long>("INSERT INTO firs (code, name, sort) VALUES (@code, @name, @sort) RETURNING id",
                         new { code, name, sort = sort++ }, tx);
            foreach (var (icao, an) in airports)
                c.Execute("INSERT INTO airports (icao, fir_id, name) VALUES (@icao, @id, @an) ON CONFLICT(icao) DO NOTHING", new { icao, id, an }, tx);
        }
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
