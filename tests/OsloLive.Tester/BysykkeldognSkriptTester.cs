namespace OsloLive.Tester;

/// <summary>
/// Tester koblingen mellom bysykkeldøgnet (#191) og hovedskriptet i index.html, uten nettverk
/// og uten å starte appen. Avspillingen skal skjule de levende lagene og hindre at de hentes
/// mens den går; det gjør effekten ved å bytte ut de globale funksjonene hent og hentBydeler
/// på window. Disse testene sikrer at forutsetningene for den overstyringen holder.
/// </summary>
public class BysykkeldognSkriptTester
{
    private static readonly string Rot = FinnRepoRot();
    private static readonly string IndexHtml = Path.Combine(Rot, "src", "OsloLive", "wwwroot", "index.html");
    private static readonly string Skript = Path.Combine(Rot, "src", "OsloLive", "wwwroot", "effekter", "bysykkeldogn.js");

    /// <summary>Går oppover fra testens utdatamappe til mappa som inneholder både src og tests.</summary>
    private static string FinnRepoRot()
    {
        var mappe = new DirectoryInfo(AppContext.BaseDirectory);
        while (mappe is not null)
        {
            if (Directory.Exists(Path.Combine(mappe.FullName, "src")) && Directory.Exists(Path.Combine(mappe.FullName, "tests")))
                return mappe.FullName;
            mappe = mappe.Parent;
        }
        throw new InvalidOperationException("Fant ingen mappe med både src og tests over " + AppContext.BaseDirectory);
    }

    private static string[] Linjer(string sti) => File.ReadAllLines(sti);

    [Fact]
    public void Index_html_deklarerer_hent_som_toppnivaafunksjon()
    {
        // `async function hent(` helt uten innrykk er en klassisk funksjonsdeklarasjon på
        // toppnivå i et vanlig (ikke-modul) skript. Slike deklarasjoner blir egenskaper på
        // window, og kallene `hent(id)` i 15-sekundersintervallet slår opp navnet ved hvert
        // kall. Det er derfor bysykkeldogn.js kan sette window.hent til en tom funksjon under
        // avspillingen og dermed stoppe all henting av levende data, og sette den tilbake
        // ved stopp. Flyttes hent inn i en modul, en klasse eller en const, ryker koblingen.
        Assert.Contains(Linjer(IndexHtml), l => l.StartsWith("async function hent(", StringComparison.Ordinal));
    }

    [Fact]
    public void Index_html_deklarerer_hentBydeler_som_toppnivaafunksjon()
    {
        // Samme resonnement som for hent: bydelene hentes i samme intervall, og
        // bysykkeldogn.js overstyrer window.hentBydeler på samme måte.
        Assert.Contains(Linjer(IndexHtml), l => l.StartsWith("async function hentBydeler(", StringComparison.Ordinal));
    }

    [Fact]
    public void Index_html_laster_bysykkeldogn_skriptet_noeyaktig_en_gang()
    {
        // Ett script-tag: lastes skriptet to ganger, får vi to knapper og to overstyringer
        // som lagrer hverandres tomme funksjon som «original», så hent aldri kommer tilbake.
        var linjer = Linjer(IndexHtml)
            .Where(l => l.Contains("<script", StringComparison.Ordinal) && l.Contains("effekter/bysykkeldogn.js", StringComparison.Ordinal))
            .ToList();

        Assert.Single(linjer);
    }

    [Fact]
    public void Skriptet_overstyrer_window_hent_under_avspilling()
    {
        // Selve overstyringen: uten den ville lagene bli hentet hvert 15. sekund under
        // avspillingen selv om de er skjult.
        var innhold = File.ReadAllText(Skript);

        Assert.Contains("window.hent = ", innhold);
        Assert.Contains("window.hentBydeler = ", innhold);
    }

    [Fact]
    public void Skriptet_laaser_lagvelgeren_under_avspilling()
    {
        // Lagvelgeren låses (disabled = true) så et klikk under avspillingen ikke kan slå
        // et lag på igjen; ellers ville et levende lag vises oppå lerretet.
        var innhold = File.ReadAllText(Skript);

        Assert.Contains("disabled = true", innhold);
        Assert.Contains("disabled = false", innhold);
    }
}
