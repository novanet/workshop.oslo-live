using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace OsloLive.Tester;

/// <summary>
/// Tester strukturert logging på tvers av verten: at et lag som feiler mot kilden logges som
/// advarsel med <c>Id</c>, og at konsolloggen er JSON utenom Development og lesbar tekst i Development.
/// </summary>
public class LoggTester(TestVert vert) : IClassFixture<TestVert>
{
    [Fact]
    public async Task Lag_som_feiler_mot_kilden_logger_advarsel_med_id_ikke_feil()
    {
        var samler = new OppsamlingsLogger();
        using var vertMedSvikt = vert.WithWebHostBuilder(b => b.ConfigureServices(tjenester =>
        {
            tjenester.AddHttpClient("fly").ConfigurePrimaryHttpMessageHandler(() => new SviktHandler());
            tjenester.AddSingleton<ILoggerProvider>(samler);
        }));
        var klient = vertMedSvikt.CreateClient();

        await klient.GetAsync("/api/lag/fly");

        Assert.Contains(samler.Oppføringer, o => o.Nivå == LogLevel.Warning && o.Melding.Contains("fly"));
        Assert.DoesNotContain(samler.Oppføringer, o => o.Nivå == LogLevel.Error && o.Melding.Contains("fly"));
    }

    [Fact]
    public void Konsollen_logger_json_utenom_development()
    {
        using var vertUtenDev = vert.WithWebHostBuilder(b => b.UseEnvironment("Production"));

        var valg = vertUtenDev.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>();

        Assert.Equal("json", valg.CurrentValue.FormatterName);
    }

    [Fact]
    public void Konsollen_logger_lesbar_tekst_i_development()
    {
        var valg = vert.Services.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>();

        Assert.NotEqual("json", valg.CurrentValue.FormatterName);
    }

    private sealed class SviktHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage forespørsel, CancellationToken stopp) =>
            throw new HttpRequestException("Kilden er nede.", null, HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>Fanger opp alle loggoppføringer fra verten, uansett kategori.</summary>
    private sealed class OppsamlingsLogger : ILoggerProvider
    {
        public List<(LogLevel Nivå, string Melding)> Oppføringer { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(OppsamlingsLogger eier) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (eier.Oppføringer)
                {
                    eier.Oppføringer.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }
}
