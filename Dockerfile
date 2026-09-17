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

# Chiseled: ingen shell, ingen pakkebehandler, kjører som ikke-root.
# Rundt en tredjedel av størrelsen på det vanlige aspnet-bildet, som
# betyr raskere nedlasting og raskere oppstart av en ny revisjon.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled

WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

EXPOSE 8080
ENTRYPOINT ["dotnet", "OsloLive.dll"]
