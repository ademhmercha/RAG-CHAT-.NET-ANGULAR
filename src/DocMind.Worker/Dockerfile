# syntax=docker/dockerfile:1.7
# Build context: the repository root.   docker build -f src/DocMind.Worker/Dockerfile -t docmind-worker .
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/DocMind.Domain/DocMind.Domain.csproj src/DocMind.Domain/
COPY src/DocMind.Application/DocMind.Application.csproj src/DocMind.Application/
COPY src/DocMind.Infrastructure/DocMind.Infrastructure.csproj src/DocMind.Infrastructure/
COPY src/DocMind.ServiceDefaults/DocMind.ServiceDefaults.csproj src/DocMind.ServiceDefaults/
COPY src/DocMind.Worker/DocMind.Worker.csproj src/DocMind.Worker/
RUN dotnet restore src/DocMind.Worker/DocMind.Worker.csproj

COPY src ./src
RUN dotnet publish src/DocMind.Worker/DocMind.Worker.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_gcServer=0 \
    DOTNET_EnableDiagnostics=0

USER $APP_UID
ENTRYPOINT ["dotnet", "DocMind.Worker.dll"]
