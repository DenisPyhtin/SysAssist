FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /src

COPY dotnet-tools.json ./
COPY SysAssist.sln ./
COPY src/SysAssist.Api/*.csproj src/SysAssist.Api/
COPY src/SysAssist.Application/*.csproj src/SysAssist.Application/
COPY src/SysAssist.Contracts/*.csproj src/SysAssist.Contracts/
COPY src/SysAssist.Domain/*.csproj src/SysAssist.Domain/
COPY src/SysAssist.Infrastructure/*.csproj src/SysAssist.Infrastructure/

RUN dotnet tool restore \
    && dotnet restore src/SysAssist.Infrastructure/SysAssist.Infrastructure.csproj

COPY . .

ENTRYPOINT ["dotnet", "dotnet-ef", "database", "update", "--project", "src/SysAssist.Infrastructure", "--startup-project", "src/SysAssist.Api", "--context", "SysAssistDbContext"]
