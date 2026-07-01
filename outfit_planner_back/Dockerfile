FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# curl is used by the container healthcheck in docker-compose.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/OutfitPlanner.Domain/OutfitPlanner.Domain.csproj src/OutfitPlanner.Domain/
COPY src/OutfitPlanner.Application/OutfitPlanner.Application.csproj src/OutfitPlanner.Application/
COPY src/OutfitPlanner.Infrastructure/OutfitPlanner.Infrastructure.csproj src/OutfitPlanner.Infrastructure/
COPY src/OutfitPlanner.Api/OutfitPlanner.Api.csproj src/OutfitPlanner.Api/
RUN dotnet restore src/OutfitPlanner.Api/OutfitPlanner.Api.csproj
COPY src/OutfitPlanner.Domain/ src/OutfitPlanner.Domain/
COPY src/OutfitPlanner.Application/ src/OutfitPlanner.Application/
COPY src/OutfitPlanner.Infrastructure/ src/OutfitPlanner.Infrastructure/
COPY src/OutfitPlanner.Api/ src/OutfitPlanner.Api/
COPY database/ database/
RUN dotnet publish src/OutfitPlanner.Api/OutfitPlanner.Api.csproj -c Release -o /app/publish

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OutfitPlanner.Api.dll"]
