using System.Text.Json;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class KollektivavvikLagTester
{
    private static readonly DateTimeOffset Nå = DateTimeOffset.Parse("2026-09-23T12:00:00Z");

    private static JsonElement Situasjoner(string json) => JsonDocument.Parse(json).RootElement;

    // Et fast, oppdiktet GraphQL-svar (aldri fra nettet): ett aktivt avvik med sluttid,
    // ett aktivt avvik uten sluttid (via AffectedStopPlaceOnLine), ett utløpt avvik,
    // og ett avvik som bare berører en linje, uten noen holdeplass.
    private const string Svar = """
        [
          {
            "id": "RUT:SituationNumber:1",
            "summary": [{ "value": "Holdeplassen er midlertidig flyttet.", "language": "no" }],
            "severity": "slight",
            "validityPeriod": { "startTime": "2026-09-20T00:00:00Z", "endTime": "2026-09-25T00:00:00Z" },
            "affects": {
              "stopPlaces": [
                { "stopPlace": { "id": "NSR:StopPlace:1", "name": "Jernbanetorget", "latitude": 59.9110, "longitude": 10.7528 } }
              ]
            }
          },
          {
            "id": "RUT:SituationNumber:2",
            "summary": [{ "value": "Line 20 is affected.", "language": "en" }, { "value": "Linje 20 kjører ikke.", "language": "no" }],
            "severity": "severe",
            "validityPeriod": { "startTime": "2026-09-01T00:00:00Z" },
            "affects": {
              "stopPlacesOnLines": [
                { "stopPlace": { "id": "NSR:StopPlace:2", "name": "Majorstuen", "latitude": 59.9296, "longitude": 10.7154 }, "line": { "id": "RUT:Line:20" } }
              ]
            }
          },
          {
            "id": "RUT:SituationNumber:3",
            "summary": [{ "value": "Utløpt avvik.", "language": "no" }],
            "severity": "normal",
            "validityPeriod": { "startTime": "2026-01-01T00:00:00Z", "endTime": "2026-01-02T00:00:00Z" },
            "affects": {
              "stopPlaces": [
                { "stopPlace": { "id": "NSR:StopPlace:3", "name": "Skøyen", "latitude": 59.9219, "longitude": 10.6837 } }
              ]
            }
          },
          {
            "id": "RUT:SituationNumber:4",
            "summary": [{ "value": "Linje 30 innstilt.", "language": "no" }],
            "severity": "severe",
            "validityPeriod": { "startTime": "2026-09-20T00:00:00Z" },
            "affects": {
              "lines": [{ "line": { "id": "RUT:Line:30" } }]
            }
          }
        ]
        """;

    [Fact]
    public void Bare_aktive_avvik_med_holdeplass_blir_punkt()
    {
        var lag = KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå);

        Assert.Equal(2, lag.Features.Count);
        var ider = lag.Features.Select(f => f.Properties["id"]).ToList();
        Assert.Contains("RUT:SituationNumber:1|NSR:StopPlace:1", ider);
        Assert.Contains("RUT:SituationNumber:2|NSR:StopPlace:2", ider);
    }

    [Fact]
    public void Aktivt_avvik_faar_navn_kilde_avvik_og_alvorlighet_fra_holdeplassen()
    {
        var lag = KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå);
        var punkt = lag.Features.Single(f => (string)f.Properties["id"]! == "RUT:SituationNumber:1|NSR:StopPlace:1");

        Assert.Equal("Jernbanetorget", punkt.Properties["navn"]);
        Assert.Equal("Entur", punkt.Properties["kilde"]);
        Assert.Equal("Holdeplassen er midlertidig flyttet.", punkt.Properties["avvik"]);
        Assert.Equal("slight", punkt.Properties["alvorlighet"]);
        Assert.Equal("25.09.2026 00:00", punkt.Properties["gjelder til"]);
    }

    [Fact]
    public void Norsk_sammendrag_velges_foran_andre_sprak()
    {
        var lag = KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå);
        var punkt = lag.Features.Single(f => (string)f.Properties["id"]! == "RUT:SituationNumber:2|NSR:StopPlace:2");

        Assert.Equal("Linje 20 kjører ikke.", punkt.Properties["avvik"]);
    }

    [Fact]
    public void Avvik_uten_sluttid_gjelder_inntil_videre()
    {
        var lag = KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå);
        var punkt = lag.Features.Single(f => (string)f.Properties["id"]! == "RUT:SituationNumber:2|NSR:StopPlace:2");

        Assert.Equal("inntil videre", punkt.Properties["gjelder til"]);
    }

    [Fact]
    public void Utlopt_avvik_blir_ikke_punkt()
    {
        var lag = KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå);

        Assert.DoesNotContain(lag.Features, f => ((string)f.Properties["id"]!).StartsWith("RUT:SituationNumber:3"));
    }

    [Fact]
    public void Avvik_som_bare_berorer_en_linje_hoppes_over_uten_at_laget_feiler()
    {
        var unntak = Record.Exception(() => KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå));

        Assert.Null(unntak);
        var lag = KollektivavvikLag.TilPunkter(Situasjoner(Svar), Nå);
        Assert.DoesNotContain(lag.Features, f => ((string)f.Properties["id"]!).StartsWith("RUT:SituationNumber:4"));
    }

    [Fact]
    public void Tomt_svar_gir_tomt_lag()
    {
        var lag = KollektivavvikLag.TilPunkter(Situasjoner("[]"), Nå);

        Assert.Empty(lag.Features);
    }
}
