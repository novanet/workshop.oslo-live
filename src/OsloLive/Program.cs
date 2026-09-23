using System.Globalization;
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

// ---------------------------------------------------------------------------
// Lagene på kartet. Nytt lag? Legg til én linje her.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<ILag, LuftkvalitetLag>();
builder.Services.AddSingleton<ILag, ArterLag>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Hvilke lag finnes?
app.MapGet("/api/lag", (IEnumerable<ILag> lag) =>
    lag.Select(l => new { id = l.Id, navn = l.Navn, beskrivelse = l.Beskrivelse, ikon = l.Ikon }));

// Punktene i ett lag, som GeoJSON.
app.MapGet("/api/lag/{id}", async (string id, IEnumerable<ILag> lag, CancellationToken stopp) =>
{
    var valgt = lag.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
    if (valgt is null)
    {
        return Results.NotFound(new { feil = $"Fant ingen lag med id «{id}»." });
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
