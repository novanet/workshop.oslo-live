using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OsloLive.Historikk;
using OsloLive.Kart;

namespace OsloLive.Tester;

/// <summary>Tester <see cref="Bildelager"/> direkte mot en midlertidig mappe, uten nettverk og uten app-verten.</summary>
public sealed class BildelagerTester : IDisposable
{
    private static readonly DateTimeOffset Nå = new(2026, 9, 23, 8, 0, 0, TimeSpan.Zero);

    private readonly string mappe = Path.Combine(Path.GetTempPath(), "oslolive-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(mappe))
        {
            Directory.Delete(mappe, recursive: true);
        }
    }

    private static Kartlag EttPunkt(string id = "a") =>
        Geo.Samle([Geo.Lag(id, 59.91, 10.75, "A", "Test")]);

    private static Kartlag ToPunkter() =>
        Geo.Samle([Geo.Lag("a", 59.91, 10.75, "A", "Test"), Geo.Lag("b", 59.92, 10.76, "B", "Test")]);

    [Fact]
    public void Lagret_bilde_leses_tilbake_med_antall_punkter()
    {
        var lager = new Bildelager(mappe);

        lager.Lagre("luftkvalitet", ToPunkter(), Nå);
        var bilder = lager.Les("luftkvalitet", Nå);

        var bilde = Assert.Single(bilder);
        Assert.Equal(2, bilde.Antall);
        Assert.Equal(Nå, bilde.Tidspunkt);
    }

    [Fact]
    public void Bildet_lagres_som_FeatureCollection_paa_disk()
    {
        var lager = new Bildelager(mappe);

        lager.Lagre("luftkvalitet", ToPunkter(), Nå);

        var filer = Directory.GetFiles(Path.Combine(mappe, "luftkvalitet"), "*.json");
        var fil = Assert.Single(filer);
        var rot = JsonDocument.Parse(File.ReadAllText(fil)).RootElement;
        Assert.Equal("FeatureCollection", rot.GetProperty("type").GetString());
        Assert.Equal(2, rot.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public void Lag_uten_bilder_gir_tom_liste()
    {
        var lager = new Bildelager(mappe);

        var bilder = lager.Les("luftkvalitet", Nå);

        Assert.Empty(bilder);
    }

    [Fact]
    public void Bare_siste_24_timer_leses_eldste_forst()
    {
        var lager = new Bildelager(mappe);

        lager.Lagre("luftkvalitet", EttPunkt(), Nå.AddHours(-1));
        lager.Lagre("luftkvalitet", EttPunkt(), Nå.AddHours(-25));
        lager.Lagre("luftkvalitet", EttPunkt(), Nå.AddHours(-2));

        var bilder = lager.Les("luftkvalitet", Nå);

        Assert.Equal([Nå.AddHours(-2), Nå.AddHours(-1)], bilder.Select(b => b.Tidspunkt));
    }

    [Fact]
    public void Bilder_eldre_enn_7_dager_slettes()
    {
        var lager = new Bildelager(mappe);

        lager.Lagre("luftkvalitet", EttPunkt(), Nå.AddDays(-8));
        lager.Lagre("luftkvalitet", EttPunkt(), Nå.AddDays(-6));

        lager.Rydd(Nå);

        var filer = Directory.GetFiles(Path.Combine(mappe, "luftkvalitet"), "*.json");
        var fil = Assert.Single(filer);
        Assert.StartsWith(Nå.AddDays(-6).ToString(Bildelager.Filformat, CultureInfo.InvariantCulture), Path.GetFileName(fil));
    }

    [Fact]
    public void Oedelagt_fil_hoppes_over()
    {
        var lager = new Bildelager(mappe);
        var lagMappe = Path.Combine(mappe, "luftkvalitet");
        Directory.CreateDirectory(lagMappe);
        File.WriteAllText(Path.Combine(lagMappe, "20260923T070000Z.json"), "{");

        lager.Lagre("luftkvalitet", EttPunkt(), Nå);
        var bilder = lager.Les("luftkvalitet", Nå);

        var bilde = Assert.Single(bilder);
        Assert.Equal(Nå, bilde.Tidspunkt);
    }

    [Fact]
    public void Siste_hopper_over_oedelagt_fil()
    {
        var lager = new Bildelager(mappe);
        lager.Lagre("luftkvalitet", EttPunkt(), Nå.AddHours(-2));
        File.WriteAllText(Path.Combine(mappe, "luftkvalitet", "20260923T070000Z.json"), "{");

        var siste = lager.Siste("luftkvalitet");

        Assert.Equal(Nå.AddHours(-2), siste);
    }

    [Fact]
    public async Task Jobben_fortsetter_med_neste_lag_naar_en_kilde_avbrytes()
    {
        var lager = new Bildelager(mappe);
        var oppsett = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Historikk:Aktiv"] = "true" })
            .Build();
        ILag[] lagene = [new TregtLag(), new RasktLag()];
        using var jobb = new Bildejobb(lagene, lager, oppsett, NullLogger<Bildejobb>.Instance);

        // Første runde med bilder tas rett etter StartAsync. Vent (med tak) til det raske
        // laget er lagret; stopper jobben etter det trege laget, blir det aldri noe bilde.
        await jobb.StartAsync(CancellationToken.None);
        SpinWait.SpinUntil(() => lager.Siste("rask") is not null, TimeSpan.FromSeconds(5));
        await jobb.StopAsync(CancellationToken.None);

        Assert.Empty(lager.Les("treg", DateTimeOffset.UtcNow));
        Assert.Single(lager.Les("rask", DateTimeOffset.UtcNow));
    }

    /// <summary>Etterligner et lag der HttpClient sin egen timeout slår inn, uten at tjenesten er stoppet.</summary>
    private sealed class TregtLag : ILag
    {
        public string Id => "treg";
        public string Navn => "Treg kilde";
        public string Beskrivelse => "Svarer aldri i tide.";
        public string Ikon => "🐢";
        public Task<Kartlag> Hent(CancellationToken stopp = default) =>
            Task.FromException<Kartlag>(new TaskCanceledException("Tidsavbrudd", new TimeoutException()));
    }

    private sealed class RasktLag : ILag
    {
        public string Id => "rask";
        public string Navn => "Rask kilde";
        public string Beskrivelse => "Svarer med ett punkt.";
        public string Ikon => "🐇";
        public Task<Kartlag> Hent(CancellationToken stopp = default) => Task.FromResult(EttPunkt());
    }

    [Theory]
    [InlineData("x")]
    public void Relativ_mappe_havner_under_temp(string oppsatt)
    {
        var funnet = Bildelager.FinnMappe(oppsatt);

        Assert.Equal(Path.Combine(Path.GetTempPath(), oppsatt), funnet);
    }

    [Fact]
    public void Absolutt_mappe_brukes_uendret()
    {
        var absolutt = Path.Combine(Path.GetTempPath(), "en-annen-mappe");

        var funnet = Bildelager.FinnMappe(absolutt);

        Assert.Equal(absolutt, funnet);
    }
}
