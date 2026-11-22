# syntax=docker/dockerfile:1.7
# Build context: the repository root.   docker build -f src/DocMind.McpServer/Dockerfile -t docmind-mcp .
# The server speaks MCP over stdio: run it with `docker run -i` (no published port).
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/DocMind.Domain/DocMind.Domain.csproj src/DocMind.Domain/
COPY src/DocMind.Application/DocMind.Application.csproj src/DocMind.Application/
COPY src/DocMind.Infrastructure/DocMind.Infrastructure.csproj src/DocMind.Infrastructure/
COPY src/DocMind.McpServer/DocMind.McpServer.csproj src/DocMind.McpServer/
RUN dotnet restore src/DocMind.McpServer/DocMind.McpServer.csproj

COPY src ./src
RUN dotnet publish src/DocMind.McpServer/DocMind.McpServer.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_gcServer=0 \
    DOTNET_EnableDiagnostics=0

USER $APP_UID
ENTRYPOINT ["dotnet", "DocMind.McpServer.dll"]
