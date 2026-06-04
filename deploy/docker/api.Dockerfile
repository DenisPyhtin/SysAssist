FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SysAssist.sln ./
COPY src/SysAssist.Api/*.csproj src/SysAssist.Api/
COPY src/SysAssist.Application/*.csproj src/SysAssist.Application/
COPY src/SysAssist.Contracts/*.csproj src/SysAssist.Contracts/
COPY src/SysAssist.Domain/*.csproj src/SysAssist.Domain/
COPY src/SysAssist.Infrastructure/*.csproj src/SysAssist.Infrastructure/
RUN dotnet restore src/SysAssist.Api/SysAssist.Api.csproj
COPY . .
RUN dotnet publish src/SysAssist.Api/SysAssist.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
USER root
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=5 CMD curl -fsS -H "X-Forwarded-Proto: https" http://localhost:8080/health/live || exit 1
USER $APP_UID
ENTRYPOINT ["dotnet", "SysAssist.Api.dll"]
