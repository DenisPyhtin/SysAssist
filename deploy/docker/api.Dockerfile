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
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SysAssist.Api.dll"]
