FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NuGet.Config Directory.Build.props Directory.Packages.props global.json ./
COPY AgenticGateway.slnx ./
COPY src/AgenticGateway.Core/ src/AgenticGateway.Core/
COPY src/AgenticGateway.Protocols/ src/AgenticGateway.Protocols/
COPY src/AgenticGateway.Providers/ src/AgenticGateway.Providers/
COPY src/AgenticGateway.Infrastructure/ src/AgenticGateway.Infrastructure/
COPY src/AgenticGateway.Host/ src/AgenticGateway.Host/

RUN dotnet restore AgenticGateway.slnx --configfile NuGet.Config
RUN dotnet publish src/AgenticGateway.Host/AgenticGateway.Host.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /app/data && chown -R app:app /app
USER app
ENV ASPNETCORE_URLS=http://0.0.0.0:8580
EXPOSE 8580
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet", "AgenticGateway.Host.dll"]
