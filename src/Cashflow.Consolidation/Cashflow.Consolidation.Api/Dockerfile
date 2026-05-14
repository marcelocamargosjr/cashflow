# syntax=docker/dockerfile:1.7
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["NuGet.config", "./"]
COPY ["global.json", "./"]

COPY ["src/Cashflow.SharedKernel/Cashflow.SharedKernel.csproj", "src/Cashflow.SharedKernel/"]
COPY ["src/Cashflow.Contracts/Cashflow.Contracts.csproj", "src/Cashflow.Contracts/"]
COPY ["src/Cashflow.Consolidation/Cashflow.Consolidation.Domain/Cashflow.Consolidation.Domain.csproj", "src/Cashflow.Consolidation/Cashflow.Consolidation.Domain/"]
COPY ["src/Cashflow.Consolidation/Cashflow.Consolidation.Application/Cashflow.Consolidation.Application.csproj", "src/Cashflow.Consolidation/Cashflow.Consolidation.Application/"]
COPY ["src/Cashflow.Consolidation/Cashflow.Consolidation.Infrastructure/Cashflow.Consolidation.Infrastructure.csproj", "src/Cashflow.Consolidation/Cashflow.Consolidation.Infrastructure/"]
COPY ["src/Cashflow.Consolidation/Cashflow.Consolidation.Api/Cashflow.Consolidation.Api.csproj", "src/Cashflow.Consolidation/Cashflow.Consolidation.Api/"]

RUN dotnet restore "src/Cashflow.Consolidation/Cashflow.Consolidation.Api/Cashflow.Consolidation.Api.csproj"

COPY . .
WORKDIR /src/src/Cashflow.Consolidation/Cashflow.Consolidation.Api
RUN dotnet publish "Cashflow.Consolidation.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# tzdata gives IANA zone "America/Sao_Paulo" — IClock relies on it.
RUN apk add --no-cache tzdata icu-data-full icu-libs

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

RUN addgroup -S app && adduser -S app -G app
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cashflow.Consolidation.Api.dll"]
