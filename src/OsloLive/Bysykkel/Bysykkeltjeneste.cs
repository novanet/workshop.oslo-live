using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace OsloLive.Bysykkel;

/// <summary>Resultatet av <see cref="Bysykkeltjeneste.Hent"/>.</summary>
public abstract record Tilstand
{
    public sealed record Klar(Bysykkeldøgn Døgn) : Tilstand;

    public sealed record Forbereder : Tilstand;

    public sealed record Feilet(string Feil) : Tilstand;
}

/// <summary>
/// Henter, mellomlagrer og forbereder ett døgn med bysykkelturer i bakgrunnen.
/// Ikke en <c>IHostedService</c>: jobben starter bare når noen spør, og starter
/// den samme jobben aldri to ganger samtidig, se <see cref="Hent"/>.
/// </summary>
public sealed class Bysykkeltjeneste(IHttpClientFactory klienter, IMemoryCache mellomlager, string mappe, ILogger<Bysykkeltjeneste> logg)
{
    private static readonly JsonSerializerOptions Valg = JsonSerializerOptions.Web;

    private readonly Lock lås = new();
    private (int År, int Måned) jobbNøkkel;
    private Task<Bysykkeldøgn>? jobb;

    public Tilstand Hent(DateTimeOffset nå)
    {
        var (år, måned) = Bysykkeldogn.SisteHeleMåned(nå);
        var nøkkel = string.Create(CultureInfo.InvariantCulture, $"bysykkeldogn:{år:0000}-{måned:00}");

        if (mellomlager.TryGetValue(nøkkel, out Bysykkeldøgn? d) && d is not null)
        {
            return new Tilstand.Klar(d);
        }

        lock (lås)
        {
            if (jobb is not null && jobbNøkkel == (år, måned))
            {
                if (jobb.IsCompletedSuccessfully)
                {
                    var døgn = jobb.Result;
                    mellomlager.Set(nøkkel, døgn);
                    jobb = null;
                    return new Tilstand.Klar(døgn);
                }

                if (jobb.IsFaulted)
                {
                    var feil = jobb.Exception!.GetBaseException().Message;
                    jobb = null;
                    return new Tilstand.Feilet(feil);
                }

                return new Tilstand.Forbereder();
            }

            jobbNøkkel = (år, måned);
            jobb = Task.Run(() => Forbered(år, måned, CancellationToken.None));
            return new Tilstand.Forbereder();
        }
    }

    private async Task<Bysykkeldøgn> Forbered(int år, int måned, CancellationToken stopp)
    {
        var fil = Path.Combine(mappe, string.Create(CultureInfo.InvariantCulture, $"bysykkeldogn-{år:0000}-{måned:00}.json.gz"));

        if (File.Exists(fil))
        {
            await using var lagretStrøm = File.OpenRead(fil);
            await using var utpakket = new GZipStream(lagretStrøm, CompressionMode.Decompress);
            return await JsonSerializer.DeserializeAsync<Bysykkeldøgn>(utpakket, Valg, stopp)
                ?? throw new InvalidOperationException($"Fant ingen data i mellomlagret fil for {år:0000}-{måned:00}.");
        }

        var klient = klienter.CreateClient("bysykkel");
        using var svar = await klient.GetAsync(Bysykkeldogn.MånedsUrl(år, måned), HttpCompletionOption.ResponseHeadersRead, stopp);
        svar.EnsureSuccessStatusCode();
        await using var strøm = await svar.Content.ReadAsStreamAsync(stopp);

        var døgn = await Bysykkeldogn.Velg(strøm, stopp)
            ?? throw new InvalidOperationException($"Fant ingen hverdag med turer i {år:0000}-{måned:00}.");

        Directory.CreateDirectory(mappe);
        var midlertidig = fil + ".tmp";
        await using (var nyStrøm = File.Create(midlertidig))
        await using (var komprimert = new GZipStream(nyStrøm, CompressionLevel.Optimal))
        {
            await JsonSerializer.SerializeAsync(komprimert, døgn, Valg, stopp);
        }

        File.Move(midlertidig, fil, overwrite: true);

        logg.LogInformation("Bysykkeldøgn for {År:0000}-{Måned:00} forberedt: {Dato} med {Antall} turer", år, måned, døgn.Dato, døgn.Turer.Count);
        return døgn;
    }
}
