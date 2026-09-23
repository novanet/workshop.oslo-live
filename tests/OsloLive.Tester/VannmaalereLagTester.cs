using System.Text.Json;
using OsloLive.Kart;
using OsloLive.Lag;

namespace OsloLive.Tester;

public class VannmaalereLagTester
{
    private static JsonElement Rad(string json) => JsonDocument.Parse(json).RootElement;

    private const string AkerselvaRad = """
        {
            "station_id": "6.38.0",
            "station_name": "Akerselva v/Elvebakken",
            "river": "Nordmarkvassdraget",
            "municipality": "Oslo",
            "county": "Oslo",
            "latitude": 59.91945,
            "longitude": 10.75348,
            "masl": 4,
            "latest_observation_at": "2026-09-23T11:00:00Z",
            "series": [
                {"parameter":1000,"name":"Vannstand","unit":"m","from":null,"to":null,"latest_data_at":"2026-09-23T11:00:00Z","resolutions":[0,60,1440]},
                {"parameter":1001,"name":"Vannføring","unit":"m³/s","from":null,"to":null,"latest_data_at":"2026-09-23T11:00:00Z","resolutions":[0,60,1440]},
                {"parameter":1003,"name":"Vanntemperatur","unit":"°C","from":"2019-07-01T00:00:00","to":null,"latest_data_at":"2026-09-23T11:00:00Z","resolutions":[0,60,1440]},
                {"parameter":1006,"name":"Ledningsevne","unit":"µS/cm","from":"2019-07-01T00:00:00","to":null,"latest_data_at":"2026-09-23T11:00:00Z","resolutions":[0,60,1440]}
            ],
            "distance_km": 1.1
        }
        """;

    [Fact]
    public void Parametrene_ber_bare_om_stasjoner_med_ferske_data()
    {
        var url = Allemannsdata.ByggUrl("nve", "find_hydro_stations", VannmaalereLag.Parametre);

        Assert.Contains("has_recent_data=true", url);
    }

    [Fact]
    public void Stasjon_blir_punkt_med_stasjonsid_navn_og_kilde()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad(AkerselvaRad));

        Assert.NotNull(punkt);
        Assert.Equal("6.38.0", punkt!.Properties["id"]);
        Assert.Equal("Akerselva v/Elvebakken", punkt.Properties["navn"]);
        Assert.Equal("NVE HydAPI", punkt.Properties["kilde"]);
    }

    [Fact]
    public void Koordinatene_ligger_som_lon_lat()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad(AkerselvaRad));

        Assert.Equal([10.75348, 59.91945], punkt!.Geometry.Coordinates);
    }

    [Fact]
    public void Stasjon_blir_punkt_med_elv_maaler_og_sist_maalt()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad(AkerselvaRad));

        Assert.NotNull(punkt);
        Assert.Equal("Nordmarkvassdraget", punkt!.Properties["elv"]);
        Assert.Equal("Vannstand, Vannføring, Vanntemperatur, Ledningsevne", punkt.Properties["måler"]);
        Assert.Equal("2026-09-23T11:00:00Z", punkt.Properties["sist målt"]);
    }

    [Fact]
    public void Samme_serienavn_to_ganger_vises_en_gang()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "1.1.0", "station_name": "Test", "river": null,
                "latitude": 59.91, "longitude": 10.75, "latest_observation_at": "2026-09-23T11:00:00Z",
                "series": [
                    {"name":"Vannstand","latest_data_at":"2026-09-23T11:00:00Z"},
                    {"name":"Vannstand","latest_data_at":"2026-09-23T10:00:00Z"}
                ]
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Vannstand", punkt!.Properties["måler"]);
    }

    [Fact]
    public void Serier_som_sluttet_for_lenge_siden_telles_ikke_med_i_maaler()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "6.12.0", "station_name": "Vestli", "river": null,
                "latitude": 59.95, "longitude": 10.85, "latest_observation_at": "2026-09-23T12:00:00Z",
                "series": [
                    {"name":"Vannstand","latest_data_at":"2026-09-23T11:00:00Z"},
                    {"name":"Vannhastighet","latest_data_at":"2017-03-09T14:19:00Z"}
                ]
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Vannstand", punkt!.Properties["måler"]);
    }

    [Fact]
    public void Stasjon_uten_elv_mangler_elvfeltet()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "1.1.0", "station_name": "Test", "river": null,
                "latitude": 59.91, "longitude": 10.75, "latest_observation_at": null, "series": []
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("elv"));
    }

    [Fact]
    public void Stasjon_uten_tidspunkt_mangler_sist_maalt()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "1.1.0", "station_name": "Test", "river": null,
                "latitude": 59.91, "longitude": 10.75, "latest_observation_at": null, "series": []
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("sist målt"));
    }

    [Fact]
    public void Stasjon_uten_serier_mangler_maalerfeltet()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "1.1.0", "station_name": "Test", "river": null,
                "latitude": 59.91, "longitude": 10.75, "latest_observation_at": "2026-09-23T11:00:00Z", "series": []
            }
            """));

        Assert.NotNull(punkt);
        Assert.False(punkt!.Properties.ContainsKey("måler"));
    }

    [Fact]
    public void Stasjon_uten_navn_heter_ukjent_stasjon()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "1.1.0", "station_name": null, "river": null,
                "latitude": 59.91, "longitude": 10.75, "latest_observation_at": null, "series": []
            }
            """));

        Assert.NotNull(punkt);
        Assert.Equal("Ukjent stasjon", punkt!.Properties["navn"]);
    }

    [Fact]
    public void Stasjon_uten_posisjon_blir_forkastet()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "1.1.0", "station_name": "Test", "river": null,
                "latitude": null, "longitude": null, "latest_observation_at": null, "series": []
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Stasjon_uten_id_blir_forkastet()
    {
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_name": "Test", "river": null,
                "latitude": 59.91, "longitude": 10.75, "latest_observation_at": null, "series": []
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Stasjon_utenfor_utsnittet_blir_forkastet()
    {
        // Asker RoofTop, langt utenfor Oslo-boksen.
        var punkt = VannmaalereLag.TilPunkt(Rad("""
            {
                "station_id": "9.8.0", "station_name": "Asker RoofTop", "river": null,
                "latitude": 59.8341, "longitude": 10.4457, "latest_observation_at": null, "series": []
            }
            """));

        Assert.Null(punkt);
    }

    [Fact]
    public void Samme_stasjon_to_ganger_gir_ett_punkt()
    {
        var rad = Rad(AkerselvaRad);

        var lag = Geo.Samle([VannmaalereLag.TilPunkt(rad), VannmaalereLag.TilPunkt(rad)]);

        Assert.Single(lag.Features);
    }
}
