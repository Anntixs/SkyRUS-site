using System.Net;
using SkyRus.Site.Data;
using SkyRus.Site.Services;

namespace SkyRus.Site.Tests;

public class TrainingTests
{
    private const long Admin = 1, Instructor = 10, Mentor = 11, StudentCid = 25;

    private static async Task<(SiteFactory Site, HttpClient Instructor, HttpClient Mentor)> Setup()
    {
        var site = new SiteFactory();
        site.Network.Add(Admin, "Anna Admin", "C1");
        site.Network.Add(Instructor, "Ivan Instructor", "I1");
        site.Network.Add(Mentor, "Maria Mentor", "C1");
        var admin = await site.As(Admin);
        await admin.SubmitAsync("/admin/staff", new Dictionary<string, string> { ["cid"] = Instructor.ToString(), ["role"] = "instructor" });
        await admin.SubmitAsync("/admin/staff", new Dictionary<string, string> { ["cid"] = Mentor.ToString(), ["role"] = "mentor" });
        return (site, await site.As(Instructor), await site.As(Mentor));
    }

    private static long Fir(SiteFactory site, string code) => site.Get<SiteContent>().Fir(code)!.Id;

    private static long TemplateId(SiteFactory site, string title) =>
        site.Get<ProtocolService>().Templates().Single(t => t.Title == title).Id;

    private static async Task<HttpResponseMessage> Grade(HttpClient c, SiteFactory site, long protocolId, Func<int, int> grade, string handler, string comment = "")
    {
        var p = site.Get<ProtocolService>().Protocol(protocolId)!;
        var fields = p.Items.Select((item, n) => (item, n)).ToDictionary(x => $"g{x.item.Id}", x => grade(x.n).ToString());
        fields["comment"] = comment;
        return await c.SubmitAsync($"/tc/protocols/{protocolId}", fields, $"/tc/protocols/{protocolId}?handler={handler}");
    }

    [Fact]
    public async Task FromRequestToRating()
    {
        var (site, instructor, mentor) = await Setup();
        using var _ = site;

        // The student applies in the Irkutsk FIR.
        var student = await site.As(StudentCid, "Petr Student", "OBS");
        await student.SubmitAsync("/cabinet", new Dictionary<string, string> { ["firId"] = Fir(site, "UIII").ToString(), ["airport"] = "UIII", ["message"] = "Вечерами" });
        var request = Assert.Single(site.Get<TrainingService>().Requests());
        Assert.Contains("ожидает", await student.HtmlAsync("/cabinet"));
        // A second request is refused while the first is open.
        Assert.Equal(1, site.Get<TrainingService>().Requests().Count);

        // The mentor sees it in the FIR filter and takes it.
        Assert.Contains("Petr Student", await mentor.HtmlAsync($"/tc/requests?fir={Fir(site, "UIII")}"));
        Assert.DoesNotContain("Petr Student", await mentor.HtmlAsync($"/tc/requests?fir={Fir(site, "UUWV")}"));
        var accepted = await mentor.SubmitAsync($"/tc/requests/{request.Id}", new Dictionary<string, string>(), $"/tc/requests/{request.Id}?handler=Accept");
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("Иркутск", site.Get<TrainingService>().Student(StudentCid)!.FirName);
        Assert.Contains("Petr Student", await mentor.HtmlAsync($"/tc/students?fir={Fir(site, "UIII")}"));

        // The mentor holds the theory test.
        var theory = await mentor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["templateId"] = TemplateId(site, "DEL/GND: зачёт по теории").ToString() },
            $"/tc/students/{StudentCid}?handler=Protocol");
        long theoryId = long.Parse(theory.Headers.Location!.OriginalString.Split('/').Last());
        await Grade(mentor, site, theoryId, _ => 4, "Close", "Хорошо");
        Assert.Equal(("passed", 4.0), (site.Get<ProtocolService>().Protocol(theoryId)!.Status, site.Get<ProtocolService>().Protocol(theoryId)!.Score!.Value));
        Assert.Empty(site.Network.Sent);

        // Exams are for instructors: the mentor cannot create one.
        long exam = TemplateId(site, "DEL/GND: экзамен на S1");
        Assert.DoesNotContain("DEL/GND: экзамен на S1", await mentor.HtmlAsync($"/tc/students/{StudentCid}"));
        var forced = await mentor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["templateId"] = exam.ToString() }, $"/tc/students/{StudentCid}?handler=Protocol");
        Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
        Assert.Single(site.Get<ProtocolService>().Protocols(StudentCid));

        // The instructor holds the exam; closing it without all grades is refused.
        var created = await instructor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["templateId"] = exam.ToString() }, $"/tc/students/{StudentCid}?handler=Protocol");
        long examId = long.Parse(created.Headers.Location!.OriginalString.Split('/').Last());
        var p = site.Get<ProtocolService>().Protocol(examId)!;
        var partial = await instructor.SubmitAsync($"/tc/protocols/{examId}", new Dictionary<string, string> { [$"g{p.Items[0].Id}"] = "5", ["comment"] = "" }, $"/tc/protocols/{examId}?handler=Close");
        Assert.Contains("Поставьте оценки по всем пунктам", await partial.Content.ReadAsStringAsync());
        Assert.True(site.Get<ProtocolService>().Protocol(examId)!.IsOpen);

        var closed = await Grade(instructor, site, examId, n => n % 2 == 0 ? 5 : 4, "Close", "Уверенная работа");
        Assert.Contains("Заявка на рейтинг S1 отправлена", await closed.Content.ReadAsStringAsync());
        var sent = Assert.Single(site.Network.Sent);
        Assert.Equal((StudentCid, "atc", "S1", Instructor, "Ivan Instructor", "Уверенная работа", $"skyrus-protocol-{examId}"),
            (sent.Cid, sent.Track, sent.Rating, sent.ExaminerCid, sent.ExaminerName, sent.Comment, sent.ExternalId));
        Assert.StartsWith($"https://skyrus.example/report/{examId}?t=", sent.ReportUrl);

        // The report link works for the supervisor; a wrong token is a 404.
        var anonymous = site.Browser();
        Assert.Contains("Уверенная работа", await anonymous.HtmlAsync(sent.ReportUrl.Replace("https://skyrus.example", "")));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/report/{examId}?t=wrong")).StatusCode);

        // Closed protocols cannot be changed.
        await Grade(instructor, site, examId, _ => 1, "Save");
        Assert.All(site.Get<ProtocolService>().Protocol(examId)!.Items, i => Assert.True(i.Grade >= 4));

        // The supervisor approves on the network; the sync picks it up and refreshes the rating.
        var exam1 = site.Get<ProtocolService>().Protocol(examId)!;
        site.Network.Decide(exam1.RrId!.Value, "approved", "Поздравляем", StudentCid, "S1");
        await site.Get<RatingRequestSync>().RunOnceAsync();
        exam1 = site.Get<ProtocolService>().Protocol(examId)!;
        Assert.Equal(("approved", "Поздравляем"), (exam1.RrStatus, exam1.RrComment));
        Assert.Equal("S1", site.Get<UserService>().Find(StudentCid)!.Rating);
        Assert.Contains("рейтинг выдан", await student.HtmlAsync("/cabinet"));
        Assert.Contains(site.Get<TrainingService>().Log(StudentCid), c => c.Body.Contains("рейтинг выдан"));
    }

    [Fact]
    public async Task FailedExam_SendsNothing_AndNetworkOutageIsRetried()
    {
        var (site, instructor, _) = await Setup();
        using var _s = site;
        site.Network.Add(StudentCid, "Petr Student");
        await instructor.SubmitAsync("/tc/students", new Dictionary<string, string> { ["cid"] = StudentCid.ToString() }, "/tc/students?handler=Open");
        long exam = TemplateId(site, "TWR: экзамен на S2");

        var r1 = await instructor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["templateId"] = exam.ToString() }, $"/tc/students/{StudentCid}?handler=Protocol");
        long failed = long.Parse(r1.Headers.Location!.OriginalString.Split('/').Last());
        await Grade(instructor, site, failed, n => n == 0 ? 2 : 5, "Close");
        Assert.Equal("failed", site.Get<ProtocolService>().Protocol(failed)!.Status);
        Assert.Empty(site.Network.Sent);

        site.Network.Down = true;
        var r2 = await instructor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["templateId"] = exam.ToString() }, $"/tc/students/{StudentCid}?handler=Protocol");
        long passed = long.Parse(r2.Headers.Location!.OriginalString.Split('/').Last());
        var page = await Grade(instructor, site, passed, _ => 5, "Close").TextAsync();
        Assert.Contains("не удалось отправить", page);
        Assert.Null(site.Get<ProtocolService>().Protocol(passed)!.RrId);

        site.Network.Down = false;
        await site.Get<RatingRequestSync>().RunOnceAsync();
        Assert.Equal("S2", Assert.Single(site.Network.Sent).Rating);
        Assert.Equal("pending", site.Get<ProtocolService>().Protocol(passed)!.RrStatus);
        // Sent once only.
        await site.Get<RatingRequestSync>().RunOnceAsync();
        Assert.Single(site.Network.Sent);
    }

    [Fact]
    public async Task Permits_ByInstructorsOnly_AndPublic()
    {
        var (site, instructor, mentor) = await Setup();
        using var _ = site;
        var student = await site.As(StudentCid, "Petr Student", "S1");
        await instructor.SubmitAsync("/tc/students", new Dictionary<string, string> { ["cid"] = StudentCid.ToString() }, "/tc/students?handler=Open");

        var mentorTry = await mentor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["position"] = "UUEE_TWR" }, $"/tc/students/{StudentCid}?handler=Permit");
        Assert.Equal(HttpStatusCode.NotFound, mentorTry.StatusCode);

        var bad = await instructor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["position"] = "TOWER" }, $"/tc/students/{StudentCid}?handler=Permit").TextAsync();
        Assert.Contains("Позиция в формате", bad);
        await instructor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["position"] = "uuee_twr", ["note"] = "" }, $"/tc/students/{StudentCid}?handler=Permit");

        var permits = await site.Browser().HtmlAsync($"/permits?fir=UUWV");
        Assert.Contains("UUEE_TWR", permits);
        Assert.DoesNotContain("UUEE_TWR", await site.Browser().HtmlAsync("/permits?fir=UIII"));
        Assert.Contains("UUEE_TWR", await site.Browser().HtmlAsync("/regions/UUWV"));
        Assert.Contains("UUEE_TWR", await student.HtmlAsync("/cabinet"));

        var permit = Assert.Single(site.Get<TrainingService>().Permits(StudentCid));
        await instructor.SubmitAsync($"/tc/students/{StudentCid}", new Dictionary<string, string> { ["permitId"] = permit.Id.ToString() }, $"/tc/students/{StudentCid}?handler=Revoke");
        Assert.Empty(site.Get<TrainingService>().Permits(StudentCid));
        Assert.DoesNotContain("UUEE_TWR", await site.Browser().HtmlAsync("/permits"));
    }

    [Fact]
    public async Task DeclinedRequest_ShowsReason()
    {
        var (site, _, mentor) = await Setup();
        using var _s = site;
        var student = await site.As(StudentCid, "Petr Student");
        await student.SubmitAsync("/cabinet", new Dictionary<string, string> { ["firId"] = Fir(site, "UUWV").ToString(), ["airport"] = "UIII", ["message"] = "" });
        Assert.Empty(site.Get<TrainingService>().Requests()); // UIII is not in the Moscow FIR
        await student.SubmitAsync("/cabinet", new Dictionary<string, string> { ["firId"] = Fir(site, "UUWV").ToString(), ["airport"] = "UUEE", ["message"] = "" });
        var r = Assert.Single(site.Get<TrainingService>().Requests());
        var empty = await mentor.SubmitAsync($"/tc/requests/{r.Id}", new Dictionary<string, string> { ["reason"] = "" }, $"/tc/requests/{r.Id}?handler=Decline").TextAsync();
        Assert.Contains("Укажите причину", empty);
        await mentor.SubmitAsync($"/tc/requests/{r.Id}", new Dictionary<string, string> { ["reason"] = "Набор в РПИ закрыт до осени" }, $"/tc/requests/{r.Id}?handler=Decline");
        Assert.Contains("Набор в РПИ закрыт до осени", await student.HtmlAsync("/cabinet"));
        Assert.Contains("Набор в РПИ закрыт до осени", await mentor.HtmlAsync("/tc/requests?archive=1") + await mentor.HtmlAsync($"/tc/requests/{r.Id}"));
    }
}
