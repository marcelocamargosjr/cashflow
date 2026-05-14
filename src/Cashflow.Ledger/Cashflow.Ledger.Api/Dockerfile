# syntax=docker/dockerfile:1.7
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy props/config for restore graph
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["NuGet.config", "./"]
COPY ["global.json", "./"]

# Copy csproj files first to leverage Docker layer cache for restore
COPY ["src/Cashflow.SharedKernel/Cashflow.SharedKernel.csproj", "src/Cashflow.SharedKernel/"]
COPY ["src/Cashflow.Contracts/Cashflow.Contracts.csproj", "src/Cashflow.Contracts/"]
COPY ["src/Cashflow.Ledger/Cashflow.Ledger.Domain/Cashflow.Ledger.Domain.csproj", "src/Cashflow.Ledger/Cashflow.Ledger.Domain/"]
COPY ["src/Cashflow.Ledger/Cashflow.Ledger.Application/Cashflow.Ledger.Application.csproj", "src/Cashflow.Ledger/Cashflow.Ledger.Application/"]
COPY ["src/Cashflow.Ledger/Cashflow.Ledger.Infrastructure/Cashflow.Ledger.Infrastructure.csproj", "src/Cashflow.Ledger/Cashflow.Ledger.Infrastructure/"]
COPY ["src/Cashflow.Ledger/Cashflow.Ledger.Api/Cashflow.Ledger.Api.csproj", "src/Cashflow.Ledger/Cashflow.Ledger.Api/"]

RUN dotnet restore "src/Cashflow.Ledger/Cashflow.Ledger.Api/Cashflow.Ledger.Api.csproj"

COPY . .
WORKDIR /src/src/Cashflow.Ledger/Cashflow.Ledger.Api
RUN dotnet publish "Cashflow.Ledger.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# tzdata gives IANA zone "America/Sao_Paulo" — IClock relies on it.
RUN apk add --no-cache tzdata icu-data-full icu-libs

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

# A imagem aspnet:9.0-alpine já provê o usuário `app` por default (sem precisar criar).
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cashflow.Ledger.Api.dll"]
