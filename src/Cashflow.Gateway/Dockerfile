# syntax=docker/dockerfile:1.7
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["NuGet.config", "./"]
COPY ["global.json", "./"]

COPY ["src/Cashflow.SharedKernel/Cashflow.SharedKernel.csproj", "src/Cashflow.SharedKernel/"]
COPY ["src/Cashflow.Gateway/Cashflow.Gateway.csproj", "src/Cashflow.Gateway/"]

RUN dotnet restore "src/Cashflow.Gateway/Cashflow.Gateway.csproj"

COPY . .
WORKDIR /src/src/Cashflow.Gateway
RUN dotnet publish "Cashflow.Gateway.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache tzdata icu-data-full icu-libs curl

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

# A imagem aspnet:9.0-alpine já provê o usuário `app` por default.
USER app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cashflow.Gateway.dll"]
