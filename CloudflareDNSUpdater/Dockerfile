FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CloudflareDNSUpdater/CloudflareDNSUpdater.csproj", "CloudflareDNSUpdater/"]
RUN dotnet restore "CloudflareDNSUpdater/CloudflareDNSUpdater.csproj"
COPY . .
WORKDIR "/src/CloudflareDNSUpdater"
RUN dotnet build "CloudflareDNSUpdater.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CloudflareDNSUpdater.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudflareDNSUpdater.dll"]
