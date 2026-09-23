using System.Globalization;
using OsloLive.Historikk;
using OsloLive.Kart;
using OsloLive.Lag;

// Oslo Live: et kart over Oslo som henter levende data fra Allemannsdata.
// Hvert lag på kartet er en klasse som implementerer ILag.

// Appen er norsk, så den kjører med norsk kultur.
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("nb-NO");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("nb-NO");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<Allemannsdata>(klient =>
{
    klient.Timeout = TimeSpan.FromSeconds(30);
    klient.DefaultRequestHeaders.UserAgent.ParseAdd("OsloLive/1.0 (kurs)");
});

// Flylaget bruker ikke Allemannsdata (ingen kilde der har flyposisjoner),
// og trenger derfor sin egen navngitte HttpClient og eget mellomlager. Se FlyLag.cs.
builder.Services.AddHttpClient("fly", klient =>
{
    klient.Timeout = TimeSpan.FromSeconds(10);
    klient.DefaultRequestHeaders.UserAgent.ParseAdd("OsloLive/1.0 (kurs)");
});
builder.Services.AddMemoryCache();

// Øyeblikksbilder av hvert lag hver time, slik at tidslinjen kan vise hvordan kartet så ut. Se Historikk/.
builder.Services.AddSingleton(tjenester =>
{
    var mappe = tjenester.GetRequiredService<IConfiguration>()["Historikk:Mappe"];
    return new Bildelager(string.IsNullOrWhiteSpace(mappe) ? Path.Combine(Path.GetTempPath(), "oslolive-historikk") : mappe);
});
builder.Services.AddHostedService<Øyeblikksjobb>();

// ---------------------------------------------------------------------------
// Lagene på kartet. Nytt lag? Legg til én linje her.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ILag, LuftkvalitetLag>();
builder.Services.AddSingleton<ILag, FlyLag>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Hvilke lag finnes?
app.MapGet("/api/lag", (IEnumerable<ILag> lag) =>
    lag.Select(l => new { id = l.Id, navn = l.Navn, beskrivelse = l.Beskrivelse, ikon = l.Ikon }));

// Punktene i ett lag, som GeoJSON. Med ?tid= hentes bildet lagret nærmest det tidspunktet, se Historikk/.
app.MapGet("/api/lag/{id}", async (string id, string? tid, IEnumerable<ILag> lag, Bildelager bilder, CancellationToken stopp) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
    }

    if (tid is not null)
    {
        if (!DateTimeOffset.TryParse(tid, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var tidspunkt))
        {
            return Results.BadRequest(new { feil = $"«{tid}» er ikke et gyldig tidspunkt. Bruk ISO 8601, for eksempel 2026-09-18T08:00:00Z." });
        }

        return Results.Ok(await bilder.HentNærmest(valgt.Id, tidspunkt, stopp) ?? new Kartlag("FeatureCollection", []));
    }

    try
    {
        return Results.Ok(await valgt.Hent(stopp));
    }
    catch (Exception ex)
    {
        // Et lag som feiler skal ikke ta ned kartet.
        app.Logger.LogError(ex, "Laget {Id} feilet", id);
        return Results.Json(new { feil = ex.Message }, statusCode: 502);
    }
});

app.MapGet("/api/helse", () => new { status = "ok", tid = DateTimeOffset.Now });

app.Run();

/// <summary>Gjør Program synlig for testprosjektet.</summary>
public partial class Program;
