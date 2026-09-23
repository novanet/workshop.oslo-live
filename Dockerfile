# Oslo Live.
#
#   az acr build -r <acr> -t oslo-live:<tag> .
#
# Lagdelingen her er satt opp for rask bygging, ikke for å være pen:
# csproj kopieres og restores FØR resten av koden, slik at restore-laget
# gjenbrukes så lenge ingen har endret prosjektfila. Det er forskjellen
# på et bygg som tar to minutter og ett som tar førti sekunder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/OsloLive/OsloLive.csproj src/OsloLive/
RUN dotnet restore src/OsloLive/OsloLive.csproj

COPY src/OsloLive/ src/OsloLive/
RUN dotnet publish src/OsloLive/OsloLive.csproj \
      -c Release -o /out --no-restore

# Tom mappe som blir historikkmappa i kjørebildet, se under.
RUN mkdir -p /historikk /bysykkel

# Chiseled: ingen shell, ingen pakkebehandler, kjører som ikke-root.
# Mye mindre enn det vanlige aspnet-bildet, som betyr raskere nedlasting
# og raskere oppstart av en ny revisjon.
#
# Det må være «-extra»-varianten. Den vanlige chiseled kjører uten ICU, i
# globalization-invariant mode, og da kaster `new CultureInfo("nb-NO")` i
# Program.cs ved oppstart. Appen er norsk med vilje, så den trenger ICU.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra

WORKDIR /app
COPY --from=build /out .

# Historikkmappa (Historikk:Mappe, standard App_Data/historikk under /app) må kunne
# skrives av ikke-root-brukeren «app» (uid 1654) som chiseled-bildet kjører som.
# Monter gjerne et volum her; uten volum forsvinner bildene sammen med containeren.
COPY --from=build --chown=1654:1654 /historikk /app/App_Data/historikk
# Bysykkeldøgnet (#191) lagrer månedsfila komprimert under App_Data/bysykkel; samme bruker må kunne skrive der.
COPY --from=build --chown=1654:1654 /bysykkel /app/App_Data/bysykkel

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

EXPOSE 8080
ENTRYPOINT ["dotnet", "OsloLive.dll"]
